using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Contracts.Parsers;
using EventWire.Abstractions.Contracts.Protocol;
using EventWire.Abstractions.Models;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Services;
using EventWire.Core.Exceptions;
using EventWire.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Handlers;

public abstract class TcpHandlerBase : ITcpHandler
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    private readonly IHeaderParser _headerParser;
    private readonly IPayloadSerializerFactory _serializerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private SslStream? _sslStream;
    private Task? _handlerTask;
    private DateTime _lastMessageTime;
    private CancellationTokenSource _handlerCts = new();
    private CancellationTokenSource? _heartbeatCancellationTokenSource;

    protected readonly TcpClient TcpClient;
    protected readonly TcpOptions TcpOptions;
    protected readonly ILogger Logger;
    protected bool Disposed;

    protected TcpHandlerBase(
        TcpClient tcpClient,
        IHeaderParser headerParser,
        IPayloadSerializerFactory serializerFactory,
        IServiceProvider serviceProvider,
        TcpOptions tcpOptions,
        ILogger logger)
    {
        TcpClient = tcpClient;
        _headerParser = headerParser;
        _serializerFactory = serializerFactory;
        _serviceProvider = serviceProvider;
        Logger = logger;
        TcpOptions = tcpOptions;
        _lastMessageTime = DateTime.UtcNow;
    }

    public bool IsCompleted => !TcpClient.Connected ||
                               _handlerTask is { IsCanceled: true }
                                   or { IsCompleted: true }
                                   or { IsCompletedSuccessfully: true }
                                   or { IsFaulted: true };

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectLock.WaitAsync(cancellationToken);
            if (TcpClient is { Connected: true } && _sslStream is not null)
                return;

            await _handlerCts.CancelAsync();

            _handlerCts.Dispose();
            _heartbeatCancellationTokenSource?.Dispose();

            await (_sslStream?.DisposeAsync() ?? ValueTask.CompletedTask);

            if (TcpClient is { Connected: false })
                await TcpClient.ConnectAsync(TcpOptions.IpAddress, TcpOptions.Port, cancellationToken);

            _sslStream = new(TcpClient.GetStream(), false, ValidateCertificate);
            await AuthenticateAsync(_sslStream, cancellationToken);

            _handlerCts = new();
            _handlerTask = Task.Run(async () => await HandleAsync(_handlerCts.Token), _handlerCts.Token);
            _heartbeatCancellationTokenSource = new();
            _ = Task.Run(() => SendHeartbeatsAsync(_heartbeatCancellationTokenSource.Token), cancellationToken);

        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to connect to TCP");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : notnull => await PublishAsync(message, new() { ContentType = "application/json" }, cancellationToken);

    public async Task PublishAsync<TMessage>(TMessage message, Headers headers, CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ObjectDisposedException.ThrowIf(Disposed, nameof(TcpClientHandler));

        try
        {
            await ConnectAsync(cancellationToken);

            var serializer = _serializerFactory.Get(headers.ContentType);
            var payload = await serializer.SerializeAsync(message, cancellationToken);
            var envelope = new Envelope
            {
                Headers = new Dictionary<string, string>
                {
                    { Header.ContentType, headers.ContentType },
                    { Header.PayloadType, message.GetType().GetFullTypeNameWithAssembly() },
                    { Header.ContentLength, Encoding.UTF8.GetByteCount(payload).ToString() },
                },
                Payload = payload,
            };

            if (headers.ApiKey is not null)
                envelope.Headers[Header.ApiKey] = headers.ApiKey;

            if(!await SendMessageInternalAsync(envelope, cancellationToken))
                Logger.LogWarning("Failed to publish envelope with type '{type}' and payload '{payload}'", envelope.Headers[Header.ContentType], envelope.Payload);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to publish message");
            throw;
        }
    }

    /// <summary>
    /// Performs SSL/TLS authentication
    /// </summary>
    protected abstract Task AuthenticateAsync(SslStream sslStream, CancellationToken cancellationToken);

    /// <summary>
    /// Validates headers received from the other end of the connection
    /// </summary>
    protected abstract bool ValidateHeaders(IReadOnlyDictionary<string, string> headers);

    /// <summary>
    /// Sends a message with the specified payload and headers
    /// </summary>
    protected async Task<bool> SendMessageInternalAsync(
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        await ConnectAsync(cancellationToken);

        if (_sslStream is null)
        {
            Logger.LogError("Cannot send message - SSL stream not initialized");
            return false;
        }

        try
        {
            _lastMessageTime = DateTime.UtcNow;

            try
            {
                await _lock.WaitAsync(cancellationToken);
                await _sslStream.WriteAsync(envelope.ToBytes(), cancellationToken);
                await _sslStream.FlushAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }

            if (envelope.Payload is not null)
            {
                Logger.LogInformation(
                    """
                    =====================================
                    Sent message {payloadType}
                    {headers}
                    Payload: {payload}
                    =====================================
                    """,
                    envelope.Headers[Header.PayloadType],
                    JsonSerializer.Serialize(envelope.Headers, _jsonSerializerOptions),
                    envelope.Payload
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send message");
            return false;
        }
    }

    private async Task HandleAsync(CancellationToken cancellationToken)
    {
        if (_sslStream is null)
            throw new SslStreamNullException();

        await using (_sslStream.ConfigureAwait(false))
        {
            try
            {
                using var streamReader = new StreamReader(_sslStream, Encoding.UTF8, leaveOpen: true);

                _lastMessageTime = DateTime.UtcNow;
                while (!cancellationToken.IsCancellationRequested && TcpClient.Connected)
                {
                    try
                    {
                        if (DateTime.UtcNow - _lastMessageTime > TcpOptions.Timeout)
                        {
                            Logger.LogWarning("Connection timed out, disconnecting...");
                            break;
                        }

                        var sw = Stopwatch.StartNew();

                        var headers = await _headerParser.ParseAsync(streamReader, cancellationToken);

                        if (headers.Count < 1)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                            continue;
                        }

                        _lastMessageTime = DateTime.UtcNow;

                        if (headers.ContainsKey(Header.Ping))
                        {
                            Logger.LogDebug("Ping received");
                            continue;
                        }

                        if (!ValidateHeaders(headers))
                        {
                            Logger.LogInformation("Could not validate headers");
                            continue;
                        }

                        var contentLength = int.Parse(headers[Header.ContentLength]);
                        var buffer = new Memory<char>(new char[contentLength]);
                        var read = await streamReader.ReadAsync(buffer, cancellationToken);
                        var payload = buffer[..read].ToString();

                        var envelope = new Envelope
                        {
                            Headers = headers.ToDictionary(),
                            Payload = payload,
                        };

                        sw.Stop();
                        Logger.LogInformation(
                            """
                            =====================================
                            Received message {payloadType}
                            Parsing took {elapsedMicroseconds}μs
                            {envelope}
                            =====================================
                            """,
                            envelope.Headers[Header.PayloadType],
                            Math.Round(sw.Elapsed.TotalMicroseconds, MidpointRounding.ToEven),
                            JsonSerializer.Serialize(envelope, _jsonSerializerOptions)
                        );

                        sw.Start();
                        await ProcessMessageAsync(envelope, cancellationToken);
                        sw.Stop();

                        Logger.LogInformation("Handling took {elapsed}ms",
                            Math.Round(sw.Elapsed.TotalMilliseconds, MidpointRounding.ToEven));
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Failed to process message");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in TCP handler");
            }
        }
    }

    /// <summary>
    /// Processes an incoming message using the appropriate handler
    /// </summary>
    private async Task ProcessMessageAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var payloadType = Type.GetType(envelope.Headers[Header.PayloadType]) ??
                              throw new InvalidOperationException($"Unable to create type '{envelope.Headers[Header.PayloadType]}'");

            var handlerType = typeof(IMessageHandler<>).MakeGenericType(payloadType);
            var handlerServiceType = typeof(IMessageHandlerService<,>).MakeGenericType(payloadType, handlerType);
            var handlerService = (IMessageHandlerService)scope.ServiceProvider.GetRequiredService(handlerServiceType);
            await handlerService.HandleAsync(envelope, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing message");
        }
    }

    /// <summary>
    /// Sends a heartbeat periodically
    /// </summary>
    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_sslStream is null)
                    continue;

                try
                {
                    var envelope = new Envelope
                    {
                        Headers = new Dictionary<string, string>
                        {
                            { Header.Ping, string.Empty },
                        },
                        Payload = null,
                    };

                    await SendMessageInternalAsync(envelope, cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to send heartbeat");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("{sendHeartbeatsAsync} has been cancelled", nameof(SendHeartbeatsAsync));
        }
        catch (Exception e)
        {
            throw new TimeoutException("Failed to send heartbeat", e);
        }
    }

    private bool ValidateCertificate(object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Logger.LogError("Certificate error '{sslPolicyErrors}'", sslPolicyErrors);
        return false;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Disposed)
            return;

        await (_heartbeatCancellationTokenSource?.CancelAsync() ?? Task.CompletedTask);

        await _handlerCts.CancelAsync();
        if (_handlerTask is not null)
            await _handlerTask;

        _heartbeatCancellationTokenSource?.Dispose();
        _handlerCts.Dispose();
        await (_sslStream?.DisposeAsync() ?? ValueTask.CompletedTask);
        TcpClient.Close();
        Disposed = true;
    }
}
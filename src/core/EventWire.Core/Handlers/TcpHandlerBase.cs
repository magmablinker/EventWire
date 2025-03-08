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
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Handlers;

public abstract class TcpHandlerBase : ITcpHandler
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    private readonly TcpClient _tcpClient;
    private readonly IHeaderParser _headerParser;
    private readonly IPayloadSerializerFactory _serializerFactory;
    private readonly IEnvelopeProcessorService _envelopeProcessorService;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private SslStream? _sslStream;
    private Task? _handlerTask;
    private DateTime _lastMessageTime;
    private CancellationTokenSource _handlerCts = new();
    private CancellationTokenSource? _heartbeatCancellationTokenSource;
    private bool _disposed;

    protected readonly TcpOptions TcpOptions;

    protected TcpHandlerBase(
        TcpClient tcpClient,
        IHeaderParser headerParser,
        IPayloadSerializerFactory serializerFactory,
        IEnvelopeProcessorService envelopeProcessorService,
        TcpOptions tcpOptions,
        ILogger logger)
    {
        _tcpClient = tcpClient;
        _headerParser = headerParser;
        _serializerFactory = serializerFactory;
        _envelopeProcessorService = envelopeProcessorService;
        _logger = logger;
        TcpOptions = tcpOptions;
        _lastMessageTime = DateTime.UtcNow;
    }

    public bool IsCompleted => !_tcpClient.Connected ||
                               _handlerTask is { IsCanceled: true }
                                   or { IsCompleted: true }
                                   or { IsCompletedSuccessfully: true }
                                   or { IsFaulted: true };

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectLock.WaitAsync(cancellationToken);
            if (_tcpClient is { Connected: true } && _sslStream is not null)
                return;

            await _handlerCts.CancelAsync();

            _handlerCts.Dispose();
            _heartbeatCancellationTokenSource?.Dispose();

            await (_sslStream?.DisposeAsync() ?? ValueTask.CompletedTask);

            if (_tcpClient is { Connected: false })
                await _tcpClient.ConnectAsync(TcpOptions.IpAddress, TcpOptions.Port, cancellationToken);

            _sslStream = new(_tcpClient.GetStream(), false, ValidateCertificate);
            await AuthenticateAsync(_sslStream, cancellationToken);

            _handlerCts = new();
            _handlerTask = Task.Run(async () => await HandleAsync(_handlerCts.Token), _handlerCts.Token);
            _heartbeatCancellationTokenSource = new();
            _ = Task.Run(() => SendHeartbeatsAsync(_heartbeatCancellationTokenSource.Token), cancellationToken);

        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to connect to TCP");
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
        ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientHandler));

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

            if(!await PublishInternalAsync(envelope, cancellationToken))
                _logger.LogWarning("Failed to publish envelope with type '{type}' and payload '{payload}'", envelope.Headers[Header.ContentType], envelope.Payload);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to publish message");
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
    private async Task<bool> PublishInternalAsync(
        Envelope envelope,
        CancellationToken cancellationToken)
    {
        await ConnectAsync(cancellationToken);

        if (_sslStream is null)
        {
            _logger.LogError("Cannot send message - SSL stream not initialized");
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
                _logger.LogDebug(
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
            _logger.LogError(ex, "Failed to send message");
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
                while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
                {
                    try
                    {
                        if (DateTime.UtcNow - _lastMessageTime > TcpOptions.Timeout)
                        {
                            _logger.LogWarning("Connection timed out, trying to reconnect...");
                            await ConnectAsync(cancellationToken);

                            if(!_tcpClient.Connected)
                            {
                                _logger.LogWarning("Reconnect failed, disconnecting...");
                                break;
                            }
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
                            _logger.LogDebug("Ping received");
                            continue;
                        }

                        if (!ValidateHeaders(headers))
                        {
                            _logger.LogError("Could not validate headers");
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
                        _logger.LogDebug(
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

                        await _envelopeProcessorService.EnqueueAsync(envelope, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to process message");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TCP handler");
            }
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

                    await PublishInternalAsync(envelope, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to send heartbeat");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{sendHeartbeatsAsync} has been cancelled", nameof(SendHeartbeatsAsync));
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

        _logger.LogError("Certificate error '{sslPolicyErrors}'", sslPolicyErrors);
        return false;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await (_heartbeatCancellationTokenSource?.CancelAsync() ?? Task.CompletedTask);

        await _handlerCts.CancelAsync();
        if (_handlerTask is not null)
            await _handlerTask;

        _heartbeatCancellationTokenSource?.Dispose();
        _handlerCts.Dispose();
        await (_sslStream?.DisposeAsync() ?? ValueTask.CompletedTask);
        _tcpClient.Close();
        _disposed = true;
    }
}
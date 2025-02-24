using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Core.Contracts.Handlers;
using EventWire.Core.Contracts.Parsers;
using EventWire.Core.Contracts.Services;
using EventWire.Core.Models;
using EventWire.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Handlers;

internal sealed class TcpClientHandler : ITcpClientHandler
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    private readonly TcpClient _tcpClient;
    private readonly IHeaderParser _headerParser;
    private readonly IServiceProvider _serviceProvider;
    private readonly TcpOptions _tcpOptions;
    private readonly ILogger<TcpClientHandler> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _handlerTask;
    private bool _disposed;

    public TcpClientHandler(TcpClient tcpClient,
        IHeaderParser headerParser,
        IServiceProvider serviceProvider,
        TcpOptions tcpOptions,
        ILogger<TcpClientHandler> logger)
    {
        _tcpClient = tcpClient;
        _headerParser = headerParser;
        _serviceProvider = serviceProvider;
        _tcpOptions = tcpOptions;
        _logger = logger;
    }

    public bool IsCompleted => !_tcpClient.Connected ||
                               _handlerTask is { IsCanceled: true }
                                   or { IsCompleted: true }
                                   or { IsCompletedSuccessfully: true }
                                   or { IsFaulted: true };

    public void Start() => _handlerTask = Task.Run(async () => await HandleAsync(_cts.Token), _cts.Token);

    private async Task HandleAsync(CancellationToken cancellationToken)
    {
        await using var networkStream = _tcpClient.GetStream();
        await using var sslStream = new SslStream(networkStream, true);
        await sslStream.AuthenticateAsServerAsync(_tcpOptions.Certificate.Value,
            clientCertificateRequired: true,
            checkCertificateRevocation: true);

        using var streamReader = new StreamReader(sslStream, Encoding.UTF8, leaveOpen: true);

        var lastMessageTime = DateTime.UtcNow;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow - lastMessageTime > TimeSpan.FromSeconds(20))
                {
                    _logger.LogWarning("Client timed out, disconnecting...");
                    break;
                }

                var sw = Stopwatch.StartNew();
                var headers = await _headerParser.ParseAsync(streamReader, cancellationToken);

                if (headers.Count < 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                    continue;
                }

                lastMessageTime = DateTime.UtcNow;
                if (headers.ContainsKey(Header.Ping)) continue;

                if (!headers.TryGetValue(Header.ApiKey, out var apiKey) ||
                    !_tcpOptions.ApiKeys.Contains(apiKey))
                {
                    _logger.LogWarning("Invalid api key supplied");
                    continue;
                }

                var contentLength = int.Parse(headers[Header.ContentLength]);
                var buffer = new Memory<char>(new char[contentLength]);
                var read = await streamReader.ReadAsync(buffer, cancellationToken);
                var payload = buffer[..read].ToString();

                var envelope = new Envelope
                {
                    Headers = headers,
                    Payload = payload,
                };

                sw.Stop();
                _logger.LogInformation(
                    """
                    =====================================
                    Received message {payloadType}
                    Parsing took {elapsedMicroseconds}μs
                    {envelope}
                    =====================================
                    """,
                    headers[Header.PayloadType],
                    Math.Round(sw.Elapsed.TotalMicroseconds, MidpointRounding.ToEven),
                    JsonSerializer.Serialize(envelope, _jsonSerializerOptions)
                );

                sw.Start();

                await using var scope = _serviceProvider.CreateAsyncScope();
                var payloadType = Type.GetType(headers[Header.PayloadType]) ??
                                  throw new InvalidOperationException($"Unable to create type '{headers[Header.PayloadType]}'");

                var handlerType = typeof(IMessageHandler<>).MakeGenericType(payloadType);
                var handlerServiceType = typeof(IMessageHandlerService<,>).MakeGenericType(payloadType, handlerType);
                var handlerService = (IMessageHandlerService)scope.ServiceProvider.GetRequiredService(handlerServiceType);
                await handlerService.HandleAsync(envelope, cancellationToken);

                sw.Stop();
                _logger.LogInformation("Handling took {elapsed}ms", Math.Round(sw.Elapsed.TotalMilliseconds, MidpointRounding.ToEven));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process message");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _cts.CancelAsync();
        if (_handlerTask is not null)
            await _handlerTask;

        _cts.Dispose();
        _tcpClient.Close();
        _disposed = true;
    }
}

using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EventWire.Abstractions.Contracts.Client;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Models;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Extensions;
using EventWire.Core.Models;
using EventWire.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Services;

internal sealed class MessageService : IMessageService, IDisposable
{
    private readonly IPayloadSerializerFactory _serializerFactory;
    private readonly TcpOptions _tcpOptions;
    private readonly ILogger<MessageService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private CancellationTokenSource? _heartbeatCts;
    private bool _disposed;

    public MessageService(IPayloadSerializerFactory serializerFactory,
        TcpOptions tcpOptions,
        ILogger<MessageService> logger)
    {
        _serializerFactory = serializerFactory;
        _tcpOptions = tcpOptions;
        _logger = logger;
    }

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : notnull => await PublishAsync(message, new() { ContentType = "application/json" }, cancellationToken);

    public async Task PublishAsync<TMessage>(TMessage message, Headers headers, CancellationToken cancellationToken = default)
        where TMessage : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MessageService));

        await _lock.WaitAsync(cancellationToken);

        try
        {
            await EnsureConnectedAsync(cancellationToken);

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

            await _sslStream!.WriteAsync(envelope.ToBytes(), cancellationToken);
            await _sslStream.FlushAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to publish message");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _lock.Dispose();
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _sslStream?.Dispose();
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_tcpClient is { Connected: true })
            return;

        _tcpClient?.Dispose();
        await (_sslStream?.DisposeAsync() ?? ValueTask.CompletedTask);
        _tcpClient = new();
        await _tcpClient.ConnectAsync(_tcpOptions.IpAddress, _tcpOptions.Port, cancellationToken);

        _sslStream = new(_tcpClient.GetStream(), false, ValidateCertificate);
        await _sslStream.AuthenticateAsClientAsync(_tcpOptions.ServerName,
            new X509Certificate2Collection(_tcpOptions.Certificate.Value),
            SslProtocols.Tls12,
            true);

        await (_heartbeatCts?.CancelAsync() ?? Task.CompletedTask);
        _heartbeatCts?.Dispose();
        _heartbeatCts = new();
        _ = Task.Run(() => SendHeartbeatsAsync(_heartbeatCts.Token), cancellationToken);
    }

    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_sslStream is null)
                    continue;

                await _lock.WaitAsync(cancellationToken);
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

                    await _sslStream.WriteAsync(envelope.ToBytes(), cancellationToken);
                    await _sslStream.FlushAsync(cancellationToken);
                }
                finally
                {
                    _lock.Release();
                }
            }
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

        _logger.LogWarning("Certificate error '{sslPolicyErrors}'", sslPolicyErrors);
        return false;
    }
}

using System.Net.Sockets;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Contracts.Parsers;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Services;
using EventWire.Server.Contracts.Registry;
using EventWire.Server.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventWire.Server.BackgroundServices;

internal sealed class EventWireServer : BackgroundService
{
    private readonly IHeaderParser _headerParser;
    private readonly IPayloadSerializerFactory _serializerFactory;
    private readonly IHandlerRegistry _registry;
    private readonly IEnvelopeProcessorService _envelopeProcessorService;
    private readonly TcpOptions _tcpOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EventWireServer> _logger;

    public EventWireServer(IHeaderParser headerParser,
        IPayloadSerializerFactory serializerFactory,
        IHandlerRegistry registry,
        IEnvelopeProcessorService envelopeProcessorService,
        TcpOptions tcpOptions,
        ILoggerFactory loggerFactory,
        ILogger<EventWireServer> logger)
    {
        _headerParser = headerParser;
        _serializerFactory = serializerFactory;
        _registry = registry;
        _envelopeProcessorService = envelopeProcessorService;
        _tcpOptions = tcpOptions;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var tcpListener = new TcpListener(_tcpOptions.IpAddress, _tcpOptions.Port);
        tcpListener.Start();

        _logger.LogInformation("Listening on '{ipAddress}:{port}'", _tcpOptions.IpAddress.ToString(), _tcpOptions.Port);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!tcpListener.Pending())
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                        continue;
                    }

                    var handler = new TcpServerHandler(await tcpListener.AcceptTcpClientAsync(stoppingToken),
                        _headerParser,
                        _serializerFactory,
                        _envelopeProcessorService,
                        _tcpOptions,
                        _loggerFactory.CreateLogger<TcpServerHandler>());
                    await handler.ConnectAsync(stoppingToken);

                    _registry.Register(handler);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to accept tcp client");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown has been requested");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Execution failed");
        }
        finally
        {
            tcpListener.Stop();
        }
    }
}

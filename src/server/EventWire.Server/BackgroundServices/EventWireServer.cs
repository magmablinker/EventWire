using System.Net.Sockets;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventWire.Server.BackgroundServices;

internal sealed class EventWireServer : BackgroundService
{
    private readonly List<ITcpClientHandler> _clients = [];
    private readonly ITcpClientHandlerFactory _tcpClientHandlerFactory;
    private readonly TcpOptions _tcpOptions;
    private readonly ILogger<EventWireServer> _logger;

    public EventWireServer(ITcpClientHandlerFactory tcpClientHandlerFactory,
        TcpOptions tcpOptions,
        ILogger<EventWireServer> logger)
    {
        _tcpClientHandlerFactory = tcpClientHandlerFactory;
        _tcpOptions = tcpOptions;
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

                    var handler = _tcpClientHandlerFactory.Create(await tcpListener.AcceptTcpClientAsync(stoppingToken));
                    handler.Start();

                    _clients.Add(handler);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to accept tcp client");
                }
                finally
                {
                    await CleanupClientsAsync();
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

    private async Task CleanupClientsAsync()
    {
        var completedClients = _clients.Where(client => client.IsCompleted).ToList();
        foreach (var client in completedClients)
        {
            await client.DisposeAsync();
            _clients.Remove(client);
        }
    }
}

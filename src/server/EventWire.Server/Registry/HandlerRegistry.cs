using System.Collections.Concurrent;
using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Server.Contracts.Registry;
using EventWire.Server.Models;
using Microsoft.Extensions.Logging;

namespace EventWire.Server.Registry;

internal sealed class HandlerRegistry : IHandlerRegistry
{
    private bool _disposed;

    private readonly ConcurrentDictionary<Guid, ITcpHandler> _handlers = new();
    private readonly Task _cleanupTask;
    private readonly CancellationTokenSource _cleanupTokenSource = new();
    private readonly ILogger<HandlerRegistry> _logger;

    public HandlerRegistry(ILogger<HandlerRegistry> logger)
    {
        _cleanupTask = Task.Run(async () => await CleanupClientsAsync(), _cleanupTokenSource.Token);
        _logger = logger;
    }

    public Guid Register(ITcpHandler tcpHandler)
    {
        var id = Guid.NewGuid();
        _handlers.TryAdd(id, tcpHandler);
        return id;
    }

    public async Task<bool> TryRemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryRemove(id, out var handler))
            return false;

        await handler.DisposeAsync();
        return true;
    }

    public IReadOnlyList<GuidHandler> GetAll() => _handlers.Select(kvp => new GuidHandler(kvp.Key, kvp.Value)).ToList();

    public ITcpHandler? Find(Guid id) => _handlers.ContainsKey(id) ? _handlers[id] : null;

    private async Task CleanupClientsAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(_cleanupTokenSource.Token))
            {
                var completedClients = GetAll().Where(client => client.Handler.IsCompleted).ToArray();
                foreach (var client in completedClients)
                {
                    await client.Handler.DisposeAsync();
                    await TryRemoveAsync(client.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cancelling cleanup task");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CleanupClientsAsync failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _cleanupTokenSource.CancelAsync();
        await _cleanupTask;
        _cleanupTask.Dispose();
        _cleanupTokenSource.Dispose();
        _disposed = true;
    }
}

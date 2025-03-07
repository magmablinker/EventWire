using EventWire.Abstractions.Models;

namespace EventWire.Abstractions.Contracts.Handlers;

/// <summary>
/// Base interface for TCP communication handlers
/// </summary>
public interface ITcpHandler : IAsyncDisposable
{
    bool IsCompleted { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : notnull;
    Task PublishAsync<TMessage>(TMessage message, Headers headers, CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
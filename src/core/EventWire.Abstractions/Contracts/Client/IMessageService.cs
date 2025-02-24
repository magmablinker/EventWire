using EventWire.Abstractions.Models;

namespace EventWire.Abstractions.Contracts.Client;

public interface IMessageService
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : notnull;

    Task PublishAsync<TMessage>(TMessage message, Headers headers, CancellationToken cancellationToken = default)
        where TMessage : notnull;
}

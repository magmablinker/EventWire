using EventWire.Abstractions.Models;

namespace EventWire.Abstractions.Contracts.Handlers;

public interface IMessageHandler<TMessage> where TMessage : notnull
{
    ValueTask HandleAsync(MessageHandlerContext<TMessage> context, CancellationToken cancellationToken = default);
}
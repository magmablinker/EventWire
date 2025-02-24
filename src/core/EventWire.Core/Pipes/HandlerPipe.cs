using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Models;

namespace EventWire.Core.Pipes;
#pragma warning disable S2326
internal interface IHandlerPipe<THandler, TPayload> : IMessageHandlerPipe<TPayload> where TPayload : notnull
#pragma warning restore S2326
{
}

internal sealed class HandlerPipe<THandler, TPayload> : IHandlerPipe<THandler, TPayload> where TPayload : notnull
    where THandler : class, IMessageHandler<TPayload>
{
    private readonly THandler _handler;

    public HandlerPipe(THandler handler)
    {
        _handler = handler;
    }

    public async ValueTask ProcessAsync(Func<ValueTask> next, MessageHandlerContext<TPayload> context, CancellationToken cancellationToken = default)
    {
        await _handler.HandleAsync(context, cancellationToken);
        await next();
    }
}
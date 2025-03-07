using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Models;

namespace EventWire.Core.Contracts.Services;

internal interface IMessageHandlerService
{
    Task HandleAsync(Envelope envelope, CancellationToken cancellationToken = default);
}

#pragma warning disable S2326
// ReSharper disable once UnusedTypeParameter
internal interface IMessageHandlerService<TMessage, THandler> : IMessageHandlerService
#pragma warning restore S2326
    where TMessage : notnull
    where THandler : IMessageHandler<TMessage>
{
}
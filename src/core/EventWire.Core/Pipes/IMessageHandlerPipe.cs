using EventWire.Abstractions.Models;

namespace EventWire.Core.Pipes;

public interface IMessageHandlerPipe<TPayload> : IPipe<MessageHandlerContext<TPayload>>;

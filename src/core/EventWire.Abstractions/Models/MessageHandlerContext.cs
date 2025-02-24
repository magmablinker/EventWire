namespace EventWire.Abstractions.Models;

public sealed class MessageHandlerContext<TPayload>
{
    public required TPayload Payload { get; init; }
}

using ProtoBuf;

namespace EventWire.Sample.Messages;

[ProtoContract]
internal sealed class TestProtoBufMessage
{
    [ProtoMember(1)]
    public required string Example { get; init; }
}

using EventWire.Abstractions.Contracts.Serializers;
using ProtoBuf;

namespace EventWire.Serializers.ProtoBuf;

internal sealed class ProtoBufPayloadSerializer : IPayloadSerializer
{
    public string ContentType => "application/x-protobuf";

    public ValueTask<string> SerializeAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        return ValueTask.FromResult(Convert.ToBase64String(stream.ToArray()));
    }

    public ValueTask<TPayload?> DeserializeAsync<TPayload>(string message, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(message));
        return ValueTask.FromResult<TPayload?>(Serializer.Deserialize<TPayload>(stream));
    }
}

using EventWire.Abstractions.Contracts.Serializers;
using MessagePack;

namespace EventWire.Serializers.MessagePack;

internal sealed class MessagePackPayloadSerializer : IPayloadSerializer
{
    public string ContentType => "application/x-msgpack";

    public async ValueTask<string> SerializeAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream();
        await MessagePackSerializer.SerializeAsync(stream, message, cancellationToken: cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return Convert.ToBase64String(stream.ToArray());
    }

    public async ValueTask<TPayload?> DeserializeAsync<TPayload>(string message, CancellationToken cancellationToken = default)
    {
        await using var stream = new MemoryStream(Convert.FromBase64String(message));
        return await MessagePackSerializer.DeserializeAsync<TPayload>(stream, cancellationToken: cancellationToken);
    }
}

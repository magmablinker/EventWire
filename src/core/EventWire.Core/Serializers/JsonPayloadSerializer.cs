using System.Text.Json;
using EventWire.Abstractions.Contracts.Serializers;

namespace EventWire.Core.Serializers;

internal sealed class JsonPayloadSerializer : IPayloadSerializer
{
    public string ContentType => "application/json";

    public ValueTask<string> SerializeAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(message);
        return ValueTask.FromResult(serialized);
    }

    public ValueTask<TPayload?> DeserializeAsync<TPayload>(string message, CancellationToken cancellationToken = default)
    {
        var deserialized = JsonSerializer.Deserialize<TPayload>(message);
        return ValueTask.FromResult(deserialized);
    }
}

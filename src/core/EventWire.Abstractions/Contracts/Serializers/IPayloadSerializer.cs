namespace EventWire.Abstractions.Contracts.Serializers;

public interface IPayloadSerializer
{
    string ContentType { get; }
    ValueTask<string> SerializeAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    ValueTask<TPayload?> DeserializeAsync<TPayload>(string message, CancellationToken cancellationToken = default);
}

using EventWire.Abstractions.Contracts.Serializers;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Exceptions;

namespace EventWire.Core.Factories;

internal sealed class PayloadSerializerFactory : IPayloadSerializerFactory
{
    private readonly IReadOnlyList<IPayloadSerializer> _serializers;

    public PayloadSerializerFactory(IEnumerable<IPayloadSerializer> serializers)
    {
        _serializers = serializers.ToArray();
    }

    public IPayloadSerializer Get(string contentType) => _serializers.FirstOrDefault(s => s.ContentType == contentType) ??
                                                         throw new SerializerNotFoundException(contentType);
}


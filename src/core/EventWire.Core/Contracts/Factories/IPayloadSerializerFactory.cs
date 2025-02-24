using EventWire.Abstractions.Contracts.Serializers;

namespace EventWire.Core.Contracts.Factories;

public interface IPayloadSerializerFactory
{
    IPayloadSerializer Get(string contentType);
}
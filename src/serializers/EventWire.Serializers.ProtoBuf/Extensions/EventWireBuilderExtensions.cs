using EventWire.Abstractions.Contracts.Builders;
using EventWire.Abstractions.Contracts.Serializers;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Serializers.ProtoBuf.Extensions;

public static class EventWireBuilderExtensions
{
    public static IEventWireBuilder AddProtoBuf(this IEventWireBuilder builder)
    {
        builder.Services.AddSingleton<IPayloadSerializer, ProtoBufPayloadSerializer>();
        return builder;
    }
}

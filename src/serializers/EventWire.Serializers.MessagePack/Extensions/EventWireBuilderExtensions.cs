using EventWire.Abstractions.Contracts.Builders;
using EventWire.Abstractions.Contracts.Serializers;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Serializers.MessagePack.Extensions;

public static class EventWireBuilderExtensions
{
    public static IEventWireBuilder AddMessagePack(this IEventWireBuilder builder)
    {
        builder.Services.AddSingleton<IPayloadSerializer, MessagePackPayloadSerializer>();
        return builder;
    }
}

using EventWire.Abstractions.Contracts.Builders;
using EventWire.Server.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Server.Extensions;

public static class EventWireBuilderExtensions
{
    public static IEventWireBuilder AddServer(this IEventWireBuilder builder)
    {
        builder.Services.AddHostedService<EventWireServer>();
        return builder;
    }
}

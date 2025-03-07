using EventWire.Abstractions.Contracts.Builders;
using EventWire.Server.BackgroundServices;
using EventWire.Server.Contracts.Registry;
using EventWire.Server.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Server.Extensions;

public static class EventWireBuilderExtensions
{
    public static IEventWireBuilder AddServer(this IEventWireBuilder builder)
    {
        builder.Services.AddHostedService<EventWireServer>()
            .AddSingleton<IHandlerRegistry, HandlerRegistry>();
        return builder;
    }
}

using System.Reflection;
using EventWire.Abstractions.Contracts.Builders;
using EventWire.Abstractions.Contracts.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Core.Extensions;

public static class EventWireBuilderExtensions
{
    public static IEventWireBuilder AddMessageHandlers(this IEventWireBuilder builder, Assembly assembly)
    {
        var handlerType = typeof(IMessageHandler<>);

        var types = assembly.GetTypes()
            .Where(t => Array.Exists(t.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType) &&
                        t is { IsAbstract: false, IsInterface: false })
            .ToList();

        foreach (var type in types)
        {
            var implementedInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerType);

            foreach (var implementedInterface in implementedInterfaces)
            {
                builder.Services.AddScoped(implementedInterface, type);
            }
        }

        return builder;
    }
}

using EventWire.Abstractions.Contracts.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Core.Builders;

internal sealed class EventWireBuilder : IEventWireBuilder
{
    public EventWireBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }
}

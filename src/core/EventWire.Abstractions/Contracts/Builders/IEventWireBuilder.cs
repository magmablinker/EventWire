using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Abstractions.Contracts.Builders;

public interface IEventWireBuilder
{
    IServiceCollection Services { get; }
}

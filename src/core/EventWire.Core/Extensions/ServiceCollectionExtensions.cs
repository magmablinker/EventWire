using EventWire.Abstractions.Contracts.Builders;
using EventWire.Abstractions.Contracts.Client;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Contracts.Serializers;
using EventWire.Core.Builders;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Parsers;
using EventWire.Core.Contracts.Services;
using EventWire.Core.Factories;
using EventWire.Core.Parsers;
using EventWire.Core.Pipes;
using EventWire.Core.Serializers;
using EventWire.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventWire.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IEventWireBuilder AddEventWire(this IServiceCollection services, Action<TcpOptions> configure)
    {
        var tcpOptions = new TcpOptions();
        configure(tcpOptions);

        services.AddSingleton(tcpOptions)
            .AddSingleton<IPayloadSerializer, JsonPayloadSerializer>()
            .AddSingleton<IPayloadSerializerFactory, PayloadSerializerFactory>()
            .AddSingleton<ITcpClientHandlerFactory, TcpClientHandlerFactory>()
            .AddSingleton<IHeaderParser, HeaderParser>()
            .AddScoped<IPipelineExecutorService, PipelineExecutorService>()
            .AddScoped(typeof(IMessageHandlerService<,>), typeof(MessageHandlerService<,>))
            .AddScoped(typeof(IHandlerPipe<,>), typeof(HandlerPipe<,>))
            .AddScoped<IMessageService, MessageService>();

        return new EventWireBuilder(services);
    }
}

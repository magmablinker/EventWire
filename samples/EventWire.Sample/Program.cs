using System.Net;
using EventWire.Abstractions.Models;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Extensions;
using EventWire.Sample;
using EventWire.Sample.Messages;
using EventWire.Serializers.MessagePack.Extensions;
using EventWire.Serializers.ProtoBuf.Extensions;
using EventWire.Server.Contracts.Registry;
using EventWire.Server.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using var host = Host.CreateDefaultBuilder()
    .ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Debug))
    .ConfigureServices(services =>
    {
        services.AddEventWire(tcpOptions =>
            {
                tcpOptions.IpAddress = IPAddress.Loopback;
                tcpOptions.Port = 777;
                tcpOptions.ApiKeys = ["test"];
                tcpOptions.ServerName = "localhost";
                tcpOptions.CertPath = @"C:\temp\my_certificate.pfx";
                tcpOptions.CertPassword = "test";
            })
            .AddMessageHandlers(AssemblyProvider.Current)
            .AddMessagePack()
            .AddProtoBuf()
            .AddServer();
    })
    .Build();

var app = host.RunAsync();

await Task.Delay(TimeSpan.FromSeconds(1));

await using (var scope = host.Services.CreateAsyncScope())
{
    var tcpClientHandlerFactory = scope.ServiceProvider.GetRequiredService<ITcpClientHandlerFactory>();
    var registry = scope.ServiceProvider.GetRequiredService<IHandlerRegistry>();
    var messageClient = tcpClientHandlerFactory.Create(new ());

    var tasks = Enumerable.Range(0, 200)
        .Select(async i => await messageClient.PublishAsync(new TestMessage { Example = $"[{i}] Im a message" },
            new Headers
            {
                ApiKey = "test",
            }))
        .ToArray();
    await Task.WhenAll(tasks);

    var tasks2 = Enumerable.Range(0, 200)
        .Select(async i => await messageClient.PublishAsync(new TestProtoBufMessage { Example = $"[{i}] Im a protobuf message" },
            new Headers
            {
                ContentType = "application/x-protobuf",
                ApiKey = "test",
            }))
        .ToArray();
    await Task.WhenAll(tasks2);

    await Task.Delay(TimeSpan.FromSeconds(2));

    var clients = registry.GetAll();
    foreach(var client in clients)
        await client.Handler.PublishAsync(new TestMessage { Example = "This is a broadcast message" });
}

await app;
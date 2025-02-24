using System.Net;
using EventWire.Abstractions.Contracts.Client;
using EventWire.Abstractions.Models;
using EventWire.Core.Extensions;
using EventWire.Sample;
using EventWire.Sample.Messages;
using EventWire.Serializers.MessagePack.Extensions;
using EventWire.Server.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = Host.CreateDefaultBuilder()
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
            .AddServer();
    })
    .Build();

var app = host.RunAsync();

await Task.Delay(TimeSpan.FromSeconds(1));

await using (var scope = host.Services.CreateAsyncScope())
{
    var messageClient = scope.ServiceProvider.GetRequiredService<IMessageService>();
    var tasks = Enumerable.Range(0, 200)
        .Select(async i => await messageClient.PublishAsync(new TestMessage { Example = $"[{i}] Im a message" },
            new Headers
            {
                ContentType = "application/json",
                ApiKey = "test",
            }))
        .ToArray();
    await Task.WhenAll(tasks);
}

await app;
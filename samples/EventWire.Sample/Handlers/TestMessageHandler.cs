using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Models;
using EventWire.Sample.Messages;
using Microsoft.Extensions.Logging;

namespace EventWire.Sample.Handlers;

internal sealed class TestMessageHandler : IMessageHandler<TestMessage>
{
    private readonly ILogger<TestMessageHandler> _logger;

    public TestMessageHandler(ILogger<TestMessageHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(MessageHandlerContext<TestMessage> context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received message '{message}'", context.Payload.Example);
        return Task.CompletedTask;
    }
}

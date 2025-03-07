using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Models;
using EventWire.Sample.Messages;
using Microsoft.Extensions.Logging;

namespace EventWire.Sample.Handlers;

internal sealed class TestProtoBufMessageHandler : IMessageHandler<TestProtoBufMessage>
{
    private readonly ILogger<TestProtoBufMessageHandler> _logger;

    public TestProtoBufMessageHandler(ILogger<TestProtoBufMessageHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(MessageHandlerContext<TestProtoBufMessage> context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received message '{message}'", context.Payload.Example);
        return Task.CompletedTask;
    }
}

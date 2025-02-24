using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Services;
using EventWire.Core.Models;
using EventWire.Core.Pipes;
using EventWire.Core.Protocol;

namespace EventWire.Core.Services;

internal sealed class MessageHandlerService<TMessage, THandler> : IMessageHandlerService<TMessage, THandler>
    where TMessage : notnull
    where THandler : IMessageHandler<TMessage>
{
    private readonly IReadOnlyList<IMessageHandlerPipe<TMessage>> _pipes;
    private readonly IHandlerPipe<THandler, TMessage> _handlerPipe;
    private readonly IPipelineExecutorService _pipelineExecutorService;
    private readonly IPayloadSerializerFactory _payloadSerializerFactory;

    public MessageHandlerService(IEnumerable<IMessageHandlerPipe<TMessage>> handlers,
        IHandlerPipe<THandler, TMessage> handlerPipe,
        IPipelineExecutorService pipelineExecutorService,
        IPayloadSerializerFactory payloadSerializerFactory)
    {
        _pipes = handlers.ToArray();
        _handlerPipe = handlerPipe;
        _pipelineExecutorService = pipelineExecutorService;
        _payloadSerializerFactory = payloadSerializerFactory;
    }

    public async Task HandleAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(envelope.Payload))
            throw new InvalidOperationException("Payload cannot be empty");

        var serializer = _payloadSerializerFactory.Get(envelope.Headers[Header.ContentType]);
        var payload = await serializer.DeserializeAsync<TMessage>(envelope.Payload, cancellationToken) ??
                      throw new InvalidOperationException("Unable to parse message");

        await _pipelineExecutorService.ExecuteAsync([.. _pipes, _handlerPipe],
            new()
            {
                Payload = payload,
            },
            cancellationToken);
    }
}

using System.Net.Sockets;
using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Contracts.Parsers;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Services;
using EventWire.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Factories;

internal sealed class TcpClientHandlerFactory : ITcpClientHandlerFactory
{
    private readonly IHeaderParser _headerParser;
    private readonly IPayloadSerializerFactory _serializerFactory;
    private readonly IEnvelopeProcessorService _envelopeProcessorService;
    private readonly TcpOptions _tcpOptions;
    private readonly ILoggerFactory _loggerFactory;

    public TcpClientHandlerFactory(IHeaderParser headerParser,
        IPayloadSerializerFactory serializerFactory,
        IEnvelopeProcessorService envelopeProcessorService,
        TcpOptions tcpOptions,
        ILoggerFactory loggerFactory)
    {
        _headerParser = headerParser;
        _serializerFactory = serializerFactory;
        _envelopeProcessorService = envelopeProcessorService;
        _tcpOptions = tcpOptions;
        _loggerFactory = loggerFactory;
    }

    public ITcpClientHandler Create(TcpClient client) => new TcpClientHandler(client,
        _headerParser,
        _serializerFactory,
        _envelopeProcessorService,
        _tcpOptions,
        _loggerFactory.CreateLogger<TcpClientHandler>());

}

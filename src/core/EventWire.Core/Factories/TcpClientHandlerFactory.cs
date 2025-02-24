using System.Net.Sockets;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Contracts.Handlers;
using EventWire.Core.Contracts.Parsers;
using EventWire.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Factories;

internal sealed class TcpClientHandlerFactory : ITcpClientHandlerFactory
{
    private readonly IHeaderParser _headerParser;
    private readonly IServiceProvider _serviceProvider;
    private readonly TcpOptions _tcpOptions;
    private readonly ILoggerFactory _loggerFactory;

    public TcpClientHandlerFactory(IHeaderParser headerParser,
        IServiceProvider serviceProvider,
        TcpOptions tcpOptions,
        ILoggerFactory loggerFactory)
    {
        _headerParser = headerParser;
        _serviceProvider = serviceProvider;
        _tcpOptions = tcpOptions;
        _loggerFactory = loggerFactory;
    }

    public ITcpClientHandler Create(TcpClient client) =>
        new TcpClientHandler(client,
            _headerParser,
            _serviceProvider,
            _tcpOptions,
            _loggerFactory.CreateLogger<TcpClientHandler>());
}

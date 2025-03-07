using System.Net.Security;
using System.Net.Sockets;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Contracts.Parsers;
using EventWire.Abstractions.Contracts.Protocol;
using EventWire.Core.Contracts.Factories;
using EventWire.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace EventWire.Server.Handlers;

internal sealed class TcpServerHandler : TcpHandlerBase, ITcpServerHandler
{
    public TcpServerHandler(
        TcpClient tcpClient,
        IHeaderParser headerParser,
        IPayloadSerializerFactory serializerFactory,
        IServiceProvider serviceProvider,
        TcpOptions options,
        ILogger<TcpServerHandler> logger)
        : base(tcpClient, headerParser, serializerFactory, serviceProvider, options, logger)
    {
    }

    protected override async Task AuthenticateAsync(SslStream sslStream, CancellationToken cancellationToken)
    {
        await sslStream.AuthenticateAsServerAsync(
            TcpOptions.Certificate.Value,
            clientCertificateRequired: true,
            checkCertificateRevocation: true);
    }

    protected override bool ValidateHeaders(IReadOnlyDictionary<string, string> headers) =>
        headers.TryGetValue(Header.ApiKey, out var apiKey) &&
        TcpOptions.ApiKeys.Contains(apiKey);
}
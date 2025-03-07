using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Abstractions.Contracts.Options;
using EventWire.Abstractions.Contracts.Parsers;
using EventWire.Core.Contracts.Factories;
using Microsoft.Extensions.Logging;

namespace EventWire.Core.Handlers;

internal sealed class TcpClientHandler : TcpHandlerBase, ITcpClientHandler
{
    public TcpClientHandler(
        TcpClient tcpClient,
        IHeaderParser headerParser,
        IPayloadSerializerFactory serializerFactory,
        IServiceProvider serviceProvider,
        TcpOptions options,
        ILogger<TcpClientHandler> logger)
        :  base(tcpClient, headerParser, serializerFactory, serviceProvider, options, logger)
    {
    }

    protected override async Task AuthenticateAsync(SslStream sslStream, CancellationToken cancellationToken)
    {
        await sslStream.AuthenticateAsClientAsync(TcpOptions.ServerName,
            new X509Certificate2Collection(TcpOptions.Certificate.Value),
            SslProtocols.Tls12,
            true);
    }

    // Client doesn't need to validate incoming messages in the same way
    // Server is trusted, and we'll let the message handler handle any issues
    protected override bool ValidateHeaders(IReadOnlyDictionary<string, string> headers) => true;
}
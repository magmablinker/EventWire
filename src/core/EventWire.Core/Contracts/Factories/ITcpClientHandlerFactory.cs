using System.Net.Sockets;
using EventWire.Abstractions.Contracts.Handlers;

namespace EventWire.Core.Contracts.Factories;

public interface ITcpClientHandlerFactory
{
    ITcpClientHandler Create(TcpClient client);
}

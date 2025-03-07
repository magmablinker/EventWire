using EventWire.Abstractions.Contracts.Handlers;
using EventWire.Server.Models;

namespace EventWire.Server.Contracts.Registry;

public interface IHandlerRegistry : IAsyncDisposable
{
    Guid Register(ITcpHandler tcpHandler);
    Task<bool> TryRemoveAsync(Guid id, CancellationToken cancellationToken = default);
    IReadOnlyList<GuidHandler> GetAll();
    ITcpHandler? Find(Guid id);
}

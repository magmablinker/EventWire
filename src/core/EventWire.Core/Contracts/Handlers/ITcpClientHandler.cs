namespace EventWire.Core.Contracts.Handlers;

public interface ITcpClientHandler : IAsyncDisposable
{
    bool IsCompleted { get; }
    void Start();
}

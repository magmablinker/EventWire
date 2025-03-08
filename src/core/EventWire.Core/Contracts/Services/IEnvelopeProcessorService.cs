using EventWire.Abstractions.Models;

namespace EventWire.Core.Contracts.Services;

public interface IEnvelopeProcessorService : IAsyncDisposable
{
    Task EnqueueAsync(Envelope envelope, CancellationToken cancellationToken = default);
}
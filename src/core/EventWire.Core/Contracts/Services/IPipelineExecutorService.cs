using EventWire.Core.Pipes;

namespace EventWire.Core.Contracts.Services;
internal interface IPipelineExecutorService
{
    ValueTask ExecuteAsync<TValue>(IEnumerable<IPipe<TValue>> pipes,
        TValue value,
        CancellationToken cancellationToken = default);
}

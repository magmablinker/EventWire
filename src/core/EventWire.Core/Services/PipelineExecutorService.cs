using EventWire.Core.Contracts.Services;
using EventWire.Core.Pipes;

namespace EventWire.Core.Services;

internal sealed class PipelineExecutorService : IPipelineExecutorService
{
    public async ValueTask ExecuteAsync<TValue>(IEnumerable<IPipe<TValue>> pipes,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queue = new Queue<IPipe<TValue>>(pipes);
        queue.Enqueue(new TerminationPipe<TValue>());
        await Dequeue(queue, value, cancellationToken);
    }

    private static async ValueTask Dequeue<TValue>(Queue<IPipe<TValue>> queue,
        TValue value,
        CancellationToken cancellationToken = default) =>
        await queue.Dequeue()
            .ProcessAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Dequeue(queue, value, cancellationToken);
                },
                value,
                cancellationToken);
}
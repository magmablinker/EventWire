namespace EventWire.Core.Pipes;
internal sealed class TerminationPipe<TValue> : IPipe<TValue>
{
    public ValueTask ProcessAsync(Func<ValueTask> next, TValue context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
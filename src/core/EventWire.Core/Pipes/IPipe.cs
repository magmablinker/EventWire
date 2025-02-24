namespace EventWire.Core.Pipes;

public interface IPipe<in TContext>
{
    ValueTask ProcessAsync(Func<ValueTask> next,
        TContext context,
        CancellationToken cancellationToken = default);
}
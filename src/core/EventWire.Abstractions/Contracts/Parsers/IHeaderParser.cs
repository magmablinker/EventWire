namespace EventWire.Abstractions.Contracts.Parsers;
public interface IHeaderParser
{
    public Task<IReadOnlyDictionary<string, string>> ParseAsync(StreamReader reader,
        CancellationToken cancellationToken = default);
}

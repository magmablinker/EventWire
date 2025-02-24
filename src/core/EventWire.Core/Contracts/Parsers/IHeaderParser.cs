namespace EventWire.Core.Contracts.Parsers;
internal interface IHeaderParser
{
    public Task<IDictionary<string, string>> ParseAsync(StreamReader reader,
        CancellationToken cancellationToken = default);
}

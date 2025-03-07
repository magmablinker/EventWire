using EventWire.Abstractions.Contracts.Parsers;

namespace EventWire.Core.Parsers;

internal sealed class HeaderParser : IHeaderParser
{
    public async Task<IReadOnlyDictionary<string, string>> ParseAsync(StreamReader reader,
        CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
                break;

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
                headers[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        return headers;
    }
}

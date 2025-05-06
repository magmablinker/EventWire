using EventWire.Abstractions.Contracts.Parsers;
using EventWire.Abstractions.Contracts.Protocol;

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

            if (!TryGetHeader(line, out var key, out var value))
                continue;

            headers.Add(key, value);
        }

        return headers;
    }

    private static bool TryGetHeader(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);

        if (separatorIndex < 0)
        {
            return false;
        }

        key = line[..separatorIndex].Trim();
        value = line[(separatorIndex + 1)..].Trim();

        return true;
    }

}

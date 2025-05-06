using BenchmarkDotNet.Attributes;

namespace EventWire.Benchmark.Evaluations;

public class HeaderParser
{
    private const string Header = "key:value";

    [Benchmark]
    public KeyValuePair<string, string> ExtractHeaderWithSpan()
    {
        var span = Header.AsSpan();
        var separatorIndex = span.IndexOf(':');

        if (separatorIndex < 0)
            return default;

        var keySpan = span[..separatorIndex];
        var valueSpan = span[(separatorIndex + 1)..];

        keySpan = keySpan.Trim();
        valueSpan = valueSpan.Trim();

        return new KeyValuePair<string, string>(
            keySpan.ToString(),
            valueSpan.ToString());
    }

    [Benchmark]
    public KeyValuePair<string, string> ExtractHeaderWithString()
    {
        var separatorIndex = Header.IndexOf(':', StringComparison.Ordinal);

        return new KeyValuePair<string, string>(Header[..separatorIndex].Trim(),
            Header[(separatorIndex + 1)..].Trim());
    }
}
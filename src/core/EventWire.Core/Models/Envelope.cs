using System.Text;
using EventWire.Core.Protocol;

namespace EventWire.Core.Models;

internal sealed class Envelope
{
    public required IDictionary<string, string> Headers { get; init; }
    public required string? Payload { get; init; }

    public byte[] ToBytes()
    {
        var messageBuilder = new StringBuilder();

        messageBuilder.Append(string.Join(Specification.Separator, Headers.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
        messageBuilder.Append(Specification.PayloadSeparator);

        if (!string.IsNullOrEmpty(Payload)) messageBuilder.Append(Payload);

        return Encoding.UTF8.GetBytes(messageBuilder.ToString());
    }
}
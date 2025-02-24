namespace EventWire.Abstractions.Models;

public sealed class Headers
{
    public required string ContentType { get; init; }
    public string? ApiKey { get; init; }
}

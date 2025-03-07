namespace EventWire.Abstractions.Models;

public sealed class Headers
{
    public string ContentType { get; init; } = "application/json";
    public string? ApiKey { get; init; }
}

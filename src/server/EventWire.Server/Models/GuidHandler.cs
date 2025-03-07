using EventWire.Abstractions.Contracts.Handlers;

namespace EventWire.Server.Models;

public sealed record class GuidHandler(Guid Id, ITcpHandler Handler);
namespace EventWire.Core.Exceptions;

public sealed class SerializerNotFoundException(string contentType)
    : Exception($"Serializer for '{contentType}' could not be found");

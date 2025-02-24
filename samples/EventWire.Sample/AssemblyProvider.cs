using System.Reflection;

namespace EventWire.Sample;

internal static class AssemblyProvider
{
    public static readonly Assembly Current = typeof(AssemblyProvider).Assembly;
}

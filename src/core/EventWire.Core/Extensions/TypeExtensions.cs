namespace EventWire.Core.Extensions;
internal static class TypeExtensions
{
    public static string GetFullTypeNameWithAssembly(this Type type) => type.AssemblyQualifiedName ?? $"{type.FullName!}, {type.Assembly}";
}

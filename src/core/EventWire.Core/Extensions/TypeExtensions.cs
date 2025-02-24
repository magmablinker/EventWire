namespace EventWire.Core.Extensions;
internal static class TypeExtensions
{
    public static string GetFullTypeNameWithAssembly(this Type type) => $"{type.FullName!}, {type.Assembly}";
}

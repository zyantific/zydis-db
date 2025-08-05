namespace Zydis.Generator.Core.Definitions;

public enum Rex2Type
{
    Forbidden,
    Allowed,
    Mandatory,
    AlwaysAllowed, // TODO: Hack!
}

public static class Rex2TypeExtensions
{
    public static string ToZydisString(this Rex2Type value)
    {
        return value.ToString("G").ToUpperInvariant().Replace("ALWAYS", "");
    }
}

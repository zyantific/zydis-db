namespace Zydis.Generator.Core.Definitions;

public enum SourceConditionCode
{
    None,
    O,
    NO,
    B,
    NB,
    Z,
    NZ,
    BE,
    NBE,
    S,
    NS,
    True,
    False,
    L,
    NL,
    LE,
    NLE,
}

public static class SourceConditionCodeExtensions
{
    public static string ToZydisString(this SourceConditionCode value)
    {
        return "ZYDIS_SCC_" + value.ToString("G").ToUpperInvariant();
    }
}

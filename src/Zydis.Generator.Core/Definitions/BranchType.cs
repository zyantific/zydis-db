using System;

namespace Zydis.Generator.Core.Definitions;

public enum BranchType
{
    None,
    ShortRel,
    NearRel,
    Far,
    Absolute,
}

public static class BranchTypeExtensions
{
    public static string ToZydisString(this BranchType value)
    {
        return value switch
        {
            BranchType.None => "NONE",
            BranchType.ShortRel => "SHORT",
            BranchType.NearRel => "NEAR",
            BranchType.Far => "FAR",
            BranchType.Absolute => "ABSOLUTE",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

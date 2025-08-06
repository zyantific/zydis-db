using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Zydis.Generator.Core.Definitions;

[Flags]
public enum WidthFlag
{
    Invalid = 0,
    Width16 = 1,
    Width32 = 2,
    Width64 = 4,
}

public static class WidthFlagExtensions
{
    private static readonly IReadOnlyDictionary<WidthFlag, string> ZydisNames = new Dictionary<WidthFlag, string>
    {
        [WidthFlag.Width16] = "ZYDIS_WIDTH_16",
        [WidthFlag.Width32] = "ZYDIS_WIDTH_32",
        [WidthFlag.Width64] = "ZYDIS_WIDTH_64",
    }.ToFrozenDictionary();

    public static string ToZydisString(this WidthFlag value)
    {
        return string.Join(" | ", Enum.GetValues<WidthFlag>()
            .Where(x => x != 0 && value.HasFlag(x))
            .Select(x => ZydisNames[x]));
    }
}

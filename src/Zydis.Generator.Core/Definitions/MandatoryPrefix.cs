using System;

namespace Zydis.Generator.Core.Definitions;

public enum MandatoryPrefix
{
   None,
   Ignore,
   P66,
   PF2,
   PF3,
}

public static class MandatoryPrefixExtensions
{
    public static string ToZydisString(this MandatoryPrefix value)
    {
        return value switch
        {
            MandatoryPrefix.None or MandatoryPrefix.Ignore => "NONE",
            MandatoryPrefix.P66 => "66",
            MandatoryPrefix.PF2 => "F2",
            MandatoryPrefix.PF3 => "F3",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

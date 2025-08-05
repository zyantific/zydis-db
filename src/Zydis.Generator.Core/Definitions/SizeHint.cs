using System;

namespace Zydis.Generator.Core.Definitions;

public enum SizeHint
{
    None,
    AddressSize,
    OperandSize,
}

public static class SizeHintExtensions
{
    public static string ToZydisString(this SizeHint value)
    {
        return value switch
        {
            SizeHint.None => "NONE",
            SizeHint.AddressSize => "ASZ",
            SizeHint.OperandSize => "OSZ",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

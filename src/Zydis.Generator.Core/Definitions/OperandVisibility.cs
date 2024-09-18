using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<OperandVisibility>))]
public enum OperandVisibility
{
    /// <summary>
    /// The operand is visible and represented in the encoded instruction form.
    /// </summary>
    Explicit,

    /// <summary>
    /// The operand is visible but not represented in the encoded instruction form.
    /// </summary>
    Implicit,

    /// <summary>
    /// The operand is hidden. This implies that it is as well not represented in the encoded
    /// instruction form.
    /// </summary>
    Hidden
}

public static class OperandVisibilityExtensions
{
    public static string ToZydisString(this OperandVisibility value)
    {
        return value switch
        {
            OperandVisibility.Explicit => "EXPLICIT",
            OperandVisibility.Implicit => "IMPLICIT",
            OperandVisibility.Hidden => "HIDDEN",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1720 // Identifier contains type name
#pragma warning disable CA1707 // Identifier contains underscores

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<ElementType>))]
public enum ElementType
{
    INVALID,
    VARIABLE,
    STRUCT,
    INT,
    UINT,
    INT1,
    INT8,
    INT8X4,
    INT16,
    INT16X2,
    INT32,
    INT64,
    INT128,
    UINT8,
    UINT8X4,
    UINT16,
    UINT16X2,
    UINT32,
    UINT64,
    UINT128,
    UINT256,
    FLOAT16,
    FLOAT16X2,
    FLOAT32,
    FLOAT64,
    FLOAT80,
    BFLOAT16X2,
    BCD80,
    CC3,
    CC5
}

// ReSharper restore InconsistentNaming

#pragma warning restore CA1707 // Identifier contains underscores
#pragma warning restore CA1720 // Identifier contains type name

public static class ElementTypeExtensions
{
    public static string ToZydisString(this ElementType value)
    {
        return value switch
        {
            ElementType.INVALID => "INVALID",
            ElementType.VARIABLE => "VARIABLE",
            ElementType.STRUCT => "STRUCT",
            ElementType.INT => "INT",
            ElementType.UINT => "UINT",
            ElementType.INT1 => "INT1",
            ElementType.INT8 => "INT8",
            ElementType.INT8X4 => "INT8X4",
            ElementType.INT16 => "INT16",
            ElementType.INT16X2 => "INT16X2",
            ElementType.INT32 => "INT32",
            ElementType.INT64 => "INT64",
            ElementType.INT128 => "INT128",
            ElementType.UINT8 => "UINT8",
            ElementType.UINT8X4 => "UINT8X4",
            ElementType.UINT16 => "UINT16",
            ElementType.UINT16X2 => "UINT16X2",
            ElementType.UINT32 => "UINT32",
            ElementType.UINT64 => "UINT64",
            ElementType.UINT128 => "UINT128",
            ElementType.UINT256 => "UINT256",
            ElementType.FLOAT16 => "FLOAT16",
            ElementType.FLOAT16X2 => "FLOAT16X2",
            ElementType.FLOAT32 => "FLOAT32",
            ElementType.FLOAT64 => "FLOAT64",
            ElementType.FLOAT80 => "FLOAT80",
            ElementType.BFLOAT16X2 => "BFLOAT16X2",
            ElementType.BCD80 => "BCD80",
            ElementType.CC3 => "CC3",
            ElementType.CC5 => "CC5",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

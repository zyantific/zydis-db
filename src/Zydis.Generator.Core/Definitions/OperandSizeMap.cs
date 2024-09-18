using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<OperandSizeMap>))]
public enum OperandSizeMap
{
    [JsonStringEnumMemberName("default"        )] Default,
    [JsonStringEnumMemberName("byteop"         )] ByteOperation,
    [JsonStringEnumMemberName("ignore66"       )] Ignore66,
    [JsonStringEnumMemberName("rexw32"         )] RexW32,
    [JsonStringEnumMemberName("default64"      )] Default64,
    [JsonStringEnumMemberName("force64"        )] Force64,
    [JsonStringEnumMemberName("force32_or_rexw")] Force32OrRexW,
    [JsonStringEnumMemberName("force32_or_64"  )] Force32Or64
}

public static class OperandSizeMapExtensions
{
    public static string ToZydisString(this OperandSizeMap value)
    {
        return value switch
        {
            OperandSizeMap.Default => "ZydisOperandSizeMap_Default",
            OperandSizeMap.ByteOperation => "ZydisOperandSizeMap_ByteOperation",
            OperandSizeMap.Ignore66 => "ZydisOperandSizeMap_Ignore66",
            OperandSizeMap.RexW32 => "ZydisOperandSizeMap_RexW32",
            OperandSizeMap.Default64 => "ZydisOperandSizeMap_Default64",
            OperandSizeMap.Force64 => "ZydisOperandSizeMap_Force64",
            OperandSizeMap.Force32OrRexW => "ZydisOperandSizeMap_Force32OrRexW",
            OperandSizeMap.Force32Or64 => "ZydisOperandSizeMap_Force32Or64",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

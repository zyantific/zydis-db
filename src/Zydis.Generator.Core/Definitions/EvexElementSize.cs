using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<EvexElementSize>))]
public enum EvexElementSize
{
    [JsonStringEnumMemberName("invalid")]
    Invalid,

    [JsonStringEnumMemberName("8")]
    E8,

    [JsonStringEnumMemberName("16")]
    E16,

    [JsonStringEnumMemberName("32")]
    E32,

    [JsonStringEnumMemberName("64")]
    E64,

    [JsonStringEnumMemberName("128")]
    E128
}

public static class EvexElementSizeExtensions
{
    public static string ToZydisString(this EvexElementSize value)
    {
        return value switch
        {
            EvexElementSize.Invalid => "INVALID",
            EvexElementSize.E8 => "8",
            EvexElementSize.E16 => "16",
            EvexElementSize.E32 => "32",
            EvexElementSize.E64 => "64",
            EvexElementSize.E128 => "128",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

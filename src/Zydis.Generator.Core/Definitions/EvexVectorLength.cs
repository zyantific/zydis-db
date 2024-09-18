using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<EvexVectorLength>))]
public enum EvexVectorLength
{
    [JsonStringEnumMemberName("default")]
    Default,

    [JsonStringEnumMemberName("128")]
    V128,

    [JsonStringEnumMemberName("256")]
    V256,

    [JsonStringEnumMemberName("512")]
    V512
}

public static class EvexVectorLengthExtensions
{
    public static string ToZydisString(this EvexVectorLength value)
    {
        return value switch
        {
            EvexVectorLength.Default => "DEFAULT",
            EvexVectorLength.V128 => "FIXED_128",
            EvexVectorLength.V256 => "FIXED_256",
            EvexVectorLength.V512 => "FIXED_512",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<VectorLength>))]
public enum VectorLength
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

public static class VectorLengthExtensions
{
    public static string ToZydisString(this VectorLength value)
    {
        return value switch
        {
            VectorLength.Default => "DEFAULT",
            VectorLength.V128 => "FIXED_128",
            VectorLength.V256 => "FIXED_256",
            VectorLength.V512 => "FIXED_512",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToZydisEncoderString(this VectorLength value)
    {
        var str = value.ToString("G");
        if (!str.StartsWith('V'))
        {
            return "INVALID";
        }
        return str[1..];
    }
}

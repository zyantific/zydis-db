using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<MaskMode>))]
public enum MaskMode
{
    [JsonStringEnumMemberName("invalid")]
    Invalid,

    [JsonStringEnumMemberName("allowed")]
    Allowed,

    [JsonStringEnumMemberName("required")]
    Required,

    [JsonStringEnumMemberName("forbidden")]
    Forbidden
}

public static class MaskModeExtensions
{
    public static string ToZydisString(this MaskMode value)
    {
        return value switch
        {
            MaskMode.Invalid => "INVALID",
            MaskMode.Allowed => "ALLOWED",
            MaskMode.Required => "REQUIRED",
            MaskMode.Forbidden => "FORBIDDEN",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

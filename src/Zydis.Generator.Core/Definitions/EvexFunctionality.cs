using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<EvexFunctionality>))]
public enum EvexFunctionality
{
    [JsonStringEnumMemberName("invalid")]
    Invalid,

    [JsonStringEnumMemberName("bc")]
    BC,

    [JsonStringEnumMemberName("rc")]
    RC,

    [JsonStringEnumMemberName("sae")]
    SAE,
}

public static class EvexFunctionalityExtensions
{
    public static string ToZydisString(this EvexFunctionality value)
    {
        return value switch
        {
            EvexFunctionality.Invalid => "INVALID",
            EvexFunctionality.BC => "BC",
            EvexFunctionality.RC => "RC",
            EvexFunctionality.SAE => "SAE",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

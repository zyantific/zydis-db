using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Common;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<OpcodeMap>))]
public enum OpcodeMap
{
    // ReSharper disable InconsistentNaming

    [JsonStringEnumMemberName("default")]
    MAP0,

    [JsonStringEnumMemberName("0f")]
    M0F,

    [JsonStringEnumMemberName("0f38")]
    M0F38,

    [JsonStringEnumMemberName("0f3a")]
    M0F3A,

    [JsonStringEnumMemberName("map4")]
    MAP4,

    [JsonStringEnumMemberName("map5")]
    MAP5,

    [JsonStringEnumMemberName("map6")]
    MAP6,

    [JsonStringEnumMemberName("map7")]
    MAP7,

    [JsonStringEnumMemberName("0f0f")]
    M0F0F, // 3DNow!

    [JsonStringEnumMemberName("xop8")]
    XOP8,

    [JsonStringEnumMemberName("xop9")]
    XOP9,

    [JsonStringEnumMemberName("xopa")]
    XOPA

    // ReSharper restore InconsistentNaming
}

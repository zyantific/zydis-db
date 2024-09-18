using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Common;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<RefiningPrefix>))]
public enum RefiningPrefix
{
    // ReSharper disable InconsistentNaming

    [JsonStringEnumMemberName("ignore")] // TODO: Remove
    PIgnore,

    [JsonStringEnumMemberName("none")]
    PNP,

    [JsonStringEnumMemberName("66")]
    P66,

    [JsonStringEnumMemberName("f3")]
    PF3,

    [JsonStringEnumMemberName("f2")]
    PF2

    // ReSharper restore InconsistentNaming
}

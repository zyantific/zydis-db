using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<AddressSizeMap>))]
public enum AddressSizeMap
{
    [JsonStringEnumMemberName("default"      )] Default,
    [JsonStringEnumMemberName("ignored"      )] Ignored,
    [JsonStringEnumMemberName("force32_or_64")] Force32Or64
}

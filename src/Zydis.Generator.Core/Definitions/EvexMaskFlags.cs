using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(StringFlagsConverterFactory<EvexMaskFlags>))]
[Flags]
public enum EvexMaskFlags
{
    [JsonStringEnumMemberName("none")]
    None = 0,

    [JsonStringEnumMemberName("is_control_mask")]
    IsControlMask = 1 << 1,

    [JsonStringEnumMemberName("accepts_zero_mask")]
    AcceptsZeroMask = 1 << 2,

    [JsonStringEnumMemberName("force_zero_mask")]
    ForceZeroMask = 1 << 3,
}

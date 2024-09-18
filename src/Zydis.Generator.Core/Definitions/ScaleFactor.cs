using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<ScaleFactor>))]
public enum ScaleFactor
{
    Static,
    [JsonStringEnumMemberName("osz")] ScaleOSZ,
    [JsonStringEnumMemberName("asz")] ScaleASZ,
    [JsonStringEnumMemberName("ssz")] ScaleSSZ
}

// ReSharper restore InconsistentNaming

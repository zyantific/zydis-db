using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<SegmentRegister>))]
public enum SegmentRegister
{
    None,
    ES,
    CS,
    SS,
    DS,
    FS,
    GS
}

// ReSharper restore InconsistentNaming

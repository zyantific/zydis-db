using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Common;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<InstructionEncoding>))]
public enum InstructionEncoding
{
    // ReSharper disable InconsistentNaming

    [JsonStringEnumMemberName("default")]
    Default,

    [JsonStringEnumMemberName("vex")]
    VEX,

    [JsonStringEnumMemberName("evex")]
    EVEX,

    [JsonStringEnumMemberName("mvex")]
    MVEX,

    [JsonStringEnumMemberName("xop")]
    XOP,

    [JsonStringEnumMemberName("3dnow")]
    AMD3DNOW

    // ReSharper restore InconsistentNaming
}

public static class InstructionEncodingExtensions
{
    public static string ToZydisString(this InstructionEncoding encoding)
    {
        return "ZYDIS_INSTRUCTION_ENCODING_" + encoding switch
        {
            InstructionEncoding.Default => "LEGACY",
            InstructionEncoding.VEX => "VEX",
            InstructionEncoding.EVEX => "EVEX",
            InstructionEncoding.MVEX => "MVEX",
            InstructionEncoding.XOP => "XOP",
            InstructionEncoding.AMD3DNOW => "3DNOW",
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
        };
    }
}

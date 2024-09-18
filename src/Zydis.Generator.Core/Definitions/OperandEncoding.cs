using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1707 // Identifier contains underscores

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<OperandEncoding>))]
public enum OperandEncoding
{
    None,
    Opcode,
    ModrmReg,
    ModrmRm,
    OpcodeBits,
    NDSNDD,       // VEX/EVEX/MVEX.VVVV
    IS4,
    MASK,         //     EVEX/MVEX.AAA
    Disp8,
    Disp16,
    Disp32,
    Disp64,

    Disp16_32_64, // DISPv
    Disp32_32_64, // DISPy
    Disp16_32_32, // DISPz
    Uimm8,
    Uimm16,
    Uimm32,
    Uimm64,
    Uimm16_32_64, // UIMMv
    Uimm32_32_64, // UIMMy
    Uimm16_32_32, // UIMMz
    Simm8,
    Simm16,
    Simm32,
    Simm64,
    Simm16_32_64, // SIMMv
    Simm32_32_64, // SIMMy
    Simm16_32_32, // SIMMz
    Jimm8,
    Jimm16,
    Jimm32,
    Jimm64,
    Jimm16_32_64, // JImmv
    Jimm32_32_64, // JImmy
    Jimm16_32_32  // JImmz
}

// ReSharper restore InconsistentNaming

#pragma warning restore CA1707 // Identifier contains underscores

public static class OperandEncodingExtensions
{
    public static string ToZydisString(this OperandEncoding value)
    {
        return value switch
        {
            OperandEncoding.None => "NONE",
            OperandEncoding.Opcode => "OPCODE",
            OperandEncoding.ModrmReg => "MODRM_REG",
            OperandEncoding.ModrmRm => "MODRM_RM",
            OperandEncoding.OpcodeBits => "OPCODE",
            OperandEncoding.NDSNDD => "NDSNDD",
            OperandEncoding.IS4 => "IS4",
            OperandEncoding.MASK => "MASK",
            OperandEncoding.Disp8 => "DISP8",
            OperandEncoding.Disp16 => "DISP16",
            OperandEncoding.Disp32 => "DISP32",
            OperandEncoding.Disp64 => "DISP64",
            OperandEncoding.Disp16_32_64 => "DISP16_32_64",
            OperandEncoding.Disp32_32_64 => "DISP32_32_64",
            OperandEncoding.Disp16_32_32 => "DISP16_32_32",
            OperandEncoding.Uimm8 => "UIMM8",
            OperandEncoding.Uimm16 => "UIMM16",
            OperandEncoding.Uimm32 => "UIMM32",
            OperandEncoding.Uimm64 => "UIMM64",
            OperandEncoding.Uimm16_32_64 => "UIMM16_32_64",
            OperandEncoding.Uimm32_32_64 => "UIMM32_32_64",
            OperandEncoding.Uimm16_32_32 => "UIMM16_32_32",
            OperandEncoding.Simm8 => "SIMM8",
            OperandEncoding.Simm16 => "SIMM16",
            OperandEncoding.Simm32 => "SIMM32",
            OperandEncoding.Simm64 => "SIMM64",
            OperandEncoding.Simm16_32_64 => "SIMM16_32_64",
            OperandEncoding.Simm32_32_64 => "SIMM32_32_64",
            OperandEncoding.Simm16_32_32 => "SIMM16_32_32",
            OperandEncoding.Jimm8 => "JIMM8",
            OperandEncoding.Jimm16 => "JIMM16",
            OperandEncoding.Jimm32 => "JIMM32",
            OperandEncoding.Jimm64 => "JIMM64",
            OperandEncoding.Jimm16_32_64 => "JIMM16_32_64",
            OperandEncoding.Jimm32_32_64 => "JIMM32_32_64",
            OperandEncoding.Jimm16_32_32 => "JIMM16_32_32",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

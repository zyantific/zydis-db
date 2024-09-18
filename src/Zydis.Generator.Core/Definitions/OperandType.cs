using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1720 // Identifier contains type name
#pragma warning disable CA1707 // Identifier contains underscores

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<OperandType>))]
public enum OperandType
{
    Invalid,
    ImplicitReg,
    ImplicitMem,
    ImplicitImm1,
    GPR8,
    GPR16,
    GPR32,
    GPR64,
    GPR16_32_64, // GPRv
    GPR32_32_64, // GPRy
    GPR16_32_32, // GPRz
    [JsonStringEnumMemberName("gpr_asz")] GPRASZ,
    FPR,
    MMX,
    XMM,
    YMM,
    ZMM,
    TMM,
    BND,
    SREG,
    CR,
    DR,
    MASK,
    MEM,
    [JsonStringEnumMemberName("mem_vsibx")] MEMVSIBX,
    [JsonStringEnumMemberName("mem_vsiby")] MEMVSIBY,
    [JsonStringEnumMemberName("mem_vsibz")] MEMVSIBZ,
    IMM,
    REL,
    ABS,

    PTR,

    AGEN,
    [JsonStringEnumMemberName("agen_norel")] AGENNoRel, // RIP rel invalid
    MOFFS,
    MIB,          // MPX Memory Operand
    DFV
}

// ReSharper restore InconsistentNaming

#pragma warning restore CA1707 // Identifier contains underscores
#pragma warning restore CA1720 // Identifier contains type name

public static class OperandTypeExtensions
{
    public static string ToZydisString(this OperandType value)
    {
        return value switch
        {
            OperandType.Invalid => "INVALID",
            OperandType.ImplicitReg => "IMPLICIT_REG",
            OperandType.ImplicitMem => "IMPLICIT_MEM",
            OperandType.ImplicitImm1 => "IMPLICIT_IMM1",
            OperandType.GPR8 => "GPR8",
            OperandType.GPR16 => "GPR16",
            OperandType.GPR32 => "GPR32",
            OperandType.GPR64 => "GPR64",
            OperandType.GPR16_32_64 => "GPR16_32_64",
            OperandType.GPR32_32_64 => "GPR32_32_64",
            OperandType.GPR16_32_32 => "GPR16_32_32",
            OperandType.GPRASZ => "GPR_ASZ",
            OperandType.FPR => "FPR",
            OperandType.MMX => "MMX",
            OperandType.XMM => "XMM",
            OperandType.YMM => "YMM",
            OperandType.ZMM => "ZMM",
            OperandType.TMM => "TMM",
            OperandType.BND => "BND",
            OperandType.SREG => "SREG",
            OperandType.CR => "CR",
            OperandType.DR => "DR",
            OperandType.MASK => "MASK",
            OperandType.MEM => "MEM",
            OperandType.MEMVSIBX => "MEM_VSIBX",
            OperandType.MEMVSIBY => "MEM_VSIBY",
            OperandType.MEMVSIBZ => "MEM_VSIBZ",
            OperandType.IMM => "IMM",
            OperandType.REL => "REL",
            OperandType.ABS => "ABS",
            OperandType.PTR => "PTR",
            OperandType.AGEN => "AGEN",
            OperandType.AGENNoRel => "AGEN", // TODO: fixme
            OperandType.MOFFS => "MOFFS",
            OperandType.MIB => "MIB",
            OperandType.DFV => "DFV",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

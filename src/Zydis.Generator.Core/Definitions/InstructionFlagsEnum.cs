using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(StringFlagsConverterFactory<InstructionFlagsEnum>))]
[Flags]
public enum InstructionFlagsEnum
{
    None                = 0,
    [JsonStringEnumMemberName("ignore_modrm_mod")] ForceRegForm        = 1 <<  1,   // Forces the instruction to always use "reg, reg" form (modrm.mod = 3)
    ProtectedMode       = 1 <<  2,   // Instruction is invalid in real and 8086 mode
    NoCompatMode        = 1 <<  3,   // Instruction is invalid incompatibility mode

    [JsonStringEnumMemberName("short_branch")]
    IsShortBranch       = 1 <<  4,

    [JsonStringEnumMemberName("near_branch")]
    IsNearBranch        = 1 <<  5,

    [JsonStringEnumMemberName("far_branch")]
    IsFarBranch         = 1 <<  6,

    [JsonStringEnumMemberName("abs_branch")]
    IsAbsBranch         = 1 <<  7,

    [JsonStringEnumMemberName("cpu_state_cr")]
    StateCPU_CR         = 1 <<  8,

    [JsonStringEnumMemberName("cpu_state_cw")]
    StateCPU_CW         = 1 <<  9,

    [JsonStringEnumMemberName("fpu_state_cr")]
    StateFPU_CR         = 1 << 10,

    [JsonStringEnumMemberName("fpu_state_cw")]
    StateFPU_CW         = 1 << 11,

    [JsonStringEnumMemberName("xmm_state_cr")]
    StateXMM_CR         = 1 << 12,

    [JsonStringEnumMemberName("xmm_state_cw")]
    StateXMM_CW         = 1 << 13,

    NoSourceDestMatch   = 1 << 14, // UD if the dst register matches any of the source regs
    NoSourceSourceMatch = 1 << 15, // AMX-E4
    IsGather            = 1 << 16
}

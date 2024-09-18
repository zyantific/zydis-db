using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<Register>))]
public enum Register
{
    [JsonStringEnumMemberName("none")]
    None,

    // General purpose registers 8-bit
    [JsonStringEnumMemberName("al")]
    AL,

    [JsonStringEnumMemberName("cl")]
    CL,

    [JsonStringEnumMemberName("dl")]
    DL,

    [JsonStringEnumMemberName("bl")]
    BL,

    [JsonStringEnumMemberName("ah")]
    AH,

    [JsonStringEnumMemberName("ch")]
    CH,

    [JsonStringEnumMemberName("dh")]
    DH,

    [JsonStringEnumMemberName("bh")]
    BH,

    [JsonStringEnumMemberName("spl")]
    SPL,

    [JsonStringEnumMemberName("bpl")]
    BPL,

    [JsonStringEnumMemberName("sil")]
    SIL,

    [JsonStringEnumMemberName("dil")]
    DIL,

    [JsonStringEnumMemberName("r8b")]
    R8B,

    [JsonStringEnumMemberName("r9b")]
    R9B,

    [JsonStringEnumMemberName("r10b")]
    R10B,

    [JsonStringEnumMemberName("r11b")]
    R11B,

    [JsonStringEnumMemberName("r12b")]
    R12B,

    [JsonStringEnumMemberName("r13b")]
    R13B,

    [JsonStringEnumMemberName("r14b")]
    R14B,

    [JsonStringEnumMemberName("r15b")]
    R15B,

    // General purpose registers 16-bit
    [JsonStringEnumMemberName("ax")]
    AX,

    [JsonStringEnumMemberName("cx")]
    CX,

    [JsonStringEnumMemberName("dx")]
    DX,

    [JsonStringEnumMemberName("bx")]
    BX,

    [JsonStringEnumMemberName("sp")]
    SP,

    [JsonStringEnumMemberName("bp")]
    BP,

    [JsonStringEnumMemberName("si")]
    SI,

    [JsonStringEnumMemberName("di")]
    DI,

    [JsonStringEnumMemberName("r8w")]
    R8W,

    [JsonStringEnumMemberName("r9w")]
    R9W,

    [JsonStringEnumMemberName("r10w")]
    R10W,

    [JsonStringEnumMemberName("r11w")]
    R11W,

    [JsonStringEnumMemberName("r12w")]
    R12W,

    [JsonStringEnumMemberName("r13w")]
    R13W,

    [JsonStringEnumMemberName("r14w")]
    R14W,

    [JsonStringEnumMemberName("r15w")]
    R15W,

    // General purpose registers 32-bit
    [JsonStringEnumMemberName("eax")]
    EAX,

    [JsonStringEnumMemberName("ecx")]
    ECX,

    [JsonStringEnumMemberName("edx")]
    EDX,

    [JsonStringEnumMemberName("ebx")]
    EBX,

    [JsonStringEnumMemberName("esp")]
    ESP,

    [JsonStringEnumMemberName("ebp")]
    EBP,

    [JsonStringEnumMemberName("esi")]
    ESI,

    [JsonStringEnumMemberName("edi")]
    EDI,

    [JsonStringEnumMemberName("e8d")]
    E8D,

    [JsonStringEnumMemberName("r9d")]
    R9D,

    [JsonStringEnumMemberName("r10d")]
    R10D,

    [JsonStringEnumMemberName("r11d")]
    R11D,

    [JsonStringEnumMemberName("r12d")]
    R12D,

    [JsonStringEnumMemberName("r13d")]
    R13D,

    [JsonStringEnumMemberName("r14d")]
    R14D,

    [JsonStringEnumMemberName("r15d")]
    R15D,

    // General purpose registers 64-bit
    [JsonStringEnumMemberName("rax")]
    RAX,

    [JsonStringEnumMemberName("rcx")]
    RCX,

    [JsonStringEnumMemberName("rdx")]
    RDX,

    [JsonStringEnumMemberName("rbx")]
    RBX,

    [JsonStringEnumMemberName("rsp")]
    RSP,

    [JsonStringEnumMemberName("rbp")]
    RBP,

    [JsonStringEnumMemberName("rsi")]
    RSI,

    [JsonStringEnumMemberName("rdi")]
    RDI,

    [JsonStringEnumMemberName("r8")]
    R8,

    [JsonStringEnumMemberName("r9")]
    R9,

    [JsonStringEnumMemberName("r10")]
    R10,

    [JsonStringEnumMemberName("r11")]
    R11,

    [JsonStringEnumMemberName("r12")]
    R12,

    [JsonStringEnumMemberName("r13")]
    R13,

    [JsonStringEnumMemberName("r14")]
    R14,

    [JsonStringEnumMemberName("r15")]
    R15,

    // Operand-size scaling general purpose pseudo-registers
    [JsonStringEnumMemberName("osz_ax")]
    OSZAX,

    [JsonStringEnumMemberName("osz_cx")]
    OSZCX,

    [JsonStringEnumMemberName("osz_dx")]
    OSZDX,

    [JsonStringEnumMemberName("osz_bx")]
    OSZBX,

    [JsonStringEnumMemberName("osz_sp")]
    OSZSP,

    [JsonStringEnumMemberName("osz_bp")]
    OSZBP,

    [JsonStringEnumMemberName("osz_si")]
    OSZSI,

    [JsonStringEnumMemberName("osz_di")]
    OSZDI,

    // Address-size scaling general purpose pseudo-registers
    [JsonStringEnumMemberName("asz_ax")]
    ASZAX,

    [JsonStringEnumMemberName("asz_cx")]
    ASZCX,

    [JsonStringEnumMemberName("asz_dx")]
    ASZDX,

    [JsonStringEnumMemberName("asz_bx")]
    ASZBX,

    [JsonStringEnumMemberName("asz_sp")]
    ASZSP,

    [JsonStringEnumMemberName("asz_bp")]
    ASZBP,

    [JsonStringEnumMemberName("asz_si")]
    ASZSI,

    [JsonStringEnumMemberName("asz_di")]
    ASZDI,

    // Stack-size scaling general purpose pseudo-registers
    [JsonStringEnumMemberName("ssz_ax")]
    SSZAX,

    [JsonStringEnumMemberName("ssz_cx")]
    SSZCX,

    [JsonStringEnumMemberName("ssz_dx")]
    SSZDX,

    [JsonStringEnumMemberName("ssz_bx")]
    SSZBX,

    [JsonStringEnumMemberName("ssz_sp")]
    SSZSP,

    [JsonStringEnumMemberName("ssz_bp")]
    SSZBP,

    [JsonStringEnumMemberName("ssz_si")]
    SSZSI,

    [JsonStringEnumMemberName("ssz_di")]
    SSZDI,

    // Address-size scaling misc pseudo-registers
    [JsonStringEnumMemberName("asz_ip")]
    ASZIP,

    // Stack-size scaling misc pseudo-registers
    [JsonStringEnumMemberName("ssz_ip")]
    SSZIP,

    [JsonStringEnumMemberName("ssz_flags")]
    SSZFLAGS,

    // Floating point legacy registers
    [JsonStringEnumMemberName("st0")]
    ST0,

    [JsonStringEnumMemberName("st1")]
    ST1,

    [JsonStringEnumMemberName("st2")]
    ST2,

    [JsonStringEnumMemberName("st3")]
    ST3,

    [JsonStringEnumMemberName("st4")]
    ST4,

    [JsonStringEnumMemberName("st5")]
    ST5,

    [JsonStringEnumMemberName("st6")]
    ST6,

    [JsonStringEnumMemberName("st7")]
    ST7,

    [JsonStringEnumMemberName("x87control")]
    X87CONTROL,

    [JsonStringEnumMemberName("x87status")]
    regX87STATUS,

    [JsonStringEnumMemberName("x87tag")]
    regX87TAG,

    // Floating point multimedia registers
    [JsonStringEnumMemberName("mm0")]
    MM0,

    [JsonStringEnumMemberName("mm1")]
    MM1,

    [JsonStringEnumMemberName("mm2")]
    MM2,

    [JsonStringEnumMemberName("mm3")]
    MM3,

    [JsonStringEnumMemberName("mm4")]
    MM4,

    [JsonStringEnumMemberName("mm5")]
    MM5,

    [JsonStringEnumMemberName("mm6")]
    MM6,

    [JsonStringEnumMemberName("mm7")]
    MM7,

    // Floating point vector registers 128-bit
    [JsonStringEnumMemberName("xmm0")]
    XMM0,

    [JsonStringEnumMemberName("xmm1")]
    XMM1,

    [JsonStringEnumMemberName("xmm2")]
    XMM2,

    [JsonStringEnumMemberName("xmm3")]
    XMM3,

    [JsonStringEnumMemberName("xmm4")]
    XMM4,

    [JsonStringEnumMemberName("xmm5")]
    XMM5,

    [JsonStringEnumMemberName("xmm6")]
    XMM6,

    [JsonStringEnumMemberName("xmm7")]
    XMM7,

    [JsonStringEnumMemberName("xmm8")]
    XMM8,

    [JsonStringEnumMemberName("xmm9")]
    XMM9,

    [JsonStringEnumMemberName("xmm10")]
    XMM10,

    [JsonStringEnumMemberName("xmm11")]
    XMM11,

    [JsonStringEnumMemberName("xmm12")]
    XMM12,

    [JsonStringEnumMemberName("xmm13")]
    XMM13,

    [JsonStringEnumMemberName("xmm14")]
    XMM14,

    [JsonStringEnumMemberName("xmm15")]
    XMM15,

    [JsonStringEnumMemberName("xmm16")]
    XMM16,

    [JsonStringEnumMemberName("xmm17")]
    XMM17,

    [JsonStringEnumMemberName("xmm18")]
    XMM18,

    [JsonStringEnumMemberName("xmm19")]
    XMM19,

    [JsonStringEnumMemberName("xmm20")]
    XMM20,

    [JsonStringEnumMemberName("xmm21")]
    XMM21,

    [JsonStringEnumMemberName("xmm22")]
    XMM22,

    [JsonStringEnumMemberName("xmm23")]
    XMM23,

    [JsonStringEnumMemberName("xmm24")]
    XMM24,

    [JsonStringEnumMemberName("xmm25")]
    XMM25,

    [JsonStringEnumMemberName("xmm26")]
    XMM26,

    [JsonStringEnumMemberName("xmm27")]
    XMM27,

    [JsonStringEnumMemberName("xmm28")]
    XMM28,

    [JsonStringEnumMemberName("xmm29")]
    XMM29,

    [JsonStringEnumMemberName("xmm30")]
    XMM30,

    [JsonStringEnumMemberName("xmm31")]
    XMM31,

    // Floating point vector registers 256-bit
    [JsonStringEnumMemberName("ymm0")]
    YMM0,

    [JsonStringEnumMemberName("ymm1")]
    YMM1,

    [JsonStringEnumMemberName("ymm2")]
    YMM2,

    [JsonStringEnumMemberName("ymm3")]
    YMM3,

    [JsonStringEnumMemberName("ymm4")]
    YMM4,

    [JsonStringEnumMemberName("ymm5")]
    YMM5,

    [JsonStringEnumMemberName("ymm6")]
    YMM6,

    [JsonStringEnumMemberName("ymm7")]
    YMM7,

    [JsonStringEnumMemberName("ymm8")]
    YMM8,

    [JsonStringEnumMemberName("ymm9")]
    YMM9,

    [JsonStringEnumMemberName("ymm10")]
    YMM10,

    [JsonStringEnumMemberName("ymm11")]
    YMM11,

    [JsonStringEnumMemberName("ymm12")]
    YMM12,

    [JsonStringEnumMemberName("ymm13")]
    YMM13,

    [JsonStringEnumMemberName("ymm14")]
    YMM14,

    [JsonStringEnumMemberName("ymm15")]
    YMM15,

    [JsonStringEnumMemberName("ymm16")]
    YMM16,

    [JsonStringEnumMemberName("ymm17")]
    YMM17,

    [JsonStringEnumMemberName("ymm18")]
    YMM18,

    [JsonStringEnumMemberName("ymm19")]
    YMM19,

    [JsonStringEnumMemberName("ymm20")]
    YMM20,

    [JsonStringEnumMemberName("ymm21")]
    YMM21,

    [JsonStringEnumMemberName("ymm22")]
    YMM22,

    [JsonStringEnumMemberName("ymm23")]
    YMM23,

    [JsonStringEnumMemberName("ymm24")]
    YMM24,

    [JsonStringEnumMemberName("ymm25")]
    YMM25,

    [JsonStringEnumMemberName("ymm26")]
    YMM26,

    [JsonStringEnumMemberName("ymm27")]
    YMM27,

    [JsonStringEnumMemberName("ymm28")]
    YMM28,

    [JsonStringEnumMemberName("ymm29")]
    YMM29,

    [JsonStringEnumMemberName("ymm30")]
    YMM30,

    [JsonStringEnumMemberName("ymm31")]
    YMM31,

    // Floating point vector registers 512-bit
    [JsonStringEnumMemberName("zmm0")]
    ZMM0,

    [JsonStringEnumMemberName("zmm1")]
    ZMM1,

    [JsonStringEnumMemberName("zmm2")]
    ZMM2,

    [JsonStringEnumMemberName("zmm3")]
    ZMM3,

    [JsonStringEnumMemberName("zmm4")]
    ZMM4,

    [JsonStringEnumMemberName("zmm5")]
    ZMM5,

    [JsonStringEnumMemberName("zmm6")]
    ZMM6,

    [JsonStringEnumMemberName("zmm7")]
    ZMM7,

    [JsonStringEnumMemberName("zmm8")]
    ZMM8,

    [JsonStringEnumMemberName("zmm9")]
    ZMM9,

    [JsonStringEnumMemberName("zmm10")]
    ZMM10,

    [JsonStringEnumMemberName("zmm11")]
    ZMM11,

    [JsonStringEnumMemberName("zmm12")]
    ZMM12,

    [JsonStringEnumMemberName("zmm13")]
    ZMM13,

    [JsonStringEnumMemberName("zmm14")]
    ZMM14,

    [JsonStringEnumMemberName("zmm15")]
    ZMM15,

    [JsonStringEnumMemberName("zmm16")]
    ZMM16,

    [JsonStringEnumMemberName("zmm17")]
    ZMM17,

    [JsonStringEnumMemberName("zmm18")]
    ZMM18,

    [JsonStringEnumMemberName("zmm19")]
    ZMM19,

    [JsonStringEnumMemberName("zmm20")]
    ZMM20,

    [JsonStringEnumMemberName("zmm21")]
    ZMM21,

    [JsonStringEnumMemberName("zmm22")]
    ZMM22,

    [JsonStringEnumMemberName("zmm23")]
    ZMM23,

    [JsonStringEnumMemberName("zmm24")]
    ZMM24,

    [JsonStringEnumMemberName("zmm25")]
    ZMM25,

    [JsonStringEnumMemberName("zmm26")]
    ZMM26,

    [JsonStringEnumMemberName("zmm27")]
    ZMM27,

    [JsonStringEnumMemberName("zmm28")]
    ZMM28,

    [JsonStringEnumMemberName("zmm29")]
    ZMM29,

    [JsonStringEnumMemberName("zmm30")]
    ZMM30,

    [JsonStringEnumMemberName("zmm31")]
    ZMM31,

    // Matrix registers
    [JsonStringEnumMemberName("tmm0")]
    TMM0,

    [JsonStringEnumMemberName("tmm1")]
    TMM1,

    [JsonStringEnumMemberName("tmm2")]
    TMM2,

    [JsonStringEnumMemberName("tmm3")]
    TMM3,

    [JsonStringEnumMemberName("tmm4")]
    TMM4,

    [JsonStringEnumMemberName("tmm5")]
    TMM5,

    [JsonStringEnumMemberName("tmm6")]
    TMM6,

    [JsonStringEnumMemberName("tmm7")]
    TMM7,

    // Flags registers
    [JsonStringEnumMemberName("flags")]
    FLAGS,

    [JsonStringEnumMemberName("eflags")]
    EFLAGS,

    [JsonStringEnumMemberName("rflags")]
    RFLAGS,

    // Instruction-pointer registers
    [JsonStringEnumMemberName("ip")]
    IP,

    [JsonStringEnumMemberName("eip")]
    EIP,

    [JsonStringEnumMemberName("rip")]
    RIP,

    // Segment registers
    [JsonStringEnumMemberName("es")]
    ES,

    [JsonStringEnumMemberName("ss")]
    SS,

    [JsonStringEnumMemberName("cs")]
    CS,

    [JsonStringEnumMemberName("ds")]
    DS,

    [JsonStringEnumMemberName("fs")]
    FS,

    [JsonStringEnumMemberName("gs")]
    GS,

    // Test registers
    [JsonStringEnumMemberName("tr0")]
    TR0,

    [JsonStringEnumMemberName("tr1")]
    TR1,

    [JsonStringEnumMemberName("tr2")]
    TR2,

    [JsonStringEnumMemberName("tr3")]
    TR3,

    [JsonStringEnumMemberName("tr4")]
    TR4,

    [JsonStringEnumMemberName("tr5")]
    TR5,

    [JsonStringEnumMemberName("tr6")]
    TR6,

    [JsonStringEnumMemberName("tr7")]
    TR7,

    // Control registers
    [JsonStringEnumMemberName("cr0")]
    CR0,

    [JsonStringEnumMemberName("cr1")]
    CR1,

    [JsonStringEnumMemberName("cr2")]
    CR2,

    [JsonStringEnumMemberName("cr3")]
    CR3,

    [JsonStringEnumMemberName("cr4")]
    CR4,

    [JsonStringEnumMemberName("cr5")]
    CR5,

    [JsonStringEnumMemberName("cr6")]
    CR6,

    [JsonStringEnumMemberName("cr7")]
    CR7,

    [JsonStringEnumMemberName("cr8")]
    CR8,

    [JsonStringEnumMemberName("cr9")]
    CR9,

    [JsonStringEnumMemberName("cr10")]
    CR10,

    [JsonStringEnumMemberName("cr11")]
    CR11,

    [JsonStringEnumMemberName("cr12")]
    CR12,

    [JsonStringEnumMemberName("cr13")]
    CR13,

    [JsonStringEnumMemberName("cr14")]
    CR14,

    [JsonStringEnumMemberName("cr15")]
    CR15,

    // Debug registers
    [JsonStringEnumMemberName("dr0")]
    DR0,

    [JsonStringEnumMemberName("dr1")]
    DR1,

    [JsonStringEnumMemberName("dr2")]
    DR2,

    [JsonStringEnumMemberName("dr3")]
    DR3,

    [JsonStringEnumMemberName("dr4")]
    DR4,

    [JsonStringEnumMemberName("dr5")]
    DR5,

    [JsonStringEnumMemberName("dr6")]
    DR6,

    [JsonStringEnumMemberName("dr7")]
    DR7,

    [JsonStringEnumMemberName("dr8")]
    DR8,

    [JsonStringEnumMemberName("dr9")]
    DR9,

    [JsonStringEnumMemberName("dr10")]
    DR10,

    [JsonStringEnumMemberName("dr11")]
    DR11,

    [JsonStringEnumMemberName("dr12")]
    DR12,

    [JsonStringEnumMemberName("dr13")]
    DR13,

    [JsonStringEnumMemberName("dr14")]
    DR14,

    [JsonStringEnumMemberName("dr15")]
    DR15,

    // Mask registers
    [JsonStringEnumMemberName("k0")]
    K0,

    [JsonStringEnumMemberName("k1")]
    K1,

    [JsonStringEnumMemberName("k2")]
    K2,

    [JsonStringEnumMemberName("k3")]
    K3,

    [JsonStringEnumMemberName("k4")]
    K4,

    [JsonStringEnumMemberName("k5")]
    K5,

    [JsonStringEnumMemberName("k6")]
    K6,

    [JsonStringEnumMemberName("k7")]
    K7,

    // Bound registers
    [JsonStringEnumMemberName("bnd0")]
    BND0,

    [JsonStringEnumMemberName("bnd1")]
    BND1,

    [JsonStringEnumMemberName("bnd2")]
    BND2,

    [JsonStringEnumMemberName("bnd3")]
    BND3,

    // DFV registers
    [JsonStringEnumMemberName("dfv0")]
    DFV0,

    [JsonStringEnumMemberName("dfv1")]
    DFV1,

    [JsonStringEnumMemberName("dfv2")]
    DFV2,

    [JsonStringEnumMemberName("dfv3")]
    DFV3,

    [JsonStringEnumMemberName("dfv4")]
    DFV4,

    [JsonStringEnumMemberName("dfv5")]
    DFV5,

    [JsonStringEnumMemberName("dfv6")]
    DFV6,

    [JsonStringEnumMemberName("dfv7")]
    DFV7,

    [JsonStringEnumMemberName("dfv8")]
    DFV8,

    [JsonStringEnumMemberName("dfv9")]
    DFV9,

    [JsonStringEnumMemberName("dfv10")]
    DFV10,

    [JsonStringEnumMemberName("dfv11")]
    DFV11,

    [JsonStringEnumMemberName("dfv12")]
    DFV12,

    [JsonStringEnumMemberName("dfv13")]
    DFV13,

    [JsonStringEnumMemberName("dfv14")]
    DFV14,

    [JsonStringEnumMemberName("dfv15")]
    DFV15,

    // Misc registers
    [JsonStringEnumMemberName("mxcsr")]
    MXCSR,

    [JsonStringEnumMemberName("pkru")]
    PKRU,

    [JsonStringEnumMemberName("xcr0")]
    XCR0,

    [JsonStringEnumMemberName("gdtr")]
    GDTR,

    [JsonStringEnumMemberName("ldtr")]
    LDTR,

    [JsonStringEnumMemberName("idtr")]
    IDTR,

    [JsonStringEnumMemberName("tr")]
    TR,

    [JsonStringEnumMemberName("bndcfg")]
    BNDCFG,

    [JsonStringEnumMemberName("bndstatus")]
    BNDSTATUS,

    [JsonStringEnumMemberName("uif")]
    regUIF,

    [JsonStringEnumMemberName("ia32_kernel_gs_base")]
    IA32KernelGSBase
}

public enum RegisterClass
{
    Invalid,
    GPR8,
    GPR16,
    GPR32,
    GPR64,
    GPROSZ,
    GPRASZ,
    GPRSSZ,
    X87,
    MMX,
    XMM,
    YMM,
    ZMM,
    TMM,
    FLAGS,
    IP,
    SEGMENT,
    TEST,
    CONTROL,
    DEBUG,
    MASK,
    BOUND,
    DFV
}

public enum RegisterKind
{
    INVALID,
    GPR,
    X87,
    MMX,
    VR,
    TMM,
    SEGMENT,
    TEST,
    CONTROL,
    DEBUG,
    MASK,
    BOUND,
    DFV
}

public static class RegisterExtensions
{
    private static FrozenDictionary<RegisterClass, (Register Lo, Register Hi)> RegisterMap = new
        Dictionary<RegisterClass, (Register Lo, Register Hi)>
        {
            { RegisterClass.Invalid, (Register.None, Register.None) },
            { RegisterClass.GPR8, (Register.AL, Register.R15B) },
            { RegisterClass.GPR16, (Register.AX, Register.R15W) },
            { RegisterClass.GPR32, (Register.EAX, Register.R15D) },
            { RegisterClass.GPR64, (Register.RAX, Register.R15) },
            { RegisterClass.GPROSZ, (Register.OSZAX, Register.OSZDI) },
            { RegisterClass.GPRASZ, (Register.ASZAX, Register.ASZDI) },
            { RegisterClass.GPRSSZ, (Register.SSZAX, Register.SSZDI) },
            { RegisterClass.X87, (Register.ST0, Register.ST7) },
            { RegisterClass.MMX, (Register.MM0, Register.MM7) },
            { RegisterClass.XMM, (Register.XMM0, Register.XMM31) },
            { RegisterClass.YMM, (Register.YMM0, Register.YMM31) },
            { RegisterClass.ZMM, (Register.ZMM0, Register.ZMM31) },
            { RegisterClass.TMM, (Register.TMM0, Register.TMM7) },
            { RegisterClass.FLAGS, (Register.FLAGS, Register.RFLAGS) },
            { RegisterClass.IP, (Register.IP, Register.RIP) },
            { RegisterClass.SEGMENT, (Register.ES, Register.GS) },
            { RegisterClass.TEST, (Register.TR0, Register.TR7) },
            { RegisterClass.CONTROL, (Register.CR0, Register.CR15) },
            { RegisterClass.DEBUG, (Register.DR0, Register.DR15) },
            { RegisterClass.MASK, (Register.K0, Register.K7) },
            { RegisterClass.BOUND, (Register.BND0, Register.BND3) },
            { RegisterClass.DFV, (Register.DFV0, Register.DFV15) }
        }.ToFrozenDictionary();

    public static RegisterClass GetRegisterClass(this Register value)
    {
        foreach (var (registerClass, bounds) in RegisterMap)
        {
            if (value >= bounds.Lo && value <= bounds.Hi)
            {
                return registerClass;
            }
        }

        return RegisterClass.Invalid;
    }

    public static int GetRegisterId(this Register value)
    {
        foreach (var (registerClass, bounds) in RegisterMap)
        {
            if (registerClass is RegisterClass.Invalid or RegisterClass.FLAGS or RegisterClass.IP)
            {
                continue;
            }

            if (value >= bounds.Lo && value <= bounds.Hi)
            {
                return (int)value - (int)bounds.Lo;
            }
        }

        return -1;
    }

    public static RegisterKind GetRegisterKind(this Register value)
    {
        var registerClass = value.GetRegisterClass();

        return registerClass switch
        {
            RegisterClass.GPR8 or
                RegisterClass.GPR16 or
                RegisterClass.GPR32 or
                RegisterClass.GPR64 or
                RegisterClass.GPROSZ or
                RegisterClass.GPRASZ or
                RegisterClass.GPRSSZ => RegisterKind.GPR,
            RegisterClass.X87 => RegisterKind.X87,
            RegisterClass.MMX => RegisterKind.MMX,
            RegisterClass.XMM or
                RegisterClass.YMM or
                RegisterClass.ZMM => RegisterKind.VR,
            RegisterClass.TMM => RegisterKind.TMM,
            RegisterClass.SEGMENT => RegisterKind.SEGMENT,
            RegisterClass.TEST => RegisterKind.TEST,
            RegisterClass.CONTROL => RegisterKind.CONTROL,
            RegisterClass.DEBUG => RegisterKind.DEBUG,
            RegisterClass.MASK => RegisterKind.MASK,
            RegisterClass.BOUND => RegisterKind.BOUND,
            RegisterClass.DFV => RegisterKind.DFV,
            _ => RegisterKind.INVALID
        };
    }

    public static string ToZydisString(this Register value)
    {
        return value switch
        {
            Register.None => "NONE",
            Register.AL => "AL",
            Register.CL => "CL",
            Register.DL => "DL",
            Register.BL => "BL",
            Register.AH => "AH",
            Register.CH => "CH",
            Register.DH => "DH",
            Register.BH => "BH",
            Register.SPL => "SPL",
            Register.BPL => "BPL",
            Register.SIL => "SIL",
            Register.DIL => "DIL",
            Register.R8B => "R8B",
            Register.R9B => "R9B",
            Register.R10B => "R10B",
            Register.R11B => "R11B",
            Register.R12B => "R12B",
            Register.R13B => "R13B",
            Register.R14B => "R14B",
            Register.R15B => "R15B",
            Register.AX => "AX",
            Register.CX => "CX",
            Register.DX => "DX",
            Register.BX => "BX",
            Register.SP => "SP",
            Register.BP => "BP",
            Register.SI => "SI",
            Register.DI => "DI",
            Register.R8W => "R8W",
            Register.R9W => "R9W",
            Register.R10W => "R10W",
            Register.R11W => "R11W",
            Register.R12W => "R12W",
            Register.R13W => "R13W",
            Register.R14W => "R14W",
            Register.R15W => "R15W",
            Register.EAX => "EAX",
            Register.ECX => "ECX",
            Register.EDX => "EDX",
            Register.EBX => "EBX",
            Register.ESP => "ESP",
            Register.EBP => "EBP",
            Register.ESI => "ESI",
            Register.EDI => "EDI",
            Register.E8D => "E8D",
            Register.R9D => "R9D",
            Register.R10D => "R10D",
            Register.R11D => "R11D",
            Register.R12D => "R12D",
            Register.R13D => "R13D",
            Register.R14D => "R14D",
            Register.R15D => "R15D",
            Register.RAX => "RAX",
            Register.RCX => "RCX",
            Register.RDX => "RDX",
            Register.RBX => "RBX",
            Register.RSP => "RSP",
            Register.RBP => "RBP",
            Register.RSI => "RSI",
            Register.RDI => "RDI",
            Register.R8 => "R8",
            Register.R9 => "R9",
            Register.R10 => "R10",
            Register.R11 => "R11",
            Register.R12 => "R12",
            Register.R13 => "R13",
            Register.R14 => "R14",
            Register.R15 => "R15",
            Register.OSZAX => "OSZ_AX",
            Register.OSZCX => "OSZ_CX",
            Register.OSZDX => "OSZ_DX",
            Register.OSZBX => "OSZ_BX",
            Register.OSZSP => "OSZ_SP",
            Register.OSZBP => "OSZ_BP",
            Register.OSZSI => "OSZ_SI",
            Register.OSZDI => "OSZ_DI",
            Register.ASZAX => "ASZ_AX",
            Register.ASZCX => "ASZ_CX",
            Register.ASZDX => "ASZ_DX",
            Register.ASZBX => "ASZ_BX",
            Register.ASZSP => "ASZ_SP",
            Register.ASZBP => "ASZ_BP",
            Register.ASZSI => "ASZ_SI",
            Register.ASZDI => "ASZ_DI",
            Register.SSZAX => "SSZ_AX",
            Register.SSZCX => "SSZ_CX",
            Register.SSZDX => "SSZ_DX",
            Register.SSZBX => "SSZ_BX",
            Register.SSZSP => "SSZ_SP",
            Register.SSZBP => "SSZ_BP",
            Register.SSZSI => "SSZ_SI",
            Register.SSZDI => "SSZ_DI",
            Register.ASZIP => "ASZ_IP",
            Register.SSZIP => "SSZ_IP",
            Register.SSZFLAGS => "SSZ_FLAGS",
            Register.ST0 => "ST0",
            Register.ST1 => "ST1",
            Register.ST2 => "ST2",
            Register.ST3 => "ST3",
            Register.ST4 => "ST4",
            Register.ST5 => "ST5",
            Register.ST6 => "ST6",
            Register.ST7 => "ST7",
            Register.X87CONTROL => "X87CONTROL",
            Register.regX87STATUS => "X87STATUS",
            Register.regX87TAG => "X87TAG",
            Register.MM0 => "MM0",
            Register.MM1 => "MM1",
            Register.MM2 => "MM2",
            Register.MM3 => "MM3",
            Register.MM4 => "MM4",
            Register.MM5 => "MM5",
            Register.MM6 => "MM6",
            Register.MM7 => "MM7",
            Register.XMM0 => "XMM0",
            Register.XMM1 => "XMM1",
            Register.XMM2 => "XMM2",
            Register.XMM3 => "XMM3",
            Register.XMM4 => "XMM4",
            Register.XMM5 => "XMM5",
            Register.XMM6 => "XMM6",
            Register.XMM7 => "XMM7",
            Register.XMM8 => "XMM8",
            Register.XMM9 => "XMM9",
            Register.XMM10 => "XMM10",
            Register.XMM11 => "XMM11",
            Register.XMM12 => "XMM12",
            Register.XMM13 => "XMM13",
            Register.XMM14 => "XMM14",
            Register.XMM15 => "XMM15",
            Register.XMM16 => "XMM16",
            Register.XMM17 => "XMM17",
            Register.XMM18 => "XMM18",
            Register.XMM19 => "XMM19",
            Register.XMM20 => "XMM20",
            Register.XMM21 => "XMM21",
            Register.XMM22 => "XMM22",
            Register.XMM23 => "XMM23",
            Register.XMM24 => "XMM24",
            Register.XMM25 => "XMM25",
            Register.XMM26 => "XMM26",
            Register.XMM27 => "XMM27",
            Register.XMM28 => "XMM28",
            Register.XMM29 => "XMM29",
            Register.XMM30 => "XMM30",
            Register.XMM31 => "XMM31",
            Register.YMM0 => "YMM0",
            Register.YMM1 => "YMM1",
            Register.YMM2 => "YMM2",
            Register.YMM3 => "YMM3",
            Register.YMM4 => "YMM4",
            Register.YMM5 => "YMM5",
            Register.YMM6 => "YMM6",
            Register.YMM7 => "YMM7",
            Register.YMM8 => "YMM8",
            Register.YMM9 => "YMM9",
            Register.YMM10 => "YMM10",
            Register.YMM11 => "YMM11",
            Register.YMM12 => "YMM12",
            Register.YMM13 => "YMM13",
            Register.YMM14 => "YMM14",
            Register.YMM15 => "YMM15",
            Register.YMM16 => "YMM16",
            Register.YMM17 => "YMM17",
            Register.YMM18 => "YMM18",
            Register.YMM19 => "YMM19",
            Register.YMM20 => "YMM20",
            Register.YMM21 => "YMM21",
            Register.YMM22 => "YMM22",
            Register.YMM23 => "YMM23",
            Register.YMM24 => "YMM24",
            Register.YMM25 => "YMM25",
            Register.YMM26 => "YMM26",
            Register.YMM27 => "YMM27",
            Register.YMM28 => "YMM28",
            Register.YMM29 => "YMM29",
            Register.YMM30 => "YMM30",
            Register.YMM31 => "YMM31",
            Register.ZMM0 => "ZMM0",
            Register.ZMM1 => "ZMM1",
            Register.ZMM2 => "ZMM2",
            Register.ZMM3 => "ZMM3",
            Register.ZMM4 => "ZMM4",
            Register.ZMM5 => "ZMM5",
            Register.ZMM6 => "ZMM6",
            Register.ZMM7 => "ZMM7",
            Register.ZMM8 => "ZMM8",
            Register.ZMM9 => "ZMM9",
            Register.ZMM10 => "ZMM10",
            Register.ZMM11 => "ZMM11",
            Register.ZMM12 => "ZMM12",
            Register.ZMM13 => "ZMM13",
            Register.ZMM14 => "ZMM14",
            Register.ZMM15 => "ZMM15",
            Register.ZMM16 => "ZMM16",
            Register.ZMM17 => "ZMM17",
            Register.ZMM18 => "ZMM18",
            Register.ZMM19 => "ZMM19",
            Register.ZMM20 => "ZMM20",
            Register.ZMM21 => "ZMM21",
            Register.ZMM22 => "ZMM22",
            Register.ZMM23 => "ZMM23",
            Register.ZMM24 => "ZMM24",
            Register.ZMM25 => "ZMM25",
            Register.ZMM26 => "ZMM26",
            Register.ZMM27 => "ZMM27",
            Register.ZMM28 => "ZMM28",
            Register.ZMM29 => "ZMM29",
            Register.ZMM30 => "ZMM30",
            Register.ZMM31 => "ZMM31",
            Register.TMM0 => "TMM0",
            Register.TMM1 => "TMM1",
            Register.TMM2 => "TMM2",
            Register.TMM3 => "TMM3",
            Register.TMM4 => "TMM4",
            Register.TMM5 => "TMM5",
            Register.TMM6 => "TMM6",
            Register.TMM7 => "TMM7",
            Register.FLAGS => "FLAGS",
            Register.EFLAGS => "EFLAGS",
            Register.RFLAGS => "RFLAGS",
            Register.IP => "IP",
            Register.EIP => "EIP",
            Register.RIP => "RIP",
            Register.ES => "ES",
            Register.SS => "SS",
            Register.CS => "CS",
            Register.DS => "DS",
            Register.FS => "FS",
            Register.GS => "GS",
            Register.TR0 => "TR0",
            Register.TR1 => "TR1",
            Register.TR2 => "TR2",
            Register.TR3 => "TR3",
            Register.TR4 => "TR4",
            Register.TR5 => "TR5",
            Register.TR6 => "TR6",
            Register.TR7 => "TR7",
            Register.CR0 => "CR0",
            Register.CR1 => "CR1",
            Register.CR2 => "CR2",
            Register.CR3 => "CR3",
            Register.CR4 => "CR4",
            Register.CR5 => "CR5",
            Register.CR6 => "CR6",
            Register.CR7 => "CR7",
            Register.CR8 => "CR8",
            Register.CR9 => "CR9",
            Register.CR10 => "CR10",
            Register.CR11 => "CR11",
            Register.CR12 => "CR12",
            Register.CR13 => "CR13",
            Register.CR14 => "CR14",
            Register.CR15 => "CR15",
            Register.DR0 => "DR0",
            Register.DR1 => "DR1",
            Register.DR2 => "DR2",
            Register.DR3 => "DR3",
            Register.DR4 => "DR4",
            Register.DR5 => "DR5",
            Register.DR6 => "DR6",
            Register.DR7 => "DR7",
            Register.DR8 => "DR8",
            Register.DR9 => "DR9",
            Register.DR10 => "DR10",
            Register.DR11 => "DR11",
            Register.DR12 => "DR12",
            Register.DR13 => "DR13",
            Register.DR14 => "DR14",
            Register.DR15 => "DR15",
            Register.K0 => "K0",
            Register.K1 => "K1",
            Register.K2 => "K2",
            Register.K3 => "K3",
            Register.K4 => "K4",
            Register.K5 => "K5",
            Register.K6 => "K6",
            Register.K7 => "K7",
            Register.BND0 => "BND0",
            Register.BND1 => "BND1",
            Register.BND2 => "BND2",
            Register.BND3 => "BND3",
            Register.DFV0 => "DFV0",
            Register.DFV1 => "DFV1",
            Register.DFV2 => "DFV2",
            Register.DFV3 => "DFV3",
            Register.DFV4 => "DFV4",
            Register.DFV5 => "DFV5",
            Register.DFV6 => "DFV6",
            Register.DFV7 => "DFV7",
            Register.DFV8 => "DFV8",
            Register.DFV9 => "DFV9",
            Register.DFV10 => "DFV10",
            Register.DFV11 => "DFV11",
            Register.DFV12 => "DFV12",
            Register.DFV13 => "DFV13",
            Register.DFV14 => "DFV14",
            Register.DFV15 => "DFV15",
            Register.MXCSR => "MXCSR",
            Register.PKRU => "PKRU",
            Register.XCR0 => "XCR0",
            Register.GDTR => "GDTR",
            Register.LDTR => "LDTR",
            Register.IDTR => "IDTR",
            Register.TR => "TR",
            Register.BNDCFG => "BNDCFG",
            Register.BNDSTATUS => "BNDSTATUS",
            Register.regUIF => "UIF",
            Register.IA32KernelGSBase => "IA32_KERNEL_GS_BASE",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

#!/usr/bin/env python3
from enum import Enum, IntEnum, IntFlag


def zydis_bool(val):
    return 'ZYAN_TRUE' if val else 'ZYAN_FALSE'


def zydis_instruction_encoding(encoding):
    encoding_name = encoding.upper()
    if encoding_name == 'DEFAULT':
        encoding_name = 'LEGACY'
    return encoding_name


def zydis_vector_length(length):
    if length in ['default', 'placeholder']:
        return 'ZYDIS_VECTOR_LENGTH_INVALID'
    elif length == '128':
        return 'ZYDIS_VECTOR_LENGTH_128'
    elif length == '256':
        return 'ZYDIS_VECTOR_LENGTH_256'
    elif length == '512':
        return 'ZYDIS_VECTOR_LENGTH_512'
    else:
        raise ValueError('Invalid vector length')


class ZydisOperandAction(Enum):
    read = 'READ'
    write = 'WRITE'
    read_write = 'READWRITE'
    condread = 'CONDREAD'
    condwrite = 'CONDWRITE'
    read_condwrite = 'READ_CONDWRITE'
    condread_write = 'CONDREAD_WRITE'


class ZydisBaseRegister(Enum):
    none = 'NONE'
    agpr_reg = 'AGPR_REG'
    agpr_rm = 'AGPR_RM'
    ax = 'AAX'
    dx = 'ADX'
    bx = 'ABX'
    si = 'ASI'
    di = 'ADI'
    sp = 'SSP'
    bp = 'SBP'


class ZydisWidthFlag(IntFlag):
    w_invalid = 0
    w_16 = 1
    w_32 = 2
    w_64 = 4


ZydisRegister = IntEnum('ZydisRegister', [
    'none',
    # General purpose registers 8-bit
    'al', 'cl', 'dl', 'bl',
    'ah', 'ch', 'dh', 'bh',
    'spl', 'bpl', 'sil', 'dil',
    'r8b', 'r9b', 'r10b', 'r11b',
    'r12b', 'r13b', 'r14b', 'r15b',
    # General purpose registers 16-bit
    'ax', 'cx', 'dx', 'bx',
    'sp', 'bp', 'si', 'di',
    'r8w', 'r9w', 'r10w', 'r11w',
    'r12w', 'r13w', 'r14w', 'r15w',
    # General purpose registers 32-bit
    'eax', 'ecx', 'edx', 'ebx',
    'esp', 'ebp', 'esi', 'edi',
    'e8d', 'r9d', 'r10d', 'r11d',
    'r12d', 'r13d', 'r14d', 'r15d',
    # General purpose registers 64-bit
    'rax', 'rcx', 'rdx', 'rbx',
    'rsp', 'rbp', 'rsi', 'rdi',
    'r8', 'r9', 'r10', 'r11',
    'r12', 'r13', 'r14', 'r15',
    # Operand-size scaling general purpose pseudo-registers
    'osz_ax', 'osz_cx', 'osz_dx', 'osz_bx',
    'osz_sp', 'osz_bp', 'osz_si', 'osz_di',
    # Address-size scaling general purpose pseudo-registers
    'asz_ax', 'asz_cx', 'asz_dx', 'asz_bx',
    'asz_sp', 'asz_bp', 'asz_si', 'asz_di',
    # Stack-size scaling general purpose pseudo-registers
    'ssz_ax', 'ssz_cx', 'ssz_dx', 'ssz_bx',
    'ssz_sp', 'ssz_bp', 'ssz_si', 'ssz_di',
    # Address-size scaling misc pseudo-registers
    'asz_ip',
    # Stack-size scaling misc pseudo-registers
    'ssz_ip', 'ssz_flags',
    # Floating point legacy registers
    'st0', 'st1', 'st2', 'st3',
    'st4', 'st5', 'st6', 'st7',
    'x87control', 'x87status', 'x87tag',
    # Floating point multimedia registers
    'mm0', 'mm1', 'mm2', 'mm3',
    'mm4', 'mm5', 'mm6', 'mm7',
    # Floating point vector registers 128-bit
    'xmm0', 'xmm1', 'xmm2', 'xmm3',
    'xmm4', 'xmm5', 'xmm6', 'xmm7',
    'xmm8', 'xmm9', 'xmm10', 'xmm11',
    'xmm12', 'xmm13', 'xmm14', 'xmm15',
    'xmm16', 'xmm17', 'xmm18', 'xmm19',
    'xmm20', 'xmm21', 'xmm22', 'xmm23',
    'xmm24', 'xmm25', 'xmm26', 'xmm27',
    'xmm28', 'xmm29', 'xmm30', 'xmm31',
    # Floating point vector registers 256-bit
    'ymm0', 'ymm1', 'ymm2', 'ymm3',
    'ymm4', 'ymm5', 'ymm6', 'ymm7',
    'ymm8', 'ymm9', 'ymm10', 'ymm11',
    'ymm12', 'ymm13', 'ymm14', 'ymm15',
    'ymm16', 'ymm17', 'ymm18', 'ymm19',
    'ymm20', 'ymm21', 'ymm22', 'ymm23',
    'ymm24', 'ymm25', 'ymm26', 'ymm27',
    'ymm28', 'ymm29', 'ymm30', 'ymm31',
    # Floating point vector registers 512-bit
    'zmm0', 'zmm1', 'zmm2', 'zmm3',
    'zmm4', 'zmm5', 'zmm6', 'zmm7',
    'zmm8', 'zmm9', 'zmm10', 'zmm11',
    'zmm12', 'zmm13', 'zmm14', 'zmm15',
    'zmm16', 'zmm17', 'zmm18', 'zmm19',
    'zmm20', 'zmm21', 'zmm22', 'zmm23',
    'zmm24', 'zmm25', 'zmm26', 'zmm27',
    'zmm28', 'zmm29', 'zmm30', 'zmm31',
    # Matrix registers
    'tmm0', 'tmm1', 'tmm2', 'tmm3',
    'tmm4', 'tmm5', 'tmm6', 'tmm7',
    # Flags registers
    'flags', 'eflags', 'rflags',
    # Instruction-pointer registers
    'ip', 'eip', 'rip',
    # Segment registers
    'es', 'ss', 'cs', 'ds',
    'fs', 'gs',
    # Test registers
    'tr0', 'tr1', 'tr2', 'tr3',
    'tr4', 'tr5', 'tr6', 'tr7',
    # Control registers
    'cr0', 'cr1', 'cr2', 'cr3',
    'cr4', 'cr5', 'cr6', 'cr7',
    'cr8', 'cr9', 'cr10', 'cr11',
    'cr12', 'cr13', 'cr14', 'cr15',
    # Debug registers
    'dr0', 'dr1', 'dr2', 'dr3',
    'dr4', 'dr5', 'dr6', 'dr7',
    'dr8', 'dr9', 'dr10', 'dr11',
    'dr12', 'dr13', 'dr14', 'dr15',
    # Mask registers
    'k0', 'k1', 'k2', 'k3',
    'k4', 'k5', 'k6', 'k7',
    # Bound registers
    'bnd0', 'bnd1', 'bnd2', 'bnd3',
    # Misc registers
    'mxcsr', 'pkru', 'xcr0', 'gdtr',
    'ldtr', 'idtr', 'tr', 'bndcfg',
    'bndstatus', 'uif'
], start=0)


ZydisSegment = IntEnum('ZydisSegment', ['none', 'es', 'cs', 'ss', 'ds', 'fs', 'gs'], start=0)


class ZydisRegisterClassItem(object):

    def __init__(self, low, high):
        self.low = low
        self.high = high


class ZydisRegisterClass(Enum):
    regcINVALID = ZydisRegisterClassItem(ZydisRegister.none, ZydisRegister.none)
    regcGPR8 = ZydisRegisterClassItem(ZydisRegister.al, ZydisRegister.r15b)
    regcGPR16 = ZydisRegisterClassItem(ZydisRegister.ax, ZydisRegister.r15w)
    regcGPR32 = ZydisRegisterClassItem(ZydisRegister.eax, ZydisRegister.r15d)
    regcGPR64 = ZydisRegisterClassItem(ZydisRegister.rax, ZydisRegister.r15)
    regcGPROSZ = ZydisRegisterClassItem(ZydisRegister.osz_ax, ZydisRegister.osz_di)
    regcGPRASZ = ZydisRegisterClassItem(ZydisRegister.asz_ax, ZydisRegister.asz_di)
    regcGPRSSZ = ZydisRegisterClassItem(ZydisRegister.ssz_ax, ZydisRegister.ssz_di)
    regcX87 = ZydisRegisterClassItem(ZydisRegister.st0, ZydisRegister.st7)
    regcMMX = ZydisRegisterClassItem(ZydisRegister.mm0, ZydisRegister.mm7)
    regcXMM = ZydisRegisterClassItem(ZydisRegister.xmm0, ZydisRegister.xmm31)
    regcYMM = ZydisRegisterClassItem(ZydisRegister.ymm0, ZydisRegister.ymm31)
    regcZMM = ZydisRegisterClassItem(ZydisRegister.zmm0, ZydisRegister.zmm31)
    regcTMM = ZydisRegisterClassItem(ZydisRegister.tmm0, ZydisRegister.tmm7)
    regcFLAGS = ZydisRegisterClassItem(ZydisRegister.flags, ZydisRegister.rflags)
    regcIP = ZydisRegisterClassItem(ZydisRegister.ip, ZydisRegister.rip)
    regcSEGMENT = ZydisRegisterClassItem(ZydisRegister.es, ZydisRegister.gs)
    regcTEST = ZydisRegisterClassItem(ZydisRegister.tr0, ZydisRegister.tr7)
    regcCONTROL = ZydisRegisterClassItem(ZydisRegister.cr0, ZydisRegister.cr15)
    regcDEBUG = ZydisRegisterClassItem(ZydisRegister.dr0, ZydisRegister.dr15)
    regcMASK = ZydisRegisterClassItem(ZydisRegister.k0, ZydisRegister.k7)
    regcBOUND = ZydisRegisterClassItem(ZydisRegister.bnd0, ZydisRegister.bnd3)

    @staticmethod
    def _get_register_value(register):
        if isinstance(register, str):
            return ZydisRegister[register].value
        elif isinstance(register, ZydisRegister):
            return register.value

        raise ValueError('Invalid register value!')

    @staticmethod
    def get_register_class(register):
        reg_value = ZydisRegisterClass._get_register_value(register)
        for register_class in ZydisRegisterClass:
            if register_class.value.low <= reg_value <= register_class.value.high:
                return register_class

        return ZydisRegisterClass.regcINVALID

    @staticmethod
    def get_register_id(register):
        reg_value = ZydisRegisterClass._get_register_value(register)
        ignored_classes = [ZydisRegisterClass.regcINVALID, ZydisRegisterClass.regcFLAGS, ZydisRegisterClass.regcIP]
        for register_class in ZydisRegisterClass:
            if register_class in ignored_classes:
                continue
            if register_class.value.low <= reg_value <= register_class.value.high:
                return reg_value - register_class.value.low

        return -1

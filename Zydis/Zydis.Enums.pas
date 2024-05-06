{***************************************************************************************************

  ZydisEditor

  Original Author : Florian Bernd

 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.

***************************************************************************************************}

unit Zydis.Enums;

interface

{ $SCOPEDENUMS ON}

type
  TZYEnumRegister = record
  public
    type Enum = (
      regNone,
      // General purpose registers 8-bit
      regAL,        regCL,        regDL,        regBL,
      regAH,        regCH,        regDH,        regBH,
      regSPL,       regBPL,       regSIL,       regDIL,
      regR8B,       regR9B,       regR10B,      regR11B,
      regR12B,      regR13B,      regR14B,      regR15B,
      // General purpose registers 16-bit
      regAX,        regCX,        regDX,        regBX,
      regSP,        regBP,        regSI,        regDI,
      regR8W,       regR9W,       regR10W,      regR11W,
      regR12W,      regR13W,      regR14W,      regR15W,
      // General purpose registers 32-bit
      regEAX,       regECX,       regEDX,       regEBX,
      regESP,       regEBP,       regESI,       regEDI,
      regE8D,       regR9D,       regR10D,      regR11D,
      regR12D,      regR13D,      regR14D,      regR15D,
      // General purpose registers 64-bit
      regRAX,       regRCX,       regRDX,       regRBX,
      regRSP,       regRBP,       regRSI,       regRDI,
      regR8,        regR9,        regR10,       regR11,
      regR12,       regR13,       regR14,       regR15,
      // Operand-size scaling general purpose pseudo-registers
      regOSZAX,     regOSZCX,     regOSZDX,     regOSZBX,
      regOSZSP,     regOSZBP,     regOSZSI,     regOSZDI,
      // Address-size scaling general purpose pseudo-registers
      regASZAX,     regASZCX,     regASZDX,     regASZBX,
      regASZSP,     regASZBP,     regASZSI,     regASZDI,
      // Stack-size scaling general purpose pseudo-registers
      regSSZAX,     regSSZCX,     regSSZDX,     regSSZBX,
      regSSZSP,     regSSZBP,     regSSZSI,     regSSZDI,
      // Address-size scaling misc pseudo-registers
      regASZIP,
      // Stack-size scaling misc pseudo-registers
      regSSZIP,     regSSZFLAGS,
      // Floating point legacy registers
      regST0,       regST1,       regST2,       regST3,
      regST4,       regST5,       regST6,       regST7,
      regX87CONTROL,regX87STATUS, regX87TAG,
      // Floating point multimedia registers
      regMM0,       regMM1,       regMM2,       regMM3,
      regMM4,       regMM5,       regMM6,       regMM7,
      // Floating point vector registers 128-bit
      regXMM0,      regXMM1,      regXMM2,      regXMM3,
      regXMM4,      regXMM5,      regXMM6,      regXMM7,
      regXMM8,      regXMM9,      regXMM10,     regXMM11,
      regXMM12,     regXMM13,     regXMM14,     regXMM15,
      regXMM16,     regXMM17,     regXMM18,     regXMM19,
      regXMM20,     regXMM21,     regXMM22,     regXMM23,
      regXMM24,     regXMM25,     regXMM26,     regXMM27,
      regXMM28,     regXMM29,     regXMM30,     regXMM31,
      // Floating point vector registers 256-bit
      regYMM0,      regYMM1,      regYMM2,      regYMM3,
      regYMM4,      regYMM5,      regYMM6,      regYMM7,
      regYMM8,      regYMM9,      regYMM10,     regYMM11,
      regYMM12,     regYMM13,     regYMM14,     regYMM15,
      regYMM16,     regYMM17,     regYMM18,     regYMM19,
      regYMM20,     regYMM21,     regYMM22,     regYMM23,
      regYMM24,     regYMM25,     regYMM26,     regYMM27,
      regYMM28,     regYMM29,     regYMM30,     regYMM31,
      // Floating point vector registers 512-bit
      regZMM0,      regZMM1,      regZMM2,      regZMM3,
      regZMM4,      regZMM5,      regZMM6,      regZMM7,
      regZMM8,      regZMM9,      regZMM10,     regZMM11,
      regZMM12,     regZMM13,     regZMM14,     regZMM15,
      regZMM16,     regZMM17,     regZMM18,     regZMM19,
      regZMM20,     regZMM21,     regZMM22,     regZMM23,
      regZMM24,     regZMM25,     regZMM26,     regZMM27,
      regZMM28,     regZMM29,     regZMM30,     regZMM31,
      // Matrix registers
      regTMM0,      regTMM1,      regTMM2,      regTMM3,
      regTMM4,      regTMM5,      regTMM6,      regTMM7,
      // Flags registers
      regFLAGS,     regEFLAGS,    regRFLAGS,
      // Instruction-pointer registers
      regIP,        regEIP,       regRIP,
      // Segment registers
      regES,        regSS,        regCS,        regDS,
      regFS,        regGS,
      // Test registers
      regTR0,       regTR1,       regTR2,       regTR3,
      regTR4,       regTR5,       regTR6,       regTR7,
      // Control registers
      regCR0,       regCR1,       regCR2,       regCR3,
      regCR4,       regCR5,       regCR6,       regCR7,
      regCR8,       regCR9,       regCR10,      regCR11,
      regCR12,      regCR13,      regCR14,      regCR15,
      // Debug registers
      regDR0,       regDR1,       regDR2,       regDR3,
      regDR4,       regDR5,       regDR6,       regDR7,
      regDR8,       regDR9,       regDR10,      regDR11,
      regDR12,      regDR13,      regDR14,      regDR15,
      // Mask registers
      regK0,        regK1,        regK2,        regK3,
      regK4,        regK5,        regK6,        regK7,
      // Bound registers
      regBND0,      regBND1,      regBND2,      regBND3,
      // Misc registers
      regMXCSR,     regPKRU,      regXCR0,      regGDTR,
      regLDTR,      regIDTR,      regTR,        regBNDCFG,
      regBNDSTATUS, regUIF ,      regIA32KernelGSBase
    );
  public
    const JSONStrings: array[Enum] of String = (
      'none',
      // General purpose registers 8-bit
      'al',         'cl',         'dl',         'bl',
      'ah',         'ch',         'dh',         'bh',
      'spl',        'bpl',        'sil',        'dil',
      'r8b',        'r9b',        'r10b',       'r11b',
      'r12b',       'r13b',       'r14b',       'r15b',
      // General purpose registers 16-bit
      'ax',         'cx',         'dx',         'bx',
      'sp',         'bp',         'si',         'di',
      'r8w',        'r9w',        'r10w',       'r11w',
      'r12w',       'r13w',       'r14w',       'r15w',
      // General purpose registers 32-bit
      'eax',        'ecx',        'edx',        'ebx',
      'esp',        'ebp',        'esi',        'edi',
      'e8d',        'r9d',        'r10d',       'r11d',
      'r12d',       'r13d',       'r14d',       'r15d',
      // General purpose registers 64-bit
      'rax',        'rcx',        'rdx',        'rbx',
      'rsp',        'rbp',        'rsi',        'rdi',
      'r8',         'r9',         'r10',        'r11',
      'r12',        'r13',        'r14',        'r15',
      // Operand-size scaling general purpose pseudo-registers
      'osz_ax',     'osz_cx',     'osz_dx',     'osz_bx',
      'osz_sp',     'osz_bp',     'osz_si',     'osz_di',
      // Address-size scaling general purpose pseudo-registers
      'asz_ax',     'asz_cx',     'asz_dx',     'asz_bx',
      'asz_sp',     'asz_bp',     'asz_si',     'asz_di',
      // Stack-size scaling general purpose pseudo-registers
      'ssz_ax',     'ssz_cx',     'ssz_dx',     'ssz_bx',
      'ssz_sp',     'ssz_bp',     'ssz_si',     'ssz_di',
      // Address-size scaling misc pseudo-registers
      'asz_ip',
      // Stack-size scaling misc pseudo-registers
      'ssz_ip',     'ssz_flags',
      // Floating point legacy registers
      'st0',        'st1',        'st2',        'st3',
      'st4',        'st5',        'st6',        'st7',
      'x87control', 'x87status',  'x87tag',
      // Floating point multimedia registers
      'mm0',        'mm1',        'mm2',        'mm3',
      'mm4',        'mm5',        'mm6',        'mm7',
      // Floating point vector registers 128-bit
      'xmm0',       'xmm1',       'xmm2',       'xmm3',
      'xmm4',       'xmm5',       'xmm6',       'xmm7',
      'xmm8',       'xmm9',       'xmm10',      'xmm11',
      'xmm12',      'xmm13',      'xmm14',      'xmm15',
      'xmm16',      'xmm17',      'xmm18',      'xmm19',
      'xmm20',      'xmm21',      'xmm22',      'xmm23',
      'xmm24',      'xmm25',      'xmm26',      'xmm27',
      'xmm28',      'xmm29',      'xmm30',      'xmm31',
      // Floating point vector registers 256-bit
      'ymm0',       'ymm1',       'ymm2',       'ymm3',
      'ymm4',       'ymm5',       'ymm6',       'ymm7',
      'ymm8',       'ymm9',       'ymm10',      'ymm11',
      'ymm12',      'ymm13',      'ymm14',      'ymm15',
      'ymm16',      'ymm17',      'ymm18',      'ymm19',
      'ymm20',      'ymm21',      'ymm22',      'ymm23',
      'ymm24',      'ymm25',      'ymm26',      'ymm27',
      'ymm28',      'ymm29',      'ymm30',      'ymm31',
      // Floating point vector registers 512-bit
      'zmm0',       'zmm1',       'zmm2',       'zmm3',
      'zmm4',       'zmm5',       'zmm6',       'zmm7',
      'zmm8',       'zmm9',       'zmm10',      'zmm11',
      'zmm12',      'zmm13',      'zmm14',      'zmm15',
      'zmm16',      'zmm17',      'zmm18',      'zmm19',
      'zmm20',      'zmm21',      'zmm22',      'zmm23',
      'zmm24',      'zmm25',      'zmm26',      'zmm27',
      'zmm28',      'zmm29',      'zmm30',      'zmm31',
      // Matrix registers
      'tmm0',       'tmm1',       'tmm2',       'tmm3',
      'tmm4',       'tmm5',       'tmm6',       'tmm7',
      // Flags registers
      'flags',      'eflags',     'rflags',
      // Instruction-pointer registers
      'ip',         'eip',        'rip',
      // Segment registers
      'es',         'ss',         'cs',         'ds',
      'fs',         'gs',
      // Test registers
      'tr0',        'tr1',        'tr2',        'tr3',
      'tr4',        'tr5',        'tr6',        'tr7',
      // Control registers
      'cr0',        'cr1',        'cr2',        'cr3',
      'cr4',        'cr5',        'cr6',        'cr7',
      'cr8',        'cr9',        'cr10',       'cr11',
      'cr12',       'cr13',       'cr14',       'cr15',
      // Debug registers
      'dr0',        'dr1',        'dr2',        'dr3',
      'dr4',        'dr5',        'dr6',        'dr7',
      'dr8',        'dr9',        'dr10',       'dr11',
      'dr12',       'dr13',       'dr14',       'dr15',
      // Mask registers
      'k0',         'k1',         'k2',         'k3',
      'k4',         'k5',         'k6',         'k7',
      // Bound registers
      'bnd0',       'bnd1',       'bnd2',       'bnd3',
      // Misc registers
      'mxcsr',      'pkru',       'xcr0',       'gdtr',
      'ldtr',       'idtr',       'tr',         'bndcfg',
      'bndstatus',  'uif' ,       'ia32_kernel_gsbase'
    );
  end;
  TZYRegister = TZYEnumRegister.Enum;

  TZYEnumRegisterClass = record
  public
    type Enum = (
      regcINVALID,
      regcGPR8,
      regcGPR16,
      regcGPR32,
      regcGPR64,
      regcGPROSZ,
      regcGPRASZ,
      regcGPRSSZ,
      regcX87,
      regcMMX,
      regcXMM,
      regcYMM,
      regcZMM,
      regcTMM,
      regcFLAGS,
      regcIP,
      regcSEGMENT,
      regcTEST,
      regcCONTROL,
      regcDEBUG,
      regcMASK,
      regcBOUND
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'INVALID',
      'GPR8',
      'GPR16',
      'GPR32',
      'GPR64',
      '',
      '',
      '',
      'X87',
      'MMX',
      'XMM',
      'YMM',
      'ZMM',
      'TMM',
      'FLAGS',
      'IP',
      'SEGMENT',
      'TEST',
      'CONTROL',
      'DEBUG',
      'MASK',
      'BOUND'
    );
  end;
  TZYRegisterClass = TZYEnumRegisterClass.Enum;

  TZYEnumRegisterKind = record
  public
    type Enum = (
      regkINVALID,
      regkGPR,
      regkX87,
      regkMMX,
      regkVR,
      regkTMM,
      regkSEGMENT,
      regkTEST,
      regkCONTROL,
      regkDEBUG,
      regkMASK,
      regkBOUND
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'INVALID',
      'GPR',
      'X87',
      'MMX',
      'VR',
      'TMM',
      'SEGMENT',
      'TEST',
      'CONTROL',
      'DEBUG',
      'MASK',
      'BOUND'
    );
  end;
  TZYRegisterKind = TZYEnumRegisterKind.Enum;

  TZYRegisterHelper = record helper for TZYRegister
  public
    function GetRegisterClass: TZYRegisterClass;
    function GetRegisterId: Integer;
    function GetRegisterKind: TZYRegisterKind;
  end;

  TZYSegmentRegister = (
    sregNone,
    sregES,
    sregCS,
    sregSS,
    sregDS,
    sregFS,
    sregGS
  );
  TZYSegmentRegisterHelper = record helper for TZYSegmentRegister
  public
    const JSONStrings: array[TZYSegmentRegister] of String = (
      'none', 'es', 'cs', 'ss', 'ds', 'fs', 'gs'
    );
  end;

  TZYBaseRegister = (
    bregNone,
    bregAGPR_REG,
    bregAGPR_RM,
    bregAAX,
    bregADX,
    bregABX,
    bregASI,
    bregADI,
    bregSSP,
    bregSBP
  );
  TZYBaseRegisterHelper = record helper for TZYBaseRegister
  public
    const JSONStrings: array[TZYBaseRegister] of String = (
      'none', 'agpr_reg', 'agpr_rm', 'ax', 'dx', 'bx', 'si', 'di', 'sp', 'bp'
    );
  public
    const ZydisStrings: array[TZYBaseRegister] of String = (
      'NONE',
      'AGPR_REG',
      'AGPR_RM',
      'AAX',
      'ADX',
      'ABX',
      'ASI',
      'ADI',
      'SSP',
      'SBP'
    );
  end;

  TZYOperandSizeMap = ( // TODO: Make this dynamic
    { mode  66  rexw  eosz
      --------------------
      16    _         16
      16    x         32
      32    _         32
      32    x         16
      64    _   _     32
      64    x   _     16
      64    _   x     64
      64    x   x     64
    }
    osmDefault,
    { mode  66  rexw  eosz
      --------------------
      16    ?          8
      32    ?          8
      64    ?   ?      8
    }
    osmByteOperation,     // BYTEOP attribute
    { mode  66  rexw  eosz
      --------------------
      16    ?         16
      32    ?         32
      64    ?   _     32
      64    ?   x     64
    }
    osmIgnore66,          // IGNORE66
    { mode  66  rexw  eosz
      --------------------
      16    _         16
      16    x         32
      32    _         32
      32    x         16
      64    _   ?     32
      64    x   _     16
      64    x   x     32
    }
    osmRexW32,            // IMMUNE_REXW
    { mode  66  rexw  eosz
      --------------------
      16    _         16
      16    x         32
      32    _         32
      32    x         16
      64    _   _     64
      64    x   _     16
      64    _   x     64
      64    x   x     64
    }
    osmDefault64,         // DF64
    { mode  66  rexw  eosz
      --------------------
      16    _         16
      16    x         32
      32    _         32
      32    x         16
      64    ?   ?     64
    }
    osmForce64,           // FORCE64
    { mode  66  rexw  eosz
      --------------------
      16    ?         32
      32    ?         32
      64    ?   _     32
      64    ?   x     64
    }
    osmForce32OrRexW,     // IMMUNE66
    { mode  66  rexw  eosz
      --------------------
      16    ?         32
      32    ?         32
      64    ?   ?     64
    }
    osmForce32Or64        // CR_WIDTH
  );
  TZYOperandSizeMapHelper = record helper for TZYOperandSizeMap
  public
    const JSONStrings: array[TZYOperandSizeMap] of String = (
      'default',
      'byteop',
      'ignore66',
      'rexw32',
      'default64',
      'force64',
      'force32_or_rexw',
      'force32_or_64'
    );
  end;

  TZYAddressSizeMap = ( // TODO: Make this dynamic
    asmDefault,
    asmIgnored,
    asmForce32Or64
  );
  TZYAddressSizeMapMapHelper = record helper for TZYAddressSizeMap
  public
    const JSONStrings: array[TZYAddressSizeMap] of String = (
      'default',
      'ignored',
      'force32_or_64'
    );
  end;

  TZYEnumDefinitionFlag = record
  public  // TODO: Find better names
    type Enum = (
      dfForceConflict,  // Forces a conflict
      dfForceRegForm,   // Forces the instruction to allways assume "reg, reg" form (modrm.mod = 3)
      dfProtectedMode,  // Instruction is invalid in real and 8086 mode
      dfNoCompatMode,   // Instruction is invalid incompatibility mode
      dfIsShortBranch,
      dfIsNearBranch,
      dfIsFarBranch,
      dfStateCPU_CR,
      dfStateCPU_CW,
      dfStateFPU_CR,
      dfStateFPU_CW,
      dfStateXMM_CR,
      dfStateXMM_CW,
      dfNoSourceDestMatch, // UD if the dst register matches any of the source regs
      dfNoSourceSourceMatch, // AMX-E4
      dfIsGather
    );
  public
    const JSONStrings: array[Enum] of String = (
      'force_conflict',
      'ignore_modrm_mod',
      'protected_mode',
      'no_compat_mode',
      'short_branch',
      'near_branch',
      'far_branch',
      'cpu_state_cr',
      'cpu_state_cw',
      'fpu_state_cr',
      'fpu_state_cw',
      'xmm_state_cr',
      'xmm_state_cw',
      'no_source_dest_match',
      'no_source_source_match',
      'is_gather'
    );
  end;
  TZYDefinitionFlag = TZYEnumDefinitionFlag.Enum;
  TZYDefinitionFlags = set of TZYDefinitionFlag;

  TZYEnumPrefixFlag = record
  public
    type Enum = (
      pfAcceptsLOCK,
      pfAcceptsREP,
      pfAcceptsREPEREPZ,
      pfAcceptsREPNEREPNZ,
      pfAcceptsBOUND,
      pfAcceptsXACQUIRE,
      pfAcceptsXRELEASE,
      pfAcceptsNOTRACK,
      pfAcceptsLocklessHLE,
      pfAcceptsBranchHints
    );
  public
    const JSONStrings: array[Enum] of String = (
      'accepts_lock',
      'accepts_rep',
      'accepts_reperepz',
      'accepts_repnerepnz',
      'accepts_bound',
      'accepts_xacquire',
      'accepts_xrelease',
      'accepts_notrack',
      'accepts_lockless_hle',
      'accepts_branch_hints'
    );
  end;
  TZYPrefixFlag = TZYEnumPrefixFlag.Enum;
  TZYPrefixFlags = set of TZYPrefixFlag;

  TZYExceptionClass = (
    ecNone,
    ecSSE1,
    ecSSE2,
    ecSSE3,
    ecSSE4,
    ecSSE5,
    ecSSE7,
    ecAVX1,
    ecAVX2,
    ecAVX3,
    ecAVX4,
    ecAVX5,
    ecAVX6,
    ecAVX7,
    ecAVX8,
    ecAVX11,
    ecAVX12,
    ecE1,
    ecE1NF,
    ecE2,
    ecE2NF,
    ecE3,
    ecE3NF,
    ecE4,
    ecE4NF,
    ecE5,
    ecE5NF,
    ecE6,
    ecE6NF,
    ecE7NM,
    ecE7NM128,
    ecE9NF,
    ecE10,
    ecE10NF,
    ecE11,
    ecE11NF,
    ecE12,
    ecE12NP,
    ecK20,
    ecK21,
    ecAMXE1,
    ecAMXE2,
    ecAMXE3,
    ecAMXE4,
    ecAMXE5,
    ecAMXE6
  );
  TZYExceptionClassHelper = record helper for TZYExceptionClass
  public
    const JSONStrings: array[TZYExceptionClass] of String = (
      'none',
      'sse1',
      'sse2',
      'sse3',
      'sse4',
      'sse5',
      'sse7',
      'avx1',
      'avx2',
      'avx3',
      'avx4',
      'avx5',
      'avx6',
      'avx7',
      'avx8',
      'avx11',
      'avx12',
      'e1',
      'e1nf',
      'e2',
      'e2nf',
      'e3',
      'e3nf',
      'e4',
      'e4nf',
      'e5',
      'e5nf',
      'e6',
      'e6nf',
      'e7nm',
      'e7nm128',
      'e9nf',
      'e10',
      'e10nf',
      'e11',
      'e11nf',
      'e12',
      'e12np',
      'k20',
      'k21',
      'amxe1',
      'amxe2',
      'amxe3',
      'amxe4',
      'amxe5',
      'amxe6'
    );
  public
    const ZydisStrings: array[TZYExceptionClass] of String = (
      'NONE',
      'SSE1',
      'SSE2',
      'SSE3',
      'SSE4',
      'SSE5',
      'SSE7',
      'AVX1',
      'AVX2',
      'AVX3',
      'AVX4',
      'AVX5',
      'AVX6',
      'AVX7',
      'AVX8',
      'AVX11',
      'AVX12',
      'E1',
      'E1NF',
      'E2',
      'E2NF',
      'E3',
      'E3NF',
      'E4',
      'E4NF',
      'E5',
      'E5NF',
      'E6',
      'E6NF',
      'E7NM',
      'E7NM128',
      'E9NF',
      'E10',
      'E10NF',
      'E11',
      'E11NF',
      'E12',
      'E12NP',
      'K20',
      'K21',
      'AMXE1',
      'AMXE2',
      'AMXE3',
      'AMXE4',
      'AMXE5',
      'AMXE6'
    );
  end;

  TZYVectorLength = (
    vlDefault,
    vlFixed128,
    vlFixed256,
    vlFixed512
  );
  TZYVectorLengthHelper = record helper for TZYVectorLength
  public
    const JSONStrings: array[TZYVectorLength] of String = (
      'default',
      '128',
      '256',
      '512'
    );
  public
    const ZydisStrings: array[TZYVectorLength] of String = (
      'DEFAULT',
      'FIXED_128',
      'FIXED_256',
      'FIXED_512'
    );
  end;

  TZYMEVEXMaskMode = (
    mmMaskInvalid,
    mmMaskAllowed,
    mmMaskRequired,
    mmMaskForbidden
  );
  TZYMEVEXMaskModeHelper = record helper for TZYMEVEXMaskMode
  public
    const JSONStrings: array[TZYMEVEXMaskMode] of String = (
      'invalid',
      'allowed',
      'required',
      'forbidden'
    );
  public
    const ZydisStrings: array[TZYMEVEXMaskMode] of String = (
      'INVALID',
      'ALLOWED',
      'REQUIRED',
      'FORBIDDEN'
    );
  end;

  TZYEVEXMaskFlag = (
    mfIsControlMask,
    mfAcceptsZeroMask,
    mfForceZeroMask
  );
  TZYEVEXMaskFlagHelper = record helper for TZYEVEXMaskFlag
  public
    const JSONStrings: array[TZYEVEXMaskFlag] of String = (
      'is_control_mask',
      'accepts_zero_mask',
      'force_zero_mask'
    );
  end;
  TZYEVEXMaskFlags = set of TZYEVEXMaskFlag;

  TZYEVEXTupleType = ( // TODO: Reorder
    ttInvalid,
    ttFV,
    ttHV,
    ttFVM,
    ttT1S,
    ttT1F,
    ttGSCAT, // TODO: Remove
    ttT2,
    ttT4,
    ttT8,
    ttHVM,
    ttQVM,
    ttOVM,
    ttM128,
    ttDUP,
    ttT14X,
    ttQUARTER
  );
  TZYEVEXTupleTypeHelper = record helper for TZYEVEXTupleType
  public
    const JSONStrings: array[TZYEVEXTupleType] of String = (
      'invalid',
      'fv',
      'hv',
      'fvm',
      't1s',
      't1f',
      'gscat',
      't2',
      't4',
      't8',
      'hvm',
      'qvm',
      'ovm',
      'm128',
      'dup',
      't1_4x',
      'quarter'
    );
  public
    const ZydisStrings: array[TZYEVEXTupleType] of String = (
      'INVALID',
      'FV',
      'HV',
      'FVM',
      'T1S',
      'T1F',
      'GSCAT',
      'T2',
      'T4',
      'T8',
      'HVM',
      'QVM',
      'OVM',
      'M128',
      'DUP',
      'T1_4X',
      'QUARTER'
    );
  end;

  TZYEVEXElementSize = (
    esInvalid,
    es8Bit,
    es16Bit,
    es32Bit,
    es64Bit,
    es128Bit
  );
  TZYEVEXElementSizeHelper = record helper for TZYEVEXElementSize
  public
    const JSONStrings: array[TZYEVEXElementSize] of String = (
      'invalid',
      '8',
      '16',
      '32',
      '64',
      '128'
    );
  public
    const ZydisStrings: array[TZYEVEXElementSize] of String = (
      'INVALID',
      '8',
      '16',
      '32',
      '64',
      '128'
    );
  end;

  TZYEVEXFunctionality = (
    evInvalid,
    evBC,
    evRC,
    evSAE
  );
  TZYEVEXFunctionalityHelper = record helper for TZYEVEXFunctionality
  public
    const JSONStrings: array[TZYEVEXFunctionality] of String = (
      'invalid',
      'bc',
      'rc',
      'sae'
    );
  public
    const ZydisStrings: array[TZYEVEXFunctionality] of String = (
      'INVALID',
      'BC',
      'RC',
      'SAE'
    );
  end;

  TZYMVEXFunctionality = (
    mvIgnored,
    mvInvalid,
    mvRC,
    mvSAE,
    mvf32,            // No special operation (float32 elements)
    mvf64,            // No special operation (float64 elements)
    mvi32,            // No special operation (uint32 elements)
    mvi64,            // No special operation (uint64 elements)
    mvSwizzle32,      // Sf32 / Si32 (register only)
    mvSwizzle64,      // Sf64 / Si64 (register only)
    mvSf32,           // (memory only)
    mvSf32Bcst,       // (memory only, broadcast only)
    mvSf32Bcst4to16,  // (memory only, broadcast 4to16 only)
    mvSf64,           // (memory only)
    mvSi32,           // (memory only)
    mvSi32Bcst,       // (memory only, broadcast only)
    mvSi32Bcst4to16,  // (memory only, broadcast 4to16 only)
    mvSi64,           // (memory only)
    mvUf32,
    mvUf64,
    mvUi32,
    mvUi64,
    mvDf32,
    mvDf64,
    mvDi32,
    mvDi64
  );
  TZYMVEXFunctionalityHelper = record helper for TZYMVEXFunctionality
  public
    const JSONStrings: array[TZYMVEXFunctionality] of String = (
      'ignored',
      'invalid',
      'rc',
      'sae',
      'f32',
      'i32',
      'f64',
      'i64',
      'swizzle32',
      'swizzle64',
      's_f32',
      's_f32_bcst',
      's_f32_bcst4to16',
      's_f64',
      's_i32',
      's_i32_bcst',
      's_i32_bcst4to16',
      'si_64',
      'u_f32',
      'u_f64',
      'u_i32',
      'u_i64',
      'd_f32',
      'd_f64',
      'd_i32',
      'd_i64'
    );
  public
    const ZydisStrings: array[TZYMVEXFunctionality] of String = (
      'IGNORED',
      'INVALID',
      'RC',
      'SAE',
      'F_32',
      'I_32',
      'F_64',
      'I_64',
      'SWIZZLE_32',
      'SWIZZLE_64',
      'SF_32',
      'SF_32_BCST',
      'SF_32_BCST_4TO16',
      'SF_64',
      'SI_32',
      'SI_32_BCST',
      'SI_32_BCST_4TO16',
      'SI_64',
      'UF_32',
      'UF_64',
      'UI_32',
      'UI_64',
      'DF_32',
      'DF_64',
      'DI_32',
      'DI_64'
    );
  end;

  TZYStaticBroadcast = (
    sbcNone,
    sbcBroadcast1to2,
    sbcBroadcast1to4,
    sbcBroadcast1to8,
    sbcBroadcast1to16,
    sbcBroadcast1to32,
    sbcBroadcast1to64,
    sbcBroadcast2to4,
    sbcBroadcast2to8,
    sbcBroadcast2to16,
    sbcBroadcast4to8,
    sbcBroadcast4to16,
    sbcBroadcast8to16
  );
  TZYStaticBroadcastHelper = record helper for TZYStaticBroadcast
  public
    const JSONStrings: array[TZYStaticBroadcast] of String = (
      'none',
      '1to2',
      '1to4',
      '1to8',
      '1to16',
      '1to32',
      '1to64',
      '2to4',
      '2to8',
      '2to16',
      '4to8',
      '4to16',
      '8to16'
    );
  public
    const ZydisStrings: array[TZYStaticBroadcast] of String = (
      'NONE',
      '1_TO_2',
      '1_TO_4',
      '1_TO_8',
      '1_TO_16',
      '1_TO_32',
      '1_TO_64',
      '2_TO_4',
      '2_TO_8',
      '2_TO_16',
      '4_TO_8',
      '4_TO_16',
      '8_TO_16'
    );
  end;

  TZYEnumElementType = record
  public
    type Enum = (
      emtINVALID,
      emtVARIABLE, // TODO: Remove
      emtSTRUCT,
      emtINT,
      emtUINT,
      emtINT1,
      emtINT8,
      emtINT8X4,
      emtINT16,
      emtINT16X2,
      emtINT32,
      emtINT64,
      emtUINT8,
      emtUINT8X4,
      emtUINT16,
      emtUINT16X2,
      emtUINT32,
      emtUINT64,
      emtUINT128,
      emtUINT256,
      emtFLOAT16,
      emtFLOAT16X2,
      emtFLOAT32,
      emtFLOAT64,
      emtFLOAT80,
      emtBFLOAT16X2,
      emtBCD80,
      emtCC3,
      emtCC5
    );
  public
    const Names: array[Enum] of String = (
      'emtINVALID',
      'emtVARIABLE',
      'emtSTRUCT',
      'emtINT',
      'emtUINT',
      'emtINT1',
      'emtINT8',
      'emtINT8X4',
      'emtINT16',
      'emtINT16X2',
      'emtINT32',
      'emtINT64',
      'emtUINT8',
      'emtUINT8X4',
      'emtUINT16',
      'emtUINT16X2',
      'emtUINT32',
      'emtUINT64',
      'emtUINT128',
      'emtUINT256',
      'emtFLOAT16',
      'emtFLOAT16X2',
      'emtFLOAT32',
      'emtFLOAT64',
      'emtFLOAT80',
      'emtBFLOAT16X2',
      'emtBCD80',
      'emtCC3',
      'emtCC5'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'invalid',
      'variable',
      'struct',
      'int',
      'uint',
      'int1',
      'int8',
      'int8x4',
      'int16',
      'int16x2',
      'int32',
      'int64',
      'uint8',
      'uint8x4',
      'uint16',
      'uint16x2',
      'uint32',
      'uint64',
      'uint128',
      'uint256',
      'float16',
      'float16x2',
      'float32',
      'float64',
      'bfloat16x2',
      'float80',
      'bcd80',
      'cc3',
      'cc5'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'INVALID',
      'VARIABLE',
      'STRUCT',
      'INT',
      'UINT',
      'INT1',
      'INT8',
      'INT8X4',
      'INT16',
      'INT16X2',
      'INT32',
      'INT64',
      'UINT8',
      'UINT8X4',
      'UINT16',
      'UINT16X2',
      'UINT32',
      'UINT64',
      'UINT128',
      'UINT256',
      'FLOAT16',
      'FLOAT16X2',
      'FLOAT32',
      'FLOAT64',
      'FLOAT80',
      'BFLOAT16X2',
      'BCD80',
      'CC3',
      'CC5'
    );
  public
    const Types: array[Enum] of String = (
      'INVALID',
      'VARIABLE',
      'STRUCT',
      'INT',
      'UINT',
      'INT',
      'INT',
      'INT',
      'INT',
      'INT',
      'INT',
      'INT',
      'UINT',
      'UINT',
      'UINT',
      'UINT',
      'UINT',
      'UINT',
      'UINT',
      'UINT',
      'FLOAT16',
      'FLOAT16X2',
      'FLOAT32',
      'FLOAT64',
      'FLOAT80',
      'BFLOAT16X2',
      'LONGBCD',
      'CC',
      'CC'
    );
  public
    const Size: array[Enum] of Integer = (
      0,
      0,
      0,
      0,
      0,
      1,
      8,
      32,
      16,
      32,
      32,
      64,
      8,
      32,
      16,
      32,
      32,
      64,
      128,
      256,
      16,
      32,
      32,
      64,
      80,
      32,
      80,
      3,
      5
    );
  end;
  TZYElementType = TZYEnumElementType.Enum;

  TZYRegisterConstraint = (
    ocUnused,
    ocNone,
    ocGPR,
    ocSRDest,
    ocSR,
    ocCR,
    ocDR,
    ocMASK,
    ocBND,
    ocTMM,
    ocVSIB,
    ocNoRel
  );
  TZYRegisterConstraintHelper = record helper for TZYRegisterConstraint
  public
    const ZydisStrings: array[TZYRegisterConstraint] of String = (
      'UNUSED',
      'NONE',
      'GPR',
      'SR_DEST',
      'SR',
      'CR',
      'DR',
      'MASK',
      'BND',
      'TMM',
      'VSIB',
      'NO_REL'
    );
  end;

  TZYEnumOperandType = record
  public
    type Enum = (
      optUnused,
      optImplicitReg,
      optImplicitMem,
      optImplicitImm1,
      optGPR8,
      optGPR16,
      optGPR32,
      optGPR64,
      optGPR16_32_64, // GPRv
      optGPR32_32_64, // GPRy
      optGPR16_32_32, // GPRz
      optGPRASZ,
      optFPR,
      optMMX,
      optXMM,
      optYMM,
      optZMM,
      optTMM,
      optBND,
      optSREG, // TODO: CS is not allowed as move target
      optCR,
      optDR,
      optMASK,
      optMEM,
      {TODO: Note that the presence of VSIB byte is enforced in this instruction. Hence, the instruction will #UD fault if
      ModRM.rm is different than 100b.
      The instruction will #UD fault if the destination vector zmm1 is the same as index vector VINDEX. The instruction
      will #UD fault if the k0 mask register is specified.}
      optMEMVSIBX,
      optMEMVSIBY,
      optMEMVSIBZ,
      optIMM,
      optREL,
      optPTR,
      optAGEN,
      optAGENNoRel,   // RIP rel invalid
      optMOFFS,
      optMIB          // MPX Memory Operand
    );
  public
    const JSONStrings: array[Enum] of String = (
      'unused',
      'implicit_reg',
      'implicit_mem',
      'implicit_imm1',
      'gpr8',
      'gpr16',
      'gpr32',
      'gpr64',
      'gpr16_32_64',
      'gpr32_32_64',
      'gpr16_32_32',
      'gpr_asz',
      'fpr',
      'mmx',
      'xmm',
      'ymm',
      'zmm',
      'tmm',
      'bnd',
      'sreg',
      'cr',
      'dr',
      'mask',
      'mem',
      'mem_vsibx',
      'mem_vsiby',
      'mem_vsibz',
      'imm',
      'rel',
      'ptr',
      'agen',
      'agen_norel',
      'moffs',
      'mib'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'UNUSED',
      'IMPLICIT_REG',
      'IMPLICIT_MEM',
      'IMPLICIT_IMM1',
      'GPR8',
      'GPR16',
      'GPR32',
      'GPR64',
      'GPR16_32_64',
      'GPR32_32_64',
      'GPR16_32_32',
      'GPR_ASZ',
      'FPR',
      'MMX',
      'XMM',
      'YMM',
      'ZMM',
      'TMM',
      'BND',
      'SREG',
      'CR',
      'DR',
      'MASK',
      'MEM',
      'MEM_VSIBX',
      'MEM_VSIBY',
      'MEM_VSIBZ',
      'IMM',
      'REL',
      'PTR',
      'AGEN',
      'AGEN',
      'MOFFS',
      'MIB'
    );
  end;
  TZYOperandType = TZYEnumOperandType.Enum;

  TZYEnumOperandAction = record
  public
    type Enum = (
      opaRead,
      opaWrite,
      opaReadWrite,
      opaCondRead,
      opaCondWrite,
      opaReadCondWrite,
      opaCondReadWrite
    );
  public
    const Names: array[Enum] of String = (
      'opaRead',
      'opaWrite',
      'opaReadWrite',
      'opaCondRead',
      'opaCondWrite',
      'opaReadCondWrite',
      'opaCondReadWrite'
    );
  public
    const JSONStrings: array[Enum] of String = (
      'read',
      'write',
      'read_write',
      'condread',
      'condwrite',
      'read_condwrite',
      'condread_write'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'READ',
      'WRITE',
      'READWRITE',
      'CONDREAD',
      'CONDWRITE',
      'READ_CONDWRITE',
      'CONDREAD_WRITE'
    );
  public
    const DisplayStrings: array[Enum] of String = (
      'r',
      'w',
      'rw',
      'cr',
      'cw',
      'r, cw',
      'cr, w'
    );
  end;
  TZYOperandAction = TZYEnumOperandAction.Enum;

  TZYEnumOperandEncoding = record
  public
    type Enum = (
      opeNone,
      opeModrmReg,
      opeModrmRm,
      opeOpcodeBits,
      opeNDSNDD,          // VEX/EVEX/MVEX.VVVV
      opeIS4,
      opeMASK,         //     EVEX/MVEX.AAA
      opeDisp8,
      opeDisp16,
      opeDisp32,
      opeDisp64,
      opeDisp16_32_64, // DISPv
      opeDisp32_32_64, // DISPy
      opeDisp16_32_32, // DISPz
      opeUImm8,
      opeUImm16,
      opeUImm32,
      opeUImm64,
      opeUImm16_32_64, // UIMMv
      opeUImm32_32_64, // UIMMy
      opeUImm16_32_32, // UIMMz
      opeSImm8,
      opeSImm16,
      opeSImm32,
      opeSImm64,
      opeSImm16_32_64, // SIMMv
      opeSImm32_32_64, // SIMMy
      opeSImm16_32_32, // SIMMz
      opeJImm8,
      opeJImm16,
      opeJImm32,
      opeJImm64,
      opeJImm16_32_64, // JImmv
      opeJImm32_32_64, // JImmy
      opeJImm16_32_32  // JImmz
    );
  public
    const JSONStrings: array[Enum] of String = (
      'none',
      'modrm_reg',
      'modrm_rm',
      'opcode',
      'ndsndd',
      'is4',
      'mask',
      'disp8',
      'disp16',
      'disp32',
      'disp64',
      'disp16_32_64',
      'disp32_32_64',
      'disp16_32_32',
      'uimm8',
      'uimm16',
      'uimm32',
      'uimm64',
      'uimm16_32_64',
      'uimm32_32_64',
      'uimm16_32_32',
      'simm8',
      'simm16',
      'simm32',
      'simm64',
      'simm16_32_64',
      'simm32_32_64',
      'simm16_32_32',
      'jimm8',
      'jimm16',
      'jimm32',
      'jimm64',
      'jimm16_32_64',
      'jimm32_32_64',
      'jimm16_32_32'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'NONE',
      'MODRM_REG',
      'MODRM_RM',
      'OPCODE',
      'NDSNDD',
      'IS4',
      'MASK',
      'DISP8',
      'DISP16',
      'DISP32',
      'DISP64',
      'DISP16_32_64',
      'DISP32_32_64',
      'DISP16_32_32',
      'UIMM8',
      'UIMM16',
      'UIMM32',
      'UIMM64',
      'UIMM16_32_64',
      'UIMM32_32_64',
      'UIMM16_32_32',
      'SIMM8',
      'SIMM16',
      'SIMM32',
      'SIMM64',
      'SIMM16_32_64',
      'SIMM32_32_64',
      'SIMM16_32_32',
      'JIMM8',
      'JIMM16',
      'JIMM32',
      'JIMM64',
      'JIMM16_32_64',
      'JIMM32_32_64',
      'JIMM16_32_32'
    );
  end;
  TZYOperandEncoding = TZYEnumOperandEncoding.Enum;

  TZYEnumScaleFactor = record
  public
    type Enum = (
      sfStatic,
      sfScaleOSZ,
      sfScaleASZ,
      sfScaleSSZ
    );
  public
    const JSONStrings: array[Enum] of String = (
      'static',
      'osz',
      'asz',
      'ssz'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'STATIC',
      'OSZ',
      'ASZ',
      'SSZ'
    );
  end;
  TZYScaleFactor = TZYEnumScaleFactor.Enum;

  TZYEnumOperandVisibility = record
  public
    type Enum = (
      opvExplicit,
      opvImplicit,
      opvHidden,
      opvUnused
    );
  public
    const Names: array[Enum] of String = (
      'opvExplicit',
      'opvImplicit',
      'opvHidden',
      'opvUnused'
    );
  public
    const ZydisStrings: array[Enum] of String = (
      'EXPLICIT',
      'IMPLICIT',
      'HIDDEN',
      'UNUSED'
    );
  end;
  TZYOperandVisibility = TZYEnumOperandVisibility.Enum;

  TZYFlagsAccess = (
    faReadOnly,
    faMustWrite,
    faMayWrite
  );
  TZYFlagsAccessHelper = record helper for TZYFlagsAccess
  public
    const JSONStrings: array[TZYFlagsAccess] of String = (
      'read_only',
      'must_write',
      'may_write'
    );
  end;

  TZYFlagOperation = (
    foNone,
    foTested,
    foTestedModified,
    foModified,
    foSet0,
    foSet1,
    foUndefined
  );
  TZYFlagOperationHelper = record helper for TZYFlagOperation
  public
    const JSONStrings: array[TZYFlagOperation] of String = (
      'none',
      't',
      't_m',
      'm',
      '0',
      '1',
      'u'
    );
  public
    const ZydisStrings: array[TZYFlagOperation] of String = (
      'NONE',
      'TESTED',
      'TESTED_MODIFIED',
      'MODIFIED',
      'SET_0',
      'SET_1',
      'UNDEFINED'
    );
  end;

implementation

type
  TZYRegisterMapItem = record
  public
    Lo: TZYRegister;
    Hi: TZYRegister;
  end;

const
  RegisterMap: array[TZYRegisterClass] of TZYRegisterMapItem = (
    (Lo: regNone   ; Hi: regNone   ),
    (Lo: regAL     ; Hi: regR15B   ),
    (Lo: regAX     ; Hi: regR15W   ),
    (Lo: regEAX    ; Hi: regR15D   ),
    (Lo: regRAX    ; Hi: regR15    ),
    (Lo: regOSZAX  ; Hi: regOSZDI  ),
    (Lo: regASZAX  ; Hi: regASZDI  ),
    (Lo: regSSZAX  ; Hi: regSSZDI  ),
    (Lo: regST0    ; Hi: regST7    ),
    (Lo: regMM0    ; Hi: regMM7    ),
    (Lo: regXMM0   ; Hi: regXMM31  ),
    (Lo: regYMM0   ; Hi: regYMM31  ),
    (Lo: regZMM0   ; Hi: regZMM31  ),
    (Lo: regTMM0   ; Hi: regTMM7   ),
    (Lo: regFLAGS  ; Hi: regRFLAGS ),
    (Lo: regIP     ; Hi: regRIP    ),
    (Lo: regES     ; Hi: regGS     ),
    (Lo: regTR0    ; Hi: regTR7    ),
    (Lo: regCR0    ; Hi: regCR15   ),
    (Lo: regDR0    ; Hi: regDR15   ),
    (Lo: regK0     ; Hi: regK7     ),
    (Lo: regBND0   ; Hi: regBND3   )
  );

{ TZYRegisterHelper }

function TZYRegisterHelper.GetRegisterClass: TZYRegisterClass;
var
  C: TZYRegisterClass;
begin
  Result := regcInvalid;
  for C := Low(RegisterMap) to High(RegisterMap) do
  begin
    if (Ord(Self) >= Ord(RegisterMap[C].Lo)) and (Ord(Self) <= Ord(RegisterMap[C].Hi)) then
    begin
      Result := C;
      Break;
    end;
  end;
end;

function TZYRegisterHelper.GetRegisterId: Integer;
var
  C: TZYRegisterClass;
begin
  Result := -1;
  for C := Low(RegisterMap) to High(RegisterMap) do
  begin
    if (C in [regcInvalid, regcFlags, regcIP]) then
    begin
      Continue;
    end;
    if (Ord(Self) >= Ord(RegisterMap[C].Lo)) and (Ord(Self) <= Ord(RegisterMap[C].Hi)) then
    begin
      Result := Ord(Self) - Ord(RegisterMap[C].Lo);
      Break;
    end;
  end;
end;

function TZYRegisterHelper.GetRegisterKind: TZYRegisterKind;
var
  C: TZYRegisterClass;
begin
  Result := regkINVALID;

  C := Self.GetRegisterClass();
  case C of
    regcGPR8   ,
    regcGPR16  ,
    regcGPR32  ,
    regcGPR64  ,
    regcGPROSZ ,
    regcGPRASZ ,
    regcGPRSSZ : Result := regkGPR;
    regcX87    : Result := regkX87;
    regcMMX    : Result := regkMMX;
    regcXMM    ,
    regcYMM    ,
    regcZMM    : Result := regkVR;
    regcTMM    : Result := regkTMM;
    regcSEGMENT: Result := regkSEGMENT;
    regcTEST   : Result := regkTEST;
    regcCONTROL: Result := regkCONTROL;
    regcDEBUG  : Result := regkDEBUG;
    regcMASK   : Result := regkMASK;
    regcBOUND  : Result := regkBOUND;
  end;
end;

end.

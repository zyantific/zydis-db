using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class DefinitionEmitter
{
    public static async Task EmitAsync(StreamWriter writer, DefinitionRegistry definitionRegistry, OperandsRegistry operandsRegistry, AccessedFlagsRegistry accessedFlagsRegistry)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(definitionRegistry);
        ArgumentNullException.ThrowIfNull(operandsRegistry);
        ArgumentNullException.ThrowIfNull(accessedFlagsRegistry);

        var declarationWriter = DeclarationWriter.Create(writer);

        foreach (var encoding in Enum.GetValues<InstructionEncoding>())
        {
            var definitions = definitionRegistry[encoding];

            var encodingName = encoding switch
            {
                InstructionEncoding.Default => "LEGACY",
                InstructionEncoding.VEX => "VEX",
                InstructionEncoding.EVEX => "EVEX",
                InstructionEncoding.MVEX => "MVEX",
                InstructionEncoding.XOP => "XOP",
                InstructionEncoding.AMD3DNOW => "3DNOW",
                _ => throw new InvalidOperationException()
            };

            var initializerListWriter = declarationWriter
                .BeginDeclaration("static const", $"ZydisInstructionDefinition{encodingName}", $"ISTR_DEFINITIONS_{encodingName}[]")
                .WriteInitializerList()
                .BeginList();

            //var i = 0;

            foreach (var definition in definitions)
            {
                // Initialize base struct
                initializerListWriter
                    //.WriteInlineComment("{0:X4}", i++)
                    .WriteInitializerList()
                    .BeginList()
                    .WriteFieldDesignation("mnemonic").WriteExpression("ZYDIS_MNEMONIC_{0}", definition.Mnemonic.ToUpperInvariant())
                    .Conditional().WriteFieldDesignation("operand_count").WriteInteger(definition.NumberOfOperands)
                    .Conditional().WriteFieldDesignation("operand_count_visible").WriteInteger(definition.NumberOfVisibleOperands)
                    .Conditional().WriteFieldDesignation("operand_reference").WriteInteger(operandsRegistry.GetOperandsIndex(definition), 4, true)
                    .WriteFieldDesignation("operand_size_map").WriteInteger((int)definition.OpsizeMap)
                    .WriteFieldDesignation("address_size_map").WriteInteger((int)definition.AdsizeMap)
                    .Conditional().WriteFieldDesignation("flags_reference").WriteInteger(accessedFlagsRegistry.GetAccessedFlagsId(definition), 2, true) // TODO: !
                    .WriteFieldDesignation("requires_protected_mode").WriteBool(definition.Flags.HasFlag(InstructionFlagsEnum.ProtectedMode))
                    .WriteFieldDesignation("no_compat_mode").WriteBool(definition.Flags.HasFlag(InstructionFlagsEnum.NoCompatMode))
                    .Conditional().WriteFieldDesignation("category").WriteExpression("ZYDIS_CATEGORY_{0}", definition.MetaInfo.Category.ToUpperInvariant())
                    .Conditional().WriteFieldDesignation("isa_set").WriteExpression("ZYDIS_ISA_SET_{0}", definition.MetaInfo.IsaSet.ToUpperInvariant())
                    .Conditional().WriteFieldDesignation("isa_ext").WriteExpression("ZYDIS_ISA_EXT_{0}", definition.MetaInfo.IsaExtension.ToUpperInvariant())
                    .Conditional().WriteFieldDesignation("branch_type").WriteExpression("ZYDIS_BRANCH_TYPE_{0}", GetBranchType(definition))
                    .Conditional().WriteFieldDesignation("exception_class").WriteExpression("ZYDIS_EXCEPTION_CLASS_{0}", (definition.ExceptionClass ?? ExceptionClass.None).ToZydisString())
                    .WriteFieldDesignation("op_reg").WriteExpression(GetRegisterConstraint(definition.Operands, OperandEncoding.ModrmReg))
                    .WriteFieldDesignation("op_rm").WriteExpression(GetRegisterConstraint(definition.Operands, OperandEncoding.ModrmRm))
                    .Conditional().WriteFieldDesignation("cpu_state").WriteExpression("ZYDIS_RW_ACTION_{0}", GetCpuState(definition))
                    .Conditional().WriteFieldDesignation("fpu_state").WriteExpression("ZYDIS_RW_ACTION_{0}", GetFpuState(definition))
                    .Conditional().WriteFieldDesignation("xmm_state").WriteExpression("ZYDIS_RW_ACTION_{0}", GetXmmState(definition))
                    .Conditional().WriteFieldDesignation("accepts_segment").WriteBool(AcceptsSegment(definition));

                if (encoding is InstructionEncoding.VEX or InstructionEncoding.EVEX or InstructionEncoding.MVEX or InstructionEncoding.XOP)
                {
                    initializerListWriter.WriteFieldDesignation("op_ndsndd").WriteExpression(GetRegisterConstraint(definition.Operands, OperandEncoding.NDSNDD));
                }

                if (encoding is InstructionEncoding.VEX or InstructionEncoding.EVEX or InstructionEncoding.MVEX)
                {
                    initializerListWriter
                        .WriteFieldDesignation("is_gather").WriteBool(definition.Flags.HasFlag(InstructionFlagsEnum.IsGather))
                        .WriteFieldDesignation("no_source_dest_match").WriteBool(definition.Flags.HasFlag(InstructionFlagsEnum.NoSourceDestMatch))
                        .WriteFieldDesignation("no_source_source_match").WriteBool(definition.Flags.HasFlag(InstructionFlagsEnum.NoSourceSourceMatch));
                }

                switch (encoding)
                {
                    case InstructionEncoding.Default:
                        initializerListWriter
                            .Conditional().WriteFieldDesignation("is_privileged").WriteBool(definition.Cpl is 0)
                            .WriteFieldDesignation("accepts_LOCK").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsLOCK))
                            .Conditional().WriteFieldDesignation("accepts_REP").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsREP))
                            .Conditional().WriteFieldDesignation("accepts_REPEREPZ").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsREPEREPZ))
                            .Conditional().WriteFieldDesignation("accepts_REPNEREPNZ").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsREPNEREPNZ))
                            .Conditional().WriteFieldDesignation("accepts_BOUND").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsBOUND))
                            .Conditional().WriteFieldDesignation("accepts_XACQUIRE").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsXACQUIRE))
                            .Conditional().WriteFieldDesignation("accepts_XRELEASE").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsXRELEASE))
                            .Conditional().WriteFieldDesignation("accepts_NOTRACK").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsNOTRACK))
                            .Conditional().WriteFieldDesignation("accepts_hle_without_lock").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsLocklessHLE))
                            .Conditional().WriteFieldDesignation("accepts_branch_hints").WriteBool(definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsBranchHints));
                        break;

                    case InstructionEncoding.VEX:
                        initializerListWriter
                            .Conditional().WriteFieldDesignation("broadcast").WriteExpression("ZYDIS_VEX_STATIC_BROADCAST_{0}", (definition.Vex?.StaticBroadcast ?? StaticBroadcast.None).ToZydisString());
                        break;

                    case InstructionEncoding.EVEX:
                        initializerListWriter
                            .Conditional().WriteFieldDesignation("vector_length").WriteExpression("ZYDIS_IVECTOR_LENGTH_{0}", definition.Evex!.VectorLength.ToZydisString())
                            .Conditional().WriteFieldDesignation("tuple_type").WriteExpression("ZYDIS_TUPLETYPE_{0}", definition.Evex!.TupleType.ToZydisString())
                            .Conditional().WriteFieldDesignation("element_size").WriteExpression("ZYDIS_IELEMENT_SIZE_{0}", definition.Evex!.ElementSize.ToZydisString())
                            .Conditional().WriteFieldDesignation("functionality").WriteExpression("ZYDIS_EVEX_FUNC_{0}", definition.Evex!.Functionality.ToZydisString())
                            .WriteFieldDesignation("mask_policy").WriteExpression("ZYDIS_MASK_POLICY_{0}", definition.Evex!.MaskMode.ToZydisString())
                            .WriteFieldDesignation("accepts_zero_mask").WriteBool(definition.Evex!.MaskFlags?.HasFlag(EvexMaskFlags.AcceptsZeroMask) ?? true)
                            .Conditional().WriteFieldDesignation("mask_override").WriteExpression("ZYDIS_MASK_OVERRIDE_{0}", GetMaskOverride(definition))
                            .Conditional().WriteFieldDesignation("broadcast").WriteExpression("ZYDIS_EVEX_STATIC_BROADCAST_{0}", definition.Evex!.StaticBroadcast.ToZydisString())
                            .WriteFieldDesignation("is_eevex").WriteBool(definition.Evex!.IsEevex)
                            .WriteFieldDesignation("has_apx_nf").WriteBool(definition.Evex!.HasNf)
                            .WriteFieldDesignation("has_apx_zu").WriteBool(definition.Evex!.HasZu)
                            .WriteFieldDesignation("has_apx_ppx").WriteBool(definition.Evex!.HasPpx);
                        break;

                    case InstructionEncoding.MVEX:
                        initializerListWriter
                            .WriteFieldDesignation("functionality").WriteExpression("ZYDIS_MVEX_FUNC_{0}", definition.Mvex!.Functionality.ToZydisString())
                            .WriteFieldDesignation("mask_policy").WriteExpression("ZYDIS_MASK_POLICY_{0}", definition.Mvex!.MaskMode.ToZydisString())
                            .Conditional().WriteFieldDesignation("has_element_granularity").WriteBool(definition.Mvex!.HasElementGranularity)
                            .Conditional().WriteFieldDesignation("broadcast").WriteExpression("ZYDIS_MVEX_STATIC_BROADCAST_{0}", definition.Mvex!.StaticBroadcast.ToZydisString());
                        break;
                }

                initializerListWriter.EndList();
            }

            initializerListWriter.EndList();
            declarationWriter.EndDeclaration();
            await writer.WriteLineAsync().ConfigureAwait(false);
        }

        return;

        static string GetBranchType(InstructionDefinition definition)
        {
            if (definition.Flags.HasFlag(InstructionFlagsEnum.IsFarBranch))
            {
                return "FAR";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.IsNearBranch))
            {
                return "NEAR";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.IsShortBranch))
            {
                return "SHORT";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.IsAbsBranch))
            {
                return "ABSOLUTE";
            }

            return "NONE";
        }

        static string GetRegisterConstraint(IReadOnlyList<InstructionOperand>? operands, OperandEncoding encoding)
        {
            foreach (var operand in operands ?? [])
            {
                if (operand.Encoding != encoding)
                {
                    continue;
                }

                return operand.Type switch
                {
                    OperandType.GPR8 or
                    OperandType.GPR16 or
                    OperandType.GPR32 or
                    OperandType.GPR64 or
                    OperandType.GPR16_32_64 or
                    OperandType.GPR32_32_64 or
                    OperandType.GPR16_32_32 or 
                    OperandType.GPRASZ => "ZYDIS_REGKIND_GPR",
                    OperandType.FPR => "ZYDIS_REGKIND_X87",
                    OperandType.MMX => "ZYDIS_REGKIND_MMX",
                    OperandType.XMM or
                    OperandType.YMM or 
                    OperandType.ZMM => "ZYDIS_REGKIND_VR",
                    OperandType.TMM => "ZYDIS_REGKIND_TMM",
                    OperandType.BND => "ZYDIS_REGKIND_BOUND",
                    OperandType.SREG => "ZYDIS_REGKIND_SEGMENT" + ((operand.Access is OperandAccess.Write) ? " | (1 << 4)" : null),
                    OperandType.CR => "ZYDIS_REGKIND_CONTROL",
                    OperandType.DR => "ZYDIS_REGKIND_DEBUG",
                    OperandType.MASK => "ZYDIS_REGKIND_MASK",
                    OperandType.MEM => "ZYDIS_MEMOP_TYPE_MEM",
                    OperandType.MEMVSIBX or
                    OperandType.MEMVSIBY or
                    OperandType.MEMVSIBZ => "ZYDIS_MEMOP_TYPE_VSIB | (1 << 3)",
                    OperandType.AGEN => "ZYDIS_MEMOP_TYPE_AGEN",
                    OperandType.AGENNoRel => "ZYDIS_MEMOP_TYPE_AGEN | (1 << 3)",
                    OperandType.MIB => "ZYDIS_MEMOP_TYPE_MIB",
                    OperandType.DFV => "ZYDIS_REGKIND_DFV",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return "0";
        }

        static string GetCpuState(InstructionDefinition definition)
        {
            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateCPU_CR) &&
                definition.Flags.HasFlag(InstructionFlagsEnum.StateCPU_CW))
            {
                return "READWRITE";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateCPU_CR))
            {
                return "READ";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateCPU_CW))
            {
                return "WRITE";
            }

            return "NONE";
        }

        static string GetFpuState(InstructionDefinition definition)
        {
            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateFPU_CR) &&
                definition.Flags.HasFlag(InstructionFlagsEnum.StateFPU_CW))
            {
                return "READWRITE";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateFPU_CR))
            {
                return "READ";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateFPU_CW))
            {
                return "WRITE";
            }

            return "NONE";
        }

        static string GetXmmState(InstructionDefinition definition)
        {
            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateXMM_CR) &&
                definition.Flags.HasFlag(InstructionFlagsEnum.StateXMM_CW))
            {
                return "READWRITE";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateXMM_CR))
            {
                return "READ";
            }

            if (definition.Flags.HasFlag(InstructionFlagsEnum.StateXMM_CW))
            {
                return "WRITE";
            }

            return "NONE";
        }

        static bool AcceptsSegment(InstructionDefinition definition)
        {
            foreach (var operand in definition.Operands ?? [])
            {
                if (operand.Type is not (OperandType.ImplicitMem or OperandType.MEM or OperandType.MEMVSIBX
                    or OperandType.MEMVSIBY or OperandType.MEMVSIBZ or OperandType.PTR or OperandType.MOFFS
                    or OperandType.MIB))
                {
                    continue;
                }

                if (operand.IgnoreSegmentOverride)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        static string GetMaskOverride(InstructionDefinition definition)
        {
            if (definition.Evex!.MaskFlags?.HasFlag(EvexMaskFlags.IsControlMask) ?? false)
            {
                return "CONTROL";
            }

            if (definition.Evex!.MaskFlags?.HasFlag(EvexMaskFlags.ForceZeroMask) ?? false)
            {
                return "ZEROING";
            }

            return "DEFAULT";
        }
    }
}

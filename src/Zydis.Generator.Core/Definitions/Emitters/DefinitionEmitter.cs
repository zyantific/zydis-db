using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Enums;

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
        string[] baseVectorFields = ["op_ndsndd", "is_gather", "no_source_dest_match", "no_source_source_match"];

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

            var definitionWriter = declarationWriter
                .BeginDeclaration("static const", $"ZydisInstructionDefinition{encodingName}", $"ISTR_DEFINITIONS_{encodingName}[]")
                .WriteInitializerList()
                .BeginList();
            var fieldList = ObjectDeclaration<InstructionDefinition>.GetFieldNames();
            string[] extraFields = encoding switch
            {
                InstructionEncoding.Default => [
                    "is_privileged",
                    "accepts_LOCK",
                    "accepts_REP",
                    "accepts_REPEREPZ",
                    "accepts_REPNEREPNZ",
                    "accepts_BOUND",
                    "accepts_XACQUIRE",
                    "accepts_XRELEASE",
                    "accepts_NOTRACK",
                    "accepts_hle_without_lock",
                    "accepts_branch_hints"],
                InstructionEncoding.VEX => [.. baseVectorFields, "broadcast"],
                InstructionEncoding.EVEX => [
                    .. baseVectorFields,
                    "vector_length",
                    "tuple_type",
                    "element_size",
                    "functionality",
                    "mask_policy",
                    "accepts_zero_mask",
                    "mask_override",
                    "broadcast",
                    "is_eevex",
                    "has_apx_nf",
                    "has_apx_zu",
                    "has_apx_ppx"],
                InstructionEncoding.MVEX => [
                    .. baseVectorFields,
                    "functionality",
                    "mask_policy",
                    "has_element_granularity",
                    "broadcast"],
                InstructionEncoding.XOP => ["op_ndsndd"],
                _ => [],
            };
            var definitionDeclaration = new SimpleObjectDeclaration([.. fieldList, .. extraFields]);

            foreach (var definition in definitions)
            {
                var definitionEntry = definitionWriter.CreateObjectWriter(definitionDeclaration)
                    .WriteExpression("mnemonic", "ZYDIS_MNEMONIC_{0}", definition.Mnemonic.ToUpperInvariant())
                    .Conditional().WriteInteger("operand_count", definition.NumberOfOperands)
                    .Conditional().WriteInteger("operand_count_visible", definition.NumberOfVisibleOperands)
                    .Conditional().WriteInteger("operand_reference", operandsRegistry.GetOperandsIndex(definition), 4, true)
                    .WriteInteger("operand_size_map", (int)definition.OpsizeMap)
                    .WriteInteger("address_size_map", (int)definition.AdsizeMap)
                    .Conditional().WriteInteger("flags_reference", accessedFlagsRegistry.GetAccessedFlagsId(definition), 2, true)
                    .WriteBool("requires_protected_mode", definition.Flags.HasFlag(InstructionFlagsEnum.ProtectedMode))
                    .WriteBool("no_compat_mode", definition.Flags.HasFlag(InstructionFlagsEnum.NoCompatMode))
                    .Conditional().WriteExpression("category", "ZYDIS_CATEGORY_{0}", definition.MetaInfo.Category.ToUpperInvariant())
                    .Conditional().WriteExpression("isa_set", "ZYDIS_ISA_SET_{0}", definition.MetaInfo.IsaSet.ToUpperInvariant())
                    .Conditional().WriteExpression("isa_ext", "ZYDIS_ISA_EXT_{0}", definition.MetaInfo.IsaExtension.ToUpperInvariant())
                    .Conditional().WriteExpression("branch_type", definition.GetBranchType().ToZydisString())
                    .Conditional().WriteExpression("exception_class", "ZYDIS_EXCEPTION_CLASS_{0}", (definition.ExceptionClass ?? ExceptionClass.None).ToZydisString())
                    .WriteExpression("op_reg", GetRegisterConstraint(definition.Operands, OperandEncoding.ModrmReg))
                    .WriteExpression("op_rm", GetRegisterConstraint(definition.Operands, OperandEncoding.ModrmRm))
                    .Conditional().WriteExpression("cpu_state", "ZYDIS_RW_ACTION_{0}", GetCpuState(definition))
                    .Conditional().WriteExpression("fpu_state", "ZYDIS_RW_ACTION_{0}", GetFpuState(definition))
                    .Conditional().WriteExpression("xmm_state", "ZYDIS_RW_ACTION_{0}", GetXmmState(definition))
                    .Conditional().WriteBool("accepts_segment", AcceptsSegment(definition));

                if (encoding is InstructionEncoding.VEX or InstructionEncoding.EVEX or InstructionEncoding.MVEX or InstructionEncoding.XOP)
                {
                    definitionEntry.WriteExpression("op_ndsndd", GetRegisterConstraint(definition.Operands, OperandEncoding.NDSNDD));
                }

                if (encoding is InstructionEncoding.VEX or InstructionEncoding.EVEX or InstructionEncoding.MVEX)
                {
                    definitionEntry
                        .WriteBool("is_gather", definition.Flags.HasFlag(InstructionFlagsEnum.IsGather))
                        .WriteBool("no_source_dest_match", definition.Flags.HasFlag(InstructionFlagsEnum.NoSourceDestMatch))
                        .WriteBool("no_source_source_match", definition.Flags.HasFlag(InstructionFlagsEnum.NoSourceSourceMatch));
                }

                switch (encoding)
                {
                    case InstructionEncoding.Default:
                        definitionEntry
                            .Conditional().WriteBool("is_privileged", definition.Cpl is 0)
                            .WriteBool("accepts_LOCK", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsLOCK))
                            .Conditional().WriteBool("accepts_REP", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsREP))
                            .Conditional().WriteBool("accepts_REPEREPZ", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsREPEREPZ))
                            .Conditional().WriteBool("accepts_REPNEREPNZ", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsREPNEREPNZ))
                            .Conditional().WriteBool("accepts_BOUND", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsBOUND))
                            .Conditional().WriteBool("accepts_XACQUIRE", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsXACQUIRE))
                            .Conditional().WriteBool("accepts_XRELEASE", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsXRELEASE))
                            .Conditional().WriteBool("accepts_NOTRACK", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsNOTRACK))
                            .Conditional().WriteBool("accepts_hle_without_lock", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsLocklessHLE))
                            .Conditional().WriteBool("accepts_branch_hints", definition.PrefixFlags.HasFlag(PrefixFlags.AcceptsBranchHints));
                        break;
                    case InstructionEncoding.VEX:
                        definitionEntry
                            .Conditional().WriteExpression("broadcast", "ZYDIS_VEX_STATIC_BROADCAST_{0}", (definition.Vex?.StaticBroadcast ?? StaticBroadcast.None).ToZydisString());
                        break;
                    case InstructionEncoding.EVEX:
                        definitionEntry
                            .Conditional().WriteExpression("vector_length", "ZYDIS_IVECTOR_LENGTH_{0}", definition.Evex!.VectorLength.ToZydisString())
                            .Conditional().WriteExpression("tuple_type", definition.Evex!.TupleType.ToZydisString())
                            .Conditional().WriteExpression("element_size", definition.Evex!.ElementSize.ToZydisString())
                            .Conditional().WriteExpression("functionality", definition.Evex!.Functionality.ToZydisString())
                            .WriteExpression("mask_policy", definition.Evex!.MaskMode.ToZydisString())
                            .WriteBool("accepts_zero_mask", definition.Evex!.MaskFlags?.HasFlag(EvexMaskFlags.AcceptsZeroMask) ?? true)
                            .Conditional().WriteExpression("mask_override", "ZYDIS_MASK_OVERRIDE_{0}", GetMaskOverride(definition))
                            .Conditional().WriteExpression("broadcast", "ZYDIS_EVEX_STATIC_BROADCAST_{0}", definition.Evex!.StaticBroadcast.ToZydisString())
                            .WriteBool("is_eevex", definition.Evex!.IsEevex)
                            .WriteBool("has_apx_nf", definition.Evex!.HasNf)
                            .WriteBool("has_apx_zu", definition.Evex!.HasZu)
                            .WriteBool("has_apx_ppx", definition.Evex!.HasPpx);
                        break;
                    case InstructionEncoding.MVEX:
                        definitionEntry
                            .WriteExpression("functionality", "ZYDIS_MVEX_FUNC_{0}", definition.Mvex!.Functionality.ToZydisString())
                            .WriteExpression("mask_policy", definition.Mvex!.MaskMode.ToZydisString())
                            .Conditional().WriteBool("has_element_granularity", definition.Mvex!.HasElementGranularity)
                            .Conditional().WriteExpression("broadcast", "ZYDIS_MVEX_STATIC_BROADCAST_{0}", definition.Mvex!.StaticBroadcast.ToZydisString());
                        break;
                }

                definitionWriter.WriteObject(definitionEntry);
            }

            definitionWriter.EndList();
            declarationWriter.EndDeclaration();
            await writer.WriteLineAsync().ConfigureAwait(false);
        }
    }

    private static string GetRegisterConstraint(IReadOnlyList<InstructionOperand>? operands, OperandEncoding encoding)
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
                OperandType.MIB => "ZYDIS_MEMOP_TYPE_MIB | (1 << 3)",
                OperandType.DFV => "ZYDIS_REGKIND_DFV",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        return "0";
    }
    private static string GetCpuState(InstructionDefinition definition)
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

    private static string GetFpuState(InstructionDefinition definition)
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

    private static string GetXmmState(InstructionDefinition definition)
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

    private static bool AcceptsSegment(InstructionDefinition definition)
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

    private static string GetMaskOverride(InstructionDefinition definition)
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

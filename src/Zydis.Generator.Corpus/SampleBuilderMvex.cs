using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Builds the samples of the MVEX encoding of Knights Corner: <c>62</c> followed by the payload bytes P0, P1 and P2,
/// see <c>ZydisDecodeMVEX</c> in Zydis <c>src/Decoder.c</c>. P0 carries the inverted R, X, B and R' bits and the map,
/// P1 carries W, the inverted vvvv and pp, and P2 carries E, SSS, the inverted V' and kkk.
/// </summary>
internal static class SampleBuilderMvex
{
    private const string MvexEAxis = "mvex_e";

    private const byte MvexPrefix = 0x62;
    private const byte AddressSizePrefix = 0x67;

    // R, X, B and R' are stored inverted, and every canonical register index is below 8, so the four bits are set.
    private const byte P0ExtensionBits = 0xF0;

    // The inverted V' in bit 3 of P2, set because no register index reaches 16.
    private const int InvertedV4 = 0x08;

    // ZydisDecodeMVEX fixes the vector length of every MVEX instruction at 512 bits.
    private const string VectorLength512 = "512";

    // E 0 comes first, the canonical default.
    private static readonly string[] ESlots = ["0", "1"];

    public static SampleBuildResult BuildMvex(DecoderTreePath path, ModeNode.Slot mode, int id, string formId)
    {
        var steps = path.Steps;
        var definition = path.Definition;
        var mapTag = CorpusTags.MapTag(path.Map);

        if (SampleBuilder.FindArchitecturalConflict(steps, mode, hasRexPrefixes: false) is { } impossible)
        {
            return new Impossible(impossible);
        }

        if (steps.FirstOrDefault(step => !IsMvexStep(step)) is { } foreign)
        {
            return new Unencodable($"{DecoderTreePath.FormatStep(foreign)} does not apply to the mvex encoding");
        }

        if (definition.Mvex is not { } mvex)
        {
            return new Unencodable("the definition has no mvex info");
        }

        if (MapField(path.Map) is not { } mmmm)
        {
            return new Unencodable($"mvex has no {mapTag} table");
        }

        if (path.Prefix is not { } refiningPrefix)
        {
            return new Unencodable($"the mvex table of {mapTag} has no refining prefix");
        }

        if (!SampleBuilder.TrySelectPrefixState(steps, mode, SampleBuilder.PayloadPrefixStates, out var prefixes,
                out var prefixFailure))
        {
            return new Unencodable(prefixFailure);
        }

        if (prefixes.OperandSize66)
        {
            var operandSize = steps.First(step => step.Node == SampleBuilder.OperandSizeAxis);
            return new Unencodable(
                $"{DecoderTreePath.FormatStep(operandSize)} needs a 66 prefix, which mvex does not allow");
        }

        if (steps.FirstOrDefault(step => (step.Node == SampleBuilder.VectorLengthAxis) &&
                !step.Slots.Contains(VectorLength512)) is { } vectorLength)
        {
            return new Unencodable(
                $"{DecoderTreePath.FormatStep(vectorLength)} has no mvex encoding, whose vector length is 512");
        }

        var (osz, asz) = EffectiveSizes.Compute(mode, operandSizePrefix: false, prefixes.AddressSize67, prefixes.W,
            definition.OpsizeMap, definition.AdsizeMap);
        var operandWidth = EffectiveSizes.ComputeOperandWidth(mode, operandSizePrefix: false, prefixes.W,
            definition.OpsizeMap);

        var hasVsib = (definition.Operands ?? []).Any(operand => operand.Type is
            OperandType.MEMVSIBX or OperandType.MEMVSIBY or OperandType.MEMVSIBZ);

        var registers = SampleBuilder.AssignRegisters(definition);

        // The payload keeps every register extension bit clear, so an index of 8 or above would lose its high bits.
        int?[] indices = [registers.Reg, registers.Rm, registers.Vvvv, hasVsib ? registers.VsibIndex : null];
        if (indices.FirstOrDefault(index => index >= 8) is { } wideIndex)
        {
            return new Unencodable(
                $"register index {wideIndex} needs an extension bit, which the mvex builder keeps clear");
        }

        var hasModrmEncoding = SampleBuilder.HasModrmEncoding(definition);
        var hasModrmStep = steps.Any(step => step.Node is SampleBuilder.ModrmModAxis or
            SampleBuilder.ModrmModCompactAxis or SampleBuilder.ModrmRegAxis or SampleBuilder.ModrmRmAxis);

        SampleBuilder.Modrm? modrm = null;
        if (hasModrmEncoding || hasModrmStep)
        {
            if (!SampleBuilder.TryChooseModrm(steps, definition, registers, asz, out var chosen,
                    out var modrmFailure))
            {
                return new Unencodable(modrmFailure);
            }

            modrm = chosen;
        }

        // The decoder reads the VSIB index from a SIB byte, which needs rm=4 and a memory form.
        if (hasVsib && modrm is not { Mod: not 3, Rm: 4 })
        {
            return new Unencodable("the ModRM steps exclude the SIB form a VSIB operand needs");
        }

        // The mvex_e filter routes by E, and on a memory form the decoder also reads it as the eviction hint. The
        // decoder rejects neither value on either form.
        var eSteps = steps.Where(step => step.Node == MvexEAxis).ToArray();
        if (ESlots.FirstOrDefault(slot => eSteps.All(step => step.Slots.Contains(slot))) is not { } e)
        {
            return new Unencodable(
                $"{string.Join(", ", eSteps.Select(DecoderTreePath.FormatStep))} leave no value for E");
        }

        // SSS 0 is the plain form of every functionality, with no swizzle, conversion or broadcast, and
        // ZydisCheckErrorConditions accepts it for each one. The rounding forms read it as round to nearest.
        const int sss = 0;

        // MVEX has no control mask, so only a required mask needs k1. k0 means no masking.
        var kkk = (mvex.MaskMode is MaskMode.Required) ? 1 : 0;

        var p0 = (byte)(P0ExtensionBits | mmmm);
        // ZydisDecodeMVEX rejects a set bit 2 of P1, where EVEX keeps U, with ZYDIS_STATUS_MALFORMED_MVEX. The choice
        // between EVEX and MVEX comes from the knc decoder mode alone.
        var p1 = (byte)(((prefixes.W ? 1 : 0) << 7) | ((~(registers.Vvvv ?? 0) & 0x0F) << 3) |
            SampleBuilder.PpField(refiningPrefix));
        var p2 = (byte)((int.Parse(e, CultureInfo.InvariantCulture) << 7) | (sss << 4) | InvertedV4 | kkk);

        var bytes = new List<byte>();
        if (prefixes.AddressSize67)
        {
            bytes.Add(AddressSizePrefix);
        }

        bytes.Add(MvexPrefix);
        bytes.Add(p0);
        bytes.Add(p1);
        bytes.Add(p2);
        bytes.Add(path.Opcode);

        if (modrm is { } fields)
        {
            var readsAddressing = hasModrmEncoding && !definition.Flags.HasFlag(InstructionFlagsEnum.ForceRegForm);
            SampleBuilder.AppendModrm(bytes, fields, asz, readsAddressing, hasVsib ? registers.VsibIndex : null);
        }

        if (!SampleBuilder.TryAppendImmediates(bytes, definition, operandWidth, asz, out var immediateFailure))
        {
            return new Unencodable(immediateFailure);
        }

        var decoderModes = SampleBuilder.GetDecoderModes(steps, hasRex2: false, requiresKnc: true);

        return new Built(SampleBuilder.CreateSample(path, mode, bytes, decoderModes, osz, asz, id, formId));
    }

    /// <summary>
    /// The mmmm field of P0. <c>ZydisDecodeMVEX</c> rejects every map above 3, and the KNC instruction set has no
    /// map 0 instruction.
    /// </summary>
    private static byte? MapField(OpcodeMap map)
    {
        return map switch
        {
            OpcodeMap.M0F => 0x01,
            OpcodeMap.M0F38 => 0x02,
            OpcodeMap.M0F3A => 0x03,
            _ => null
        };
    }

    private static bool IsMvexStep(PathStep step)
    {
        return step.Node.StartsWith(SampleBuilder.FeatureAxisPrefix, StringComparison.Ordinal) || step.Node is
            SampleBuilder.AddressSizeAxis or
            SampleBuilder.ModeAxis or
            SampleBuilder.ModeCompactAxis or
            SampleBuilder.ModrmModAxis or
            SampleBuilder.ModrmModCompactAxis or
            SampleBuilder.ModrmRegAxis or
            SampleBuilder.ModrmRmAxis or
            MvexEAxis or
            SampleBuilder.OperandSizeAxis or
            SampleBuilder.RexWAxis or
            SampleBuilder.VectorLengthAxis;
    }
}

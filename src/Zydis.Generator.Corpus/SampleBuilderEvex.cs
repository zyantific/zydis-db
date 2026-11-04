using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Builds the samples of the EVEX encoding: <c>62</c> followed by the payload bytes P0, P1 and P2, see
/// <c>ZydisDecodeEVEX</c> in Zydis <c>src/Decoder.c</c>. P0 carries the inverted R, X, B and R' bits, B4 and the map,
/// P1 carries W, the inverted vvvv, U and pp, and P2 carries z, L'L, b, the inverted V' and aaa. APX reads b as ND,
/// bit 2 of aaa as NF, the low four bits of P2 as SCC and, on a memory form, U as the inverted X4.
/// </summary>
internal static class SampleBuilderEvex
{
    private const string EvexBAxis = "evex_b";
    private const string EvexNdAxis = "evex_nd";
    private const string EvexNfAxis = "evex_nf";
    private const string EvexSccAxis = "evex_scc";

    internal const byte EvexPrefix = 0x62;

    // R, X, B and R' are stored inverted and B4 as is. Every canonical register index is below 8, so the four
    // inverted bits are set and B4 stays clear, which the decoder also requires with APX off. Both top bits set also
    // keep the decoder from reading 62 as BOUND outside 64-bit mode.
    internal const byte P0ExtensionBits = 0xF0;

    // The inverted V' in bit 3 of P2, set because no register index reaches 16.
    internal const int InvertedV4 = 0x08;

    // The NF bit is bit 2 of aaa.
    private const int NfBit = 0x04;

    // Each list starts with the canonical default: U is 1, the vector length 128 and every other field 0.
    private static readonly string[] BitSlots = ["0", "1"];
    private static readonly string[] USlots = ["1", "0"];
    private static readonly string[] VectorLengthSlots = ["128", "256", "512"];

    public static SampleBuildResult BuildEvex(DecoderTreePath path, ModeNode.Slot mode)
    {
        var steps = path.Steps;
        var definition = path.Definition;
        var mapTag = CorpusTags.MapTag(path.Map);

        if (steps.FirstOrDefault(step => !IsEvexStep(step)) is { } foreign)
        {
            return new Unencodable($"{DecoderTreePath.FormatStep(foreign)} does not apply to the evex encoding");
        }

        if (definition.Evex is not { } evex)
        {
            return new Unencodable("the definition has no evex info");
        }

        if (MapField(path.Map) is not { } mmm)
        {
            return new Unencodable($"evex has no {mapTag} table");
        }

        if (!SampleBuilder.TrySelectPayloadPrefixes(path, mode, out var refiningPrefix, out var prefixes,
                out var prefixFailure))
        {
            return new Unencodable(prefixFailure);
        }

        // On a register form with b=1 the decoder reads L'L as the rounding control, which an open vector_length axis
        // leaves at 00.
        if (!SampleBuilder.TryChooseSlot(steps, [SampleBuilder.VectorLengthAxis], VectorLengthSlots, "evex",
                out var vectorLength, out var vectorLengthFailure))
        {
            return new Unencodable(vectorLengthFailure);
        }

        var ll = Array.IndexOf(VectorLengthSlots, vectorLength);

        // ZydisSetEffectiveOperandWidth reads the 66 table of map 4 as an operand-size override.
        var operandSizePrefix = (path.Map is OpcodeMap.MAP4) && (refiningPrefix is RefiningPrefix.P66);

        var (osz, asz, operandWidth) = EffectiveSizes.Compute(mode, operandSizePrefix, prefixes.AddressSize67,
            prefixes.W, definition.OpsizeMap, definition.AdsizeMap);

        var registers = SampleBuilder.AssignRegisters(definition);
        if ((registers.VsibIndex is not null) && (asz == 16))
        {
            return new Unencodable("a VSIB operand has no 16-bit address form");
        }

        // The payload keeps every register extension bit clear, so an index of 8 or above would lose its high bits.
        if (registers.WideIndex is { } wideIndex)
        {
            return new Unencodable(
                $"register index {wideIndex} needs an extension bit, which the evex builder keeps clear");
        }

        if (!SampleBuilder.TryChooseModrm(steps, definition, registers, asz, forcesModrm: false, out var modrm,
                out var modrmFailure))
        {
            return new Unencodable(modrmFailure);
        }

        // b and ND share bit 4 of P2.
        if (!SampleBuilder.TryChooseSlot(steps, [EvexBAxis, EvexNdAxis], BitSlots, "evex", out var bit4,
                out var bit4Failure))
        {
            return new Unencodable(bit4Failure);
        }

        if (!SampleBuilder.TryChooseSlot(steps, [SampleBuilder.EvexUAxis], USlots, "evex", out var u, out var uFailure))
        {
            return new Unencodable(uFailure);
        }

        if (!TryChooseLowNibble(steps, evex, out var lowNibble, out var lowNibbleFailure))
        {
            return new Unencodable(lowNibbleFailure);
        }

        // ZydisNodeHandlerEvexU routes every memory form as U=1 under APX, so only APX off reaches a U=0 memory leaf.
        var forbidsApx = (u == "0") && modrm is { Mod: not 3 };

        // Maps 4 and 7 are the APX maps. Maps 5 and 6 hold AVX512-FP16 and AVX10 forms, which decode without APX.
        var requiresApx = evex.IsEevex || (path.Map is OpcodeMap.MAP4 or OpcodeMap.MAP7);

        if (forbidsApx && requiresApx)
        {
            var uStep = steps.First(step => step.Node == SampleBuilder.EvexUAxis);
            var apxForm = evex.IsEevex ? "is_eevex" : mapTag;
            return new Unencodable($"{DecoderTreePath.FormatStep(uStep)} on a memory form needs apx off, " +
                $"while the {apxForm} definition needs apx on");
        }

        // The decoder reads the vvvv bits of a definition with a default flags value as that value, not inverted, so
        // 0000 is DFV 0.
        var vvvv = evex.HasDfv ? 0 : SampleBuilder.InvertedVvvv(registers.Vvvv);

        var p0 = (byte)(P0ExtensionBits | mmm);
        var p1 = SampleBuilder.WvvvvPpByte(prefixes.W, vvvv, int.Parse(u, CultureInfo.InvariantCulture),
            refiningPrefix);
        // z stays 0. A force_zero_mask definition gets the zeroing mode without it, and a definition without
        // accepts_zero_mask rejects it.
        var p2 = (byte)((ll << 5) | (int.Parse(bit4, CultureInfo.InvariantCulture) << 4) | lowNibble);

        if ((mode is not ModeNode.Slot.M64) && ((p2 & InvertedV4) == 0))
        {
            return new Unencodable($"the evex payload byte {p2:X2} clears V', which the decoder rejects in " +
                $"{CorpusTags.ModeTag(mode)}-bit mode");
        }

        var bytes = new List<byte>();
        if (prefixes.AddressSize67)
        {
            bytes.Add(SampleBuilder.AddressSizePrefix);
        }

        bytes.Add(EvexPrefix);
        bytes.Add(p0);
        bytes.Add(p1);
        bytes.Add(p2);
        bytes.Add(path.Opcode);

        if (modrm is { } fields)
        {
            SampleBuilder.AppendModrm(bytes, fields, asz, registers.VsibIndex);
        }

        if (!SampleBuilder.TryAppendImmediates(bytes, definition, operandWidth, asz, out var immediateFailure))
        {
            return new Unencodable(immediateFailure);
        }

        var apx = forbidsApx
            ? SampleBuilder.ApxRequirement.Forbidden
            : requiresApx ? SampleBuilder.ApxRequirement.Required : SampleBuilder.ApxRequirement.None;
        var decoderModes = SampleBuilder.GetDecoderModes(steps, apx);

        return new Built(bytes, decoderModes, osz, asz);
    }

    /// <summary>
    /// Chooses the low four bits of P2, the inverted V' and aaa, which APX reads as SCC, with NF in bit 2. Without an
    /// <c>evex_scc</c> step the inverted V' is set and aaa holds the mask register: <c>k1</c> when the definition
    /// requires a mask or uses it as a control mask, else <c>k0</c>, which means no masking.
    /// </summary>
    private static bool TryChooseLowNibble(IReadOnlyList<PathStep> steps, InstructionEvexInfo evex, out int lowNibble,
        out string failure)
    {
        lowNibble = 0;

        var mask = ((evex.MaskMode is MaskMode.Required) ||
            (evex.MaskFlags?.HasFlag(EvexMaskFlags.IsControlMask) ?? false)) ? 1 : 0;

        var nfSteps = steps.Where(step => step.Node == EvexNfAxis).ToArray();
        var sccSteps = steps.Where(step => step.Node == EvexSccAxis).ToArray();

        // ZydisCheckErrorConditions reads these bits as NF or SCC for such a definition and clears the mask, so a
        // required k1 has no encoding there.
        if ((mask != 0) && ((nfSteps.Length != 0) || (sccSteps.Length != 0)))
        {
            var apxStep = nfSteps.Concat(sccSteps).First();
            failure = $"the definition needs the mask k1, which its {apxStep.Node} field replaces";
            return false;
        }

        int[] candidates = (sccSteps.Length != 0)
            ? [.. Enumerable.Range(0, 16)]
            : [InvertedV4 | mask, InvertedV4 | NfBit | mask];

        var index = Array.FindIndex(candidates, value =>
            sccSteps.All(step => step.Slots.Contains(Slot(value))) &&
            nfSteps.All(step => step.Slots.Contains(Slot((value & NfBit) >> 2))));

        if (index < 0)
        {
            failure = $"{string.Join(", ", nfSteps.Concat(sccSteps).Select(DecoderTreePath.FormatStep))} leave no " +
                "value for the low bits of P2";
            return false;
        }

        lowNibble = candidates[index];
        failure = string.Empty;
        return true;

        static string Slot(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The map field of P0. <c>ZydisDecodeEVEX</c> rejects map 0.
    /// </summary>
    private static byte? MapField(OpcodeMap map)
    {
        return map switch
        {
            OpcodeMap.M0F => 0x01,
            OpcodeMap.M0F38 => 0x02,
            OpcodeMap.M0F3A => 0x03,
            OpcodeMap.MAP4 => 0x04,
            OpcodeMap.MAP5 => 0x05,
            OpcodeMap.MAP6 => 0x06,
            OpcodeMap.MAP7 => 0x07,
            _ => null
        };
    }

    private static bool IsEvexStep(PathStep step)
    {
        return SampleBuilder.IsPayloadStep(step) || step.Node is
            EvexBAxis or
            EvexNdAxis or
            EvexNfAxis or
            EvexSccAxis or
            SampleBuilder.EvexUAxis;
    }
}

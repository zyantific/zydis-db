using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Builds the samples of the VEX encoding, in the three-byte form, and of the XOP encoding. Both prefixes carry the
/// inverted R, X and B bits and the map in the first payload byte, and W, the inverted vvvv, L and pp in the second,
/// see <c>ZydisDecodeVEX</c> and <c>ZydisDecodeXOP</c> in Zydis <c>src/Decoder.c</c>.
/// </summary>
internal static class SampleBuilderVex
{
    private const byte VexPrefix = 0xC4;
    private const byte XopPrefix = 0x8F;
    private const byte AddressSizePrefix = 0x67;

    // R, X and B are stored inverted, and every canonical register index is below 8, so all three bits are set.
    private const byte InvertedRxb = 0xE0;

    private static readonly bool[] Booleans = [false, true];

    // A 66, F2 or F3 before C4 or 8F makes the decoder fail with ZYDIS_STATUS_ILLEGAL_LEGACY_PFX, so the states with a
    // 66 come last and only serve to name the operand_size step that needs one. The pp field replaces the refining
    // prefix bytes.
    private static readonly SampleBuilder.PrefixState[] PrefixStates =
    [
        .. from operandSize66 in Booleans
           from addressSize67 in Booleans
           from w in Booleans
           select new SampleBuilder.PrefixState(0x00, operandSize66, addressSize67, w, B: false, Rex2: false)
    ];

    public static SampleBuildResult BuildVex(DecoderTreePath path, ModeNode.Slot mode, int id, string formId)
    {
        // ZydisDecodeVEX accepts map 0 only in a build with KNC support, for the KNC short branches.
        byte? mmmmm = path.Map switch
        {
            OpcodeMap.MAP0 => 0x00,
            OpcodeMap.M0F => 0x01,
            OpcodeMap.M0F38 => 0x02,
            OpcodeMap.M0F3A => 0x03,
            OpcodeMap.MAP5 => 0x05,
            OpcodeMap.MAP7 => 0x07,
            _ => null
        };

        return Build(path, mode, id, formId, VexPrefix, mmmmm);
    }

    public static SampleBuildResult BuildXop(DecoderTreePath path, ModeNode.Slot mode, int id, string formId)
    {
        byte? mmmmm = path.Map switch
        {
            OpcodeMap.XOP8 => 0x08,
            OpcodeMap.XOP9 => 0x09,
            OpcodeMap.XOPA => 0x0A,
            _ => null
        };

        return Build(path, mode, id, formId, XopPrefix, mmmmm);
    }

    private static SampleBuildResult Build(DecoderTreePath path, ModeNode.Slot mode, int id, string formId,
        byte prefix, byte? mmmmm)
    {
        var steps = path.Steps;
        var definition = path.Definition;
        var encoding = CorpusTags.EncodingTag(path.Encoding);

        if (SampleBuilder.FindArchitecturalConflict(steps, mode, hasRexPrefixes: false) is { } impossible)
        {
            return new Impossible(impossible);
        }

        if (steps.FirstOrDefault(step => !IsVexStep(step)) is { } foreign)
        {
            return new Unencodable($"{DecoderTreePath.FormatStep(foreign)} does not apply to the {encoding} encoding");
        }

        if (mmmmm is not { } map)
        {
            return new Unencodable($"{encoding} has no {CorpusTags.MapTag(path.Map)} table");
        }

        if (path.Prefix is not { } refiningPrefix)
        {
            return new Unencodable($"the {encoding} table of {CorpusTags.MapTag(path.Map)} has no refining prefix");
        }

        if (!SampleBuilder.TrySelectPrefixState(steps, mode, PrefixStates, out var prefixes, out var prefixFailure))
        {
            return new Unencodable(prefixFailure);
        }

        if (prefixes.OperandSize66)
        {
            var operandSize = steps.First(step => step.Node == SampleBuilder.OperandSizeAxis);
            return new Unencodable(
                $"{DecoderTreePath.FormatStep(operandSize)} needs a 66 prefix, which {encoding} does not allow");
        }

        if (!TryChooseVectorLength(steps, encoding, out var l, out var vectorLengthFailure))
        {
            return new Unencodable(vectorLengthFailure);
        }

        var (osz, asz) = EffectiveSizes.Compute(mode, operandSizePrefix: false, prefixes.AddressSize67, prefixes.W,
            definition.OpsizeMap, definition.AdsizeMap);
        var operandWidth = EffectiveSizes.ComputeOperandWidth(mode, operandSizePrefix: false, prefixes.W,
            definition.OpsizeMap);

        var hasVsib = (definition.Operands ?? []).Any(operand => operand.Type is
            OperandType.MEMVSIBX or OperandType.MEMVSIBY or OperandType.MEMVSIBZ);
        if (hasVsib && (asz == 16))
        {
            return new Unencodable("a VSIB operand has no 16-bit address form");
        }

        var registers = SampleBuilder.AssignRegisters(definition);
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

        var payload1 = (byte)(InvertedRxb | map);
        var payload2 = (byte)(((prefixes.W ? 1 : 0) << 7) | ((~(registers.Vvvv ?? 0) & 0x0F) << 3) | (l << 2) |
            PpField(refiningPrefix));

        if (!IsReadAsPrefix(prefix, payload1, mode))
        {
            return new Unencodable(
                $"the {encoding} payload byte {payload1:X2} makes the decoder read {prefix:X2} as an opcode in " +
                $"{CorpusTags.ModeTag(mode)}-bit mode");
        }

        var bytes = new List<byte>();
        if (prefixes.AddressSize67)
        {
            bytes.Add(AddressSizePrefix);
        }

        bytes.Add(prefix);
        bytes.Add(payload1);
        bytes.Add(payload2);
        bytes.Add(path.Opcode);

        if (modrm is { } fields)
        {
            var readsAddressing = hasModrmEncoding && !definition.Flags.HasFlag(InstructionFlagsEnum.ForceRegForm);
            SampleBuilder.AppendModrm(bytes, fields, asz, readsAddressing, hasVsib ? registers.VsibIndex : null);
        }

        byte? is4Byte = (registers.Is4 is { } is4) ? (byte)(is4 << 4) : null;
        if (!SampleBuilder.TryAppendImmediates(bytes, definition, operandWidth, asz, out var immediateFailure,
                is4Byte))
        {
            return new Unencodable(immediateFailure);
        }

        // VEX map 0 exists only in a build with KNC support. The knc tag lets the checker skip the sample in a build
        // without it instead of failing it.
        var decoderModes = SampleBuilder.GetDecoderModes(steps, hasRex2: false,
            requiresKnc: (path.Encoding is InstructionEncoding.VEX) && (path.Map is OpcodeMap.MAP0));

        return new Built(SampleBuilder.CreateSample(path, mode, bytes, decoderModes, osz, asz, id, formId));
    }

    /// <summary>
    /// Chooses the L bit from the <c>vector_length</c> steps, 128 bits when the path leaves the axis open.
    /// </summary>
    private static bool TryChooseVectorLength(IReadOnlyList<PathStep> steps, string encoding, out int l,
        out string failure)
    {
        var allowed = new HashSet<string>(["128", "256"], StringComparer.Ordinal);

        foreach (var step in steps.Where(step => step.Node == SampleBuilder.VectorLengthAxis))
        {
            allowed.IntersectWith(step.Slots);
            if (allowed.Count == 0)
            {
                l = 0;
                failure = $"{DecoderTreePath.FormatStep(step)} has no {encoding} encoding";
                return false;
            }
        }

        l = allowed.Contains("128") ? 0 : 1;
        failure = string.Empty;
        return true;
    }

    /// <summary>
    /// The test <c>ZydisNodeHandlerOpcode</c> applies to the byte after <c>C4</c> or <c>8F</c>. Outside 64-bit mode
    /// <c>C4</c> is LES unless both top bits of that byte are set. <c>8F</c> is POP unless its map field is 8 or above,
    /// in every mode.
    /// </summary>
    private static bool IsReadAsPrefix(byte prefix, byte payload1, ModeNode.Slot mode)
    {
        return (prefix == XopPrefix)
            ? ((payload1 & 0x1F) >= 0x08)
            : ((mode is ModeNode.Slot.M64) || ((payload1 & 0xC0) == 0xC0));
    }

    private static int PpField(RefiningPrefix prefix)
    {
        return prefix switch
        {
            RefiningPrefix.PNP => 0b00,
            RefiningPrefix.P66 => 0b01,
            RefiningPrefix.PF3 => 0b10,
            RefiningPrefix.PF2 => 0b11,
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null)
        };
    }

    private static bool IsVexStep(PathStep step)
    {
        return step.Node.StartsWith(SampleBuilder.FeatureAxisPrefix, StringComparison.Ordinal) || step.Node is
            SampleBuilder.AddressSizeAxis or
            SampleBuilder.ModeAxis or
            SampleBuilder.ModeCompactAxis or
            SampleBuilder.ModrmModAxis or
            SampleBuilder.ModrmModCompactAxis or
            SampleBuilder.ModrmRegAxis or
            SampleBuilder.ModrmRmAxis or
            SampleBuilder.OperandSizeAxis or
            SampleBuilder.RexWAxis or
            SampleBuilder.VectorLengthAxis;
    }
}

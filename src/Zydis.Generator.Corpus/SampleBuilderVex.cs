using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.DecoderTree;
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

    // R, X and B are stored inverted, and every canonical register index is below 8, so all three bits are set. Both
    // top bits set also keep the decoder from reading C4 as LES outside 64-bit mode, and the XOP map fields 8 to A
    // keep it from reading 8F as POP.
    private const byte InvertedRxb = 0xE0;

    public static SampleBuildResult BuildVex(DecoderTreePath path, ModeNode.Slot mode)
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

        return Build(path, mode, VexPrefix, mmmmm);
    }

    public static SampleBuildResult BuildXop(DecoderTreePath path, ModeNode.Slot mode)
    {
        byte? mmmmm = path.Map switch
        {
            OpcodeMap.XOP8 => 0x08,
            OpcodeMap.XOP9 => 0x09,
            OpcodeMap.XOPA => 0x0A,
            _ => null
        };

        return Build(path, mode, XopPrefix, mmmmm);
    }

    private static SampleBuildResult Build(DecoderTreePath path, ModeNode.Slot mode, byte prefix, byte? mmmmm)
    {
        var steps = path.Steps;
        var definition = path.Definition;
        var encoding = CorpusTags.EncodingTag(path.Encoding);

        if (steps.FirstOrDefault(step => !SampleBuilder.IsPayloadStep(step)) is { } foreign)
        {
            return new Unencodable($"{DecoderTreePath.FormatStep(foreign)} does not apply to the {encoding} encoding");
        }

        if (mmmmm is not { } map)
        {
            return new Unencodable($"{encoding} has no {CorpusTags.MapTag(path.Map)} table");
        }

        if (!SampleBuilder.TrySelectPayloadPrefixes(path, mode, out var refiningPrefix, out var prefixes,
                out var prefixFailure))
        {
            return new Unencodable(prefixFailure);
        }

        if (!SampleBuilder.TryChooseSlot(steps, [SampleBuilder.VectorLengthAxis], ["128", "256"], encoding,
                out var vectorLength, out var vectorLengthFailure))
        {
            return new Unencodable(vectorLengthFailure);
        }

        var l = (vectorLength == "256") ? 1 : 0;

        var (osz, asz, operandWidth) = EffectiveSizes.Compute(mode, operandSizePrefix: false, prefixes.AddressSize67,
            prefixes.W, definition.OpsizeMap, definition.AdsizeMap);

        var registers = SampleBuilder.AssignRegisters(definition);
        if ((registers.VsibIndex is not null) && (asz == 16))
        {
            return new Unencodable("a VSIB operand has no 16-bit address form");
        }

        if (!SampleBuilder.TryChooseModrm(steps, definition, registers, asz, forcesModrm: false, out var modrm,
                out var modrmFailure))
        {
            return new Unencodable(modrmFailure);
        }

        var payload1 = (byte)(InvertedRxb | map);
        var payload2 = SampleBuilder.WvvvvPpByte(prefixes.W, SampleBuilder.InvertedVvvv(registers.Vvvv), l,
            refiningPrefix);

        var bytes = new List<byte>();
        if (prefixes.AddressSize67)
        {
            bytes.Add(SampleBuilder.AddressSizePrefix);
        }

        bytes.Add(prefix);
        bytes.Add(payload1);
        bytes.Add(payload2);
        bytes.Add(path.Opcode);

        if (modrm is { } fields)
        {
            SampleBuilder.AppendModrm(bytes, fields, asz, registers.VsibIndex);
        }

        byte? is4Byte = (registers.Is4 is { } is4) ? (byte)(is4 << 4) : null;
        if (!SampleBuilder.TryAppendImmediates(bytes, definition, operandWidth, asz, out var immediateFailure,
                is4Byte))
        {
            return new Unencodable(immediateFailure);
        }

        // VEX map 0 exists only in a build with KNC support. The knc tag lets the checker skip the sample in a build
        // without it instead of failing it.
        var decoderModes = SampleBuilder.GetDecoderModes(steps,
            requiresKnc: (path.Encoding is InstructionEncoding.VEX) && (path.Map is OpcodeMap.MAP0));

        return new Built(bytes, decoderModes, osz, asz);
    }
}

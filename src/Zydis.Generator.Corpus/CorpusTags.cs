using System;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// The spellings the corpus files use for encodings, opcode maps and machine modes.
/// </summary>
internal static class CorpusTags
{
    public static string EncodingTag(InstructionEncoding encoding)
    {
        return encoding switch
        {
            InstructionEncoding.Default => "default",
            InstructionEncoding.AMD3DNOW => "3dnow",
            InstructionEncoding.XOP => "xop",
            InstructionEncoding.VEX => "vex",
            InstructionEncoding.EVEX => "evex",
            InstructionEncoding.MVEX => "mvex",
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
        };
    }

    public static string MapTag(OpcodeMap map)
    {
        return map switch
        {
            OpcodeMap.MAP0 => "map0",
            OpcodeMap.M0F => "0f",
            OpcodeMap.M0F38 => "0f38",
            OpcodeMap.M0F3A => "0f3a",
            OpcodeMap.M0F0F => "0f0f",
            OpcodeMap.MAP4 => "map4",
            OpcodeMap.MAP5 => "map5",
            OpcodeMap.MAP6 => "map6",
            OpcodeMap.MAP7 => "map7",
            OpcodeMap.XOP8 => "xop8",
            OpcodeMap.XOP9 => "xop9",
            OpcodeMap.XOPA => "xopa",
            _ => throw new ArgumentOutOfRangeException(nameof(map), map, null)
        };
    }

    public static string ModeTag(ModeNode.Slot mode)
    {
        return ModeNode.NodeDefinition.Instance.GetSlotName((int)mode);
    }
}

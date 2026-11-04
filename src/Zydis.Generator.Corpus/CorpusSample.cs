using System.Globalization;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// One line of a corpus file: the bytes of one decoder-tree leaf in one machine mode and the tags the checker
/// validates the decode against.
/// </summary>
/// <param name="Bytes">The instruction bytes as uppercase hex without separators.</param>
/// <param name="Mode">The machine mode.</param>
/// <param name="Enable">The comma-separated decoder modes to enable before the decode, or <c>-</c>.</param>
/// <param name="Disable">The comma-separated decoder modes to disable before the decode, or <c>-</c>.</param>
/// <param name="Encoding">The encoding.</param>
/// <param name="Map">The opcode map of the opcode table.</param>
/// <param name="Opcode">The opcode byte.</param>
/// <param name="Osz">The effective operand size, <c>8</c> for byte operations.</param>
/// <param name="Asz">The effective address size.</param>
/// <param name="Id">The definition id inside the encoding, as the emitted tables use it.</param>
/// <param name="Form">The FormId of the definition.</param>
/// <param name="Mnemonic">The mnemonic of the definition.</param>
/// <param name="Path">The decision slots of the leaf, as <see cref="DecoderTreePath.PathString"/> renders them.</param>
public sealed record CorpusSample(
    string Bytes,
    ModeNode.Slot Mode,
    string Enable,
    string Disable,
    InstructionEncoding Encoding,
    OpcodeMap Map,
    byte Opcode,
    int Osz,
    int Asz,
    int Id,
    string Form,
    string Mnemonic,
    string Path)
{
    /// <summary>
    /// The header line of a corpus file.
    /// </summary>
    public static string Header =>
        "bytes\tmode\tenable\tdisable\tencoding\tmap\topcode\tosz\tasz\tid\tform\tmnemonic\tpath";

    /// <summary>
    /// Renders the sample as one tab-separated line in <see cref="Header"/> column order, with the mode, encoding and
    /// map in their <see cref="CorpusTags"/> spellings and the opcode as two uppercase hex digits.
    /// </summary>
    /// <returns>The line without a line break.</returns>
    public string ToTsvLine()
    {
        return string.Join('\t', Bytes, CorpusTags.ModeTag(Mode), Enable, Disable, CorpusTags.EncodingTag(Encoding),
            CorpusTags.MapTag(Map), Opcode.ToString("X2", CultureInfo.InvariantCulture),
            Osz.ToString(CultureInfo.InvariantCulture), Asz.ToString(CultureInfo.InvariantCulture),
            Id.ToString(CultureInfo.InvariantCulture), Form, Mnemonic, Path);
    }
}

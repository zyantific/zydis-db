using System.Globalization;

namespace Zydis.Generator.Corpus;

/// <summary>
/// One line of a corpus file: the bytes of one decoder-tree leaf in one machine mode and the tags the checker
/// validates the decode against.
/// </summary>
/// <param name="Bytes">The instruction bytes as uppercase hex without separators.</param>
/// <param name="Mode">The machine mode, <c>16</c>, <c>32</c> or <c>64</c>.</param>
/// <param name="Enable">The comma-separated decoder modes to enable before the decode, or <c>-</c>.</param>
/// <param name="Disable">The comma-separated decoder modes to disable before the decode, or <c>-</c>.</param>
/// <param name="Encoding">The encoding tag, for example <c>default</c> or <c>3dnow</c>.</param>
/// <param name="Map">The opcode map tag of the opcode table, for example <c>map0</c> or <c>0f38</c>.</param>
/// <param name="Opcode">The opcode byte as two uppercase hex digits.</param>
/// <param name="Osz">The effective operand size, <c>8</c> for byte operations.</param>
/// <param name="Asz">The effective address size.</param>
/// <param name="Id">The definition id inside the encoding, as the emitted tables use it.</param>
/// <param name="Form">The FormId of the definition.</param>
/// <param name="Mnemonic">The mnemonic of the definition.</param>
/// <param name="Path">The decision slots of the leaf, as <see cref="DecoderTreePath.PathString"/> renders them.</param>
public sealed record CorpusSample(
    string Bytes,
    string Mode,
    string Enable,
    string Disable,
    string Encoding,
    string Map,
    string Opcode,
    string Osz,
    string Asz,
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
    /// Renders the sample as one tab-separated line in <see cref="Header"/> column order.
    /// </summary>
    /// <returns>The line without a line break.</returns>
    public string ToTsvLine()
    {
        return string.Join('\t', Bytes, Mode, Enable, Disable, Encoding, Map, Opcode, Osz, Asz,
            Id.ToString(CultureInfo.InvariantCulture), Form, Mnemonic, Path);
    }
}

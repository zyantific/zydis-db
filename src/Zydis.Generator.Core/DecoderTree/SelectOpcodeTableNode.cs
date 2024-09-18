using Zydis.Generator.Core.Common;

namespace Zydis.Generator.Core.DecoderTree;

public sealed class SelectOpcodeTableNode :
    TerminalNode
{
    /// <inheritdoc/>
    public override int EncodedSize => 1;

    public InstructionEncoding Encoding { get; }
    public OpcodeMap Map { get; }
    public RefiningPrefix? Prefix { get; }
    public int OpcodeTableId { get; }

    /// <summary>
    /// Creates a new opcode table selector node corresponding to the specified <paramref name="encoding"/>, opcode
    /// <paramref name="map"/>, and optional refining <paramref name="prefix"/>.
    /// </summary>
    /// <param name="encoding">The instruction encoding.</param>
    /// <param name="map">The opcode map.</param>
    /// <param name="prefix">
    /// An optional refining prefix used to further specify the opcode table. Must be <see langword="null"/> for
    /// <see cref="InstructionEncoding.Default"/>.
    /// </param>
    public SelectOpcodeTableNode(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
    {
        Encoding = encoding;
        Map = map;
        Prefix = prefix;
        OpcodeTableId = OpcodeTables.GetTableId(encoding, map, prefix);
    }

    /// <inheritdoc/>
    public override int CalcEncodedSizeRecursive()
    {
        return EncodedSize;
    }

    #region Debugging

    public override string ToString()
    {
        return OpcodeTables.FormatTableString(Encoding, Map, Prefix);
    }

    #endregion Debugging
}

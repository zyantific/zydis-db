using System.Diagnostics;

using Zydis.Generator.Core.Common;

namespace Zydis.Generator.Core.DecoderTree;

public sealed class OpcodeTableNode :
    SelectorNode
{
    private readonly InstructionEncoding _encoding;
    private readonly RefiningPrefix? _prefix;
    private readonly OpcodeMap _map;

    internal OpcodeTableNode(InstructionEncoding encoding, RefiningPrefix? prefix, OpcodeMap map) :
        base(SelectorDefinitions.OpcodeTable, null)
    {
        _encoding = encoding;
        _prefix = prefix;
        _map = map;
    }

    #region Debugging

    public override string ToString()
    {
        return OpcodeTables.FormatTableString(_encoding, _map, _prefix);
    }

    #endregion Debugging
}

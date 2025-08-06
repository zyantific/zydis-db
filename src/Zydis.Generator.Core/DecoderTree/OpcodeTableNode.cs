using System.Globalization;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.DecoderTree;

public sealed partial class OpcodeTableNode
{
#pragma warning disable CA1034

    public sealed partial class NodeDefinition
    {
        /// <inheritdoc/>
        [System.Diagnostics.Contracts.Pure]
        protected override int SlotNameToIndex(string value)
        {
            if (!int.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var index))
            {
                return -1;
            }

            if ((index < 0) || (index >= NumberOfSlots))
            {
                return -1;
            }

            return index;
        }

        /// <inheritdoc/>
        [System.Diagnostics.Contracts.Pure]
        protected override string? IndexToSlotName(int index)
        {
            if ((index < 0) || (index >= NumberOfSlots))
            {
                return null;
            }

            return $"{index:X2}";
        }
    }

#pragma warning restore CA1034

    private readonly InstructionEncoding _encoding;
    private readonly RefiningPrefix? _prefix;
    private readonly OpcodeMap _map;

    internal OpcodeTableNode(InstructionEncoding encoding, RefiningPrefix? prefix, OpcodeMap map) :
        base(NodeDefinition.Instance)
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

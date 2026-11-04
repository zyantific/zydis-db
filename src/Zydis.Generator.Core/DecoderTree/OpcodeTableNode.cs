using System.Globalization;
using System.Linq;

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

    public InstructionEncoding Encoding { get; }
    public OpcodeMap Map { get; }
    public RefiningPrefix? Prefix { get; }

    /// <summary>
    /// <see langword="true"/> if no slot of the table holds an entry. Generation emits no tree for such a table.
    /// </summary>
    public bool IsEmpty => EnumerateSlots().All(slot => slot is null);

    internal OpcodeTableNode(InstructionEncoding encoding, RefiningPrefix? prefix, OpcodeMap map) :
        base(NodeDefinition.Instance)
    {
        Encoding = encoding;
        Prefix = prefix;
        Map = map;
    }

    #region Debugging

    public override string ToString()
    {
        return OpcodeTables.FormatTableString(Encoding, Map, Prefix);
    }

    #endregion Debugging
}

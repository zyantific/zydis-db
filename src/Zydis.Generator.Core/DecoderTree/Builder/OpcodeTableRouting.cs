using System;
using System.Diagnostics;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Shared opcode-table routing used by every decoder-tree builder: it maps a definition onto its refining prefix and
/// wires the inter-table switch nodes. Keeping this in one place means the legacy and variable-position builders bucket
/// definitions and stitch the top-level tables together identically.
/// </summary>
internal static class OpcodeTableRouting
{
    /// <summary>
    /// Derives the refining prefix that selects the opcode table a definition belongs to, from its encoding and (for
    /// the vector encodings) its mandatory-prefix filter.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The mandatory-prefix filter is <c>ignore</c> or negated for a vector encoding.
    /// </exception>
    public static RefiningPrefix? GetRefiningPrefix(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Encoding switch
        {
            InstructionEncoding.Default => null,
            InstructionEncoding.AMD3DNOW => RefiningPrefix.PNP,
            _ when definition.GetDecisionNodeIndex(MandatoryPrefixNode.NodeDefinition.Instance) is { Index: var index, IsNegated: false } =>
                MandatoryPrefixNode.NodeDefinition.Instance.GetSlotName(index) switch
                {
                    "ignore" => throw new NotSupportedException($"Refining prefix 'ignore' is not supported for instruction encoding '{definition.Encoding}'."),
                    "none" => RefiningPrefix.PNP,
                    "66" => RefiningPrefix.P66,
                    "f3" => RefiningPrefix.PF3,
                    "f2" => RefiningPrefix.PF2,
                    _ => throw new UnreachableException()
                },
            _ when definition.GetDecisionNodeIndex(MandatoryPrefixNode.NodeDefinition.Instance) is { IsNegated: true } =>
                throw new NotSupportedException($"Negated refining prefix is not supported for instruction encoding '{definition.Encoding}'."),
            _ => null
        };
    }

    /// <summary>
    /// Wires the top-level opcode-map switch nodes (0F escapes, REX2, XOP, VEX, EVEX/MVEX) into <paramref name="opcodeTables"/>,
    /// pointing each entry at the corresponding populated opcode table.
    /// </summary>
    public static void WireSwitchNodes(OpcodeTables opcodeTables)
    {
        ArgumentNullException.ThrowIfNull(opcodeTables);

        const int opcodeRex2 = 0xD5;
        const int opcodeXOP = 0x8F;
        const int opcodeVEX3 = 0xC4;
        const int opcodeVEX2 = 0xC5;
        const int opcodeEMVEX = 0x62;
        const int offset3DNow = 0x0F;

        var defaultTable = opcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null);
        defaultTable[0x0F] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F, null);

        var table0F = opcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.M0F, null);
        table0F[0x38] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F38, null);
        table0F[0x3A] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F3A, null);
        table0F[offset3DNow] = CreateOpcodeTableSelectNode(InstructionEncoding.AMD3DNOW, OpcodeMap.M0F0F, RefiningPrefix.PNP);

#pragma warning disable IDE0055

        defaultTable[opcodeRex2] = new SwitchTableREX2Node
        {
            [SwitchTableREX2Node.Slot.Default] = defaultTable[opcodeRex2],
            [SwitchTableREX2Node.Slot.REX2   ] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.MAP0, null),
            [SwitchTableREX2Node.Slot.REX2_0F] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F , null)
        };

        defaultTable[opcodeXOP] = new SwitchTableXOPNode
        {
            [SwitchTableXOPNode.Slot.Default ] = defaultTable[opcodeXOP],
            [SwitchTableXOPNode.Slot.PNP_XOP8] = CreateOpcodeTableSelectNode(InstructionEncoding.XOP, OpcodeMap.XOP8, RefiningPrefix.PNP),
            [SwitchTableXOPNode.Slot.PNP_XOP9] = CreateOpcodeTableSelectNode(InstructionEncoding.XOP, OpcodeMap.XOP9, RefiningPrefix.PNP),
            [SwitchTableXOPNode.Slot.PNP_XOPA] = CreateOpcodeTableSelectNode(InstructionEncoding.XOP, OpcodeMap.XOPA, RefiningPrefix.PNP)
            /* Rest = INVALID */
        };

        defaultTable[opcodeVEX3] = new SwitchTableVEXNode
        {
            [SwitchTableVEXNode.Slot.Default ] = defaultTable[opcodeVEX3],
            [SwitchTableVEXNode.Slot.PNP     ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.PNP),
            [SwitchTableVEXNode.Slot.PNP_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.PNP),
            [SwitchTableVEXNode.Slot.PNP_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.PNP),
            [SwitchTableVEXNode.Slot.PNP_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.PNP),
            [SwitchTableVEXNode.Slot.P66     ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.P66),
            [SwitchTableVEXNode.Slot.P66_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.P66),
            [SwitchTableVEXNode.Slot.P66_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.P66),
            [SwitchTableVEXNode.Slot.P66_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.P66),
            [SwitchTableVEXNode.Slot.PF3     ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.PF3),
            [SwitchTableVEXNode.Slot.PF3_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.PF3),
            [SwitchTableVEXNode.Slot.PF3_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.PF3),
            [SwitchTableVEXNode.Slot.PF3_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.PF3),
            [SwitchTableVEXNode.Slot.PF2     ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.PF2),
            [SwitchTableVEXNode.Slot.PF2_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.PF2),
            [SwitchTableVEXNode.Slot.PF2_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.PF2),
            [SwitchTableVEXNode.Slot.PF2_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.PF2)
        };

        // VEX C5 (2-byte) only supports the 0F opcode map.

        defaultTable[opcodeVEX2] = new SwitchTableVEXNode
        {
            [SwitchTableVEXNode.Slot.Default] = defaultTable[opcodeVEX2],
            [SwitchTableVEXNode.Slot.PNP_0F ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.PNP),
            [SwitchTableVEXNode.Slot.P66_0F ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.P66),
            [SwitchTableVEXNode.Slot.PF3_0F ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.PF3),
            [SwitchTableVEXNode.Slot.PF2_0F ] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.PF2)
            /* Rest = INVALID */
        };

        defaultTable[opcodeEMVEX] = new SwitchTableEMVEXNode
        {
            [SwitchTableEMVEXNode.Slot.Default      ] = defaultTable[opcodeEMVEX],
            [SwitchTableEMVEXNode.Slot.EVEX_PNP     ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_MAP4] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_MAP5] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_MAP6] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_PNP_MAP7] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.EVEX_P66     ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_MAP4] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_MAP5] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_MAP6] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_P66_MAP7] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3     ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_MAP4] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_MAP5] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_MAP6] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF3_MAP7] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2     ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_MAP4] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_MAP5] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_MAP6] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.EVEX_PF2_MAP7] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.MVEX_PNP     ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.MVEX_PNP_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.MVEX_PNP_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.MVEX_PNP_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.PNP),
            [SwitchTableEMVEXNode.Slot.MVEX_P66     ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.MVEX_P66_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.MVEX_P66_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.MVEX_P66_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.P66),
            [SwitchTableEMVEXNode.Slot.MVEX_PF3     ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.MVEX_PF3_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.MVEX_PF3_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.MVEX_PF3_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.PF3),
            [SwitchTableEMVEXNode.Slot.MVEX_PF2     ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.MVEX_PF2_0F  ] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.MVEX_PF2_0F38] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.PF2),
            [SwitchTableEMVEXNode.Slot.MVEX_PF2_0F3A] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.PF2)
        };

#pragma warning restore IDE0055

        return;

        OpcodeTableSwitchNode? CreateOpcodeTableSelectNode(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
        {
            if (!opcodeTables.GetTable(encoding, map, prefix).EnumerateSlots().Any(x => x is not null))
            {
                return null;
            }

            return new OpcodeTableSwitchNode(encoding, map, prefix);
        }
    }
}

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

public sealed class DecoderTreeBuilder
{
    public OpcodeTables OpcodeTables { get; } = new();

    public void InsertDefinition(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        RefiningPrefix? prefix = definition.Encoding switch
        {
            InstructionEncoding.Default => null,
            InstructionEncoding.AMD3DNOW => RefiningPrefix.PNP,
            _ => (RefiningPrefix?)definition.GetSelectorIndex(SelectorDefinitions.MandatoryPrefix)?.Index
        };

        SelectorNode currentTarget = OpcodeTables.GetTable(definition.Encoding, definition.OpcodeMap, prefix);
        SelectorTableIndex currentTargetIndex = definition.Opcode;

        foreach (var filter in EnumerateSelectorValues(definition))
        {
            var (nextSelectorDefinition, nextSelectorArguments) = SelectorDefinitions.ParseSelectorType(filter.Type);
            var nextTargetIndex = nextSelectorDefinition.ParseIndex(filter.Value);

            switch (currentTarget[currentTargetIndex])
            {
                case null:
                {
                    // Target slot is empty.
                    // Create a new selector node and insert it.
                    var next = new SelectorNode(nextSelectorDefinition, nextSelectorArguments);
                    currentTarget[currentTargetIndex] = next;
                    currentTarget = next;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case SelectorNode sn when sn.IsConstructedFrom(nextSelectorDefinition, nextSelectorArguments):
                {
                    // Target slot contains a selector node of the correct type.
                    // Continue inserting into the existing selector node.
                    currentTarget = sn;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case OverflowNode on when FindSelectorNode(on, nextSelectorDefinition, nextSelectorArguments) is { } sn:
                {
                    // Target slots contains an overflow node that has a selector node of correct type.
                    // Continue inserting into the existing selector node.
                    currentTarget = sn;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case OverflowNode on:
                {
                    // Target slot contains an overflow node that does NOT have selector node of correct type.
                    // Create a new selector node and insert it.
                    var next = new SelectorNode(nextSelectorDefinition, nextSelectorArguments);
                    on.Add(next);
                    currentTarget = next;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case FunctionNode:
                {
                    // TODO: Handle function nodes.
                    throw new NotImplementedException();
                }
                case DataNode:
                {
                    throw new UnreachableException();
                }
                default:
                {
                    // Target slot contains a selector node of the wrong type or a definition node.
                    // Create a new overflow node and insert both the existing node and the new selector node.
                    var next = new SelectorNode(nextSelectorDefinition, nextSelectorArguments);
                    currentTarget[currentTargetIndex] = new OverflowNode(currentTarget[currentTargetIndex]!, next);
                    currentTarget = next;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
            }
        }

        var definitionNode = new DefinitionNode(definition);

        switch (currentTarget[currentTargetIndex])
        {
            case null:
            {
                // Target slot is empty.
                // Insert the definition node directly.
                currentTarget[currentTargetIndex] = definitionNode;
                return;
            }
            case OverflowNode nd:
            {
                // Target slot contains an overflow node.
                // Insert the definition node into the overflow node.
                nd.Add(definitionNode);
                return;
            }
            default:
            {
                // Target slot already contains a non-overflow node.
                // Create a new overflow node and insert both the existing node and the definition node.
                currentTarget[currentTargetIndex] = new OverflowNode(currentTarget[currentTargetIndex]!, definitionNode);
                return;
            }
        }

        static SelectorNode? FindSelectorNode(OverflowNode haystack, SelectorDefinition definition, string[] arguments)
        {
            return haystack.Children
                .FirstOrDefault(x => x is SelectorNode fn && fn.IsConstructedFrom(definition, arguments)) as SelectorNode;
        }
    }

    public void InsertOpcodeTableSwitchNodes()
    {
        const int opcodeRex2 = 0xD5;
        const int opcodeXOP = 0x8F;
        const int opcodeVEX3 = 0xC4;
        const int opcodeVEX2 = 0xC5;
        const int opcodeEMVEX = 0x62;
        const int offset3DNow = 0x0F;

        var defaultTable = OpcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null);
        defaultTable[0x0F] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F, null);

        var table0F = OpcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.M0F, null);
        table0F[0x38] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F38, null);
        table0F[0x3A] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F3A, null);
        table0F[offset3DNow] = CreateOpcodeTableSelectNode(InstructionEncoding.AMD3DNOW, OpcodeMap.M0F0F, RefiningPrefix.PNP);

#pragma warning disable IDE0055

        defaultTable[opcodeRex2] = new SelectorNode(SelectorDefinitions.Rex2Map, null)
        {
            /* default      */ [0] = defaultTable[opcodeRex2],
            /* rex2_default */ [1] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.MAP0, null),
            /* rex2_0f      */ [2] = CreateOpcodeTableSelectNode(InstructionEncoding.Default, OpcodeMap.M0F , null)
        };

        defaultTable[opcodeXOP] = new SelectorNode(SelectorDefinitions.XOP, null)
        {
            /* default */ [0] = defaultTable[opcodeXOP],
            /* np_xop8 */ [1] = CreateOpcodeTableSelectNode(InstructionEncoding.XOP, OpcodeMap.XOP8, RefiningPrefix.PNP),
            /* np_xop9 */ [2] = CreateOpcodeTableSelectNode(InstructionEncoding.XOP, OpcodeMap.XOP9, RefiningPrefix.PNP),
            /* np_xopa */ [3] = CreateOpcodeTableSelectNode(InstructionEncoding.XOP, OpcodeMap.XOPA, RefiningPrefix.PNP)
            /* ....... */ /* INVALID */
        };

        defaultTable[opcodeVEX3] = new SelectorNode(SelectorDefinitions.VEX, null)
        {
            /* default */ [ 0] = defaultTable[opcodeVEX3],
            /* np      */ [ 1] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.PNP),
            /* np_0f   */ [ 2] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.PNP),
            /* np_0f38 */ [ 3] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.PNP),
            /* np_0f3a */ [ 4] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.PNP),
            /* 66      */ [ 5] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.P66),
            /* 66_0f   */ [ 6] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.P66),
            /* 66_0f38 */ [ 7] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.P66),
            /* 66_0f3a */ [ 8] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.P66),
            /* f3      */ [ 9] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.PF3),
            /* f3_0f   */ [10] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.PF3),
            /* f3_0f38 */ [11] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.PF3),
            /* f3_0f3a */ [12] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.PF3),
            /* f2      */ [13] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.MAP0 , RefiningPrefix.PF2),
            /* f2_0f   */ [14] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F  , RefiningPrefix.PF2),
            /* f2_0f38 */ [15] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F38, RefiningPrefix.PF2),
            /* f2_0f3a */ [16] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F3A, RefiningPrefix.PF2)
        };

        // VEX C5 (2-byte) only supports the 0F opcode map.

        defaultTable[opcodeVEX2] = new SelectorNode(SelectorDefinitions.VEX, null)
        {
            /* default */[ 0] = defaultTable[opcodeVEX2],
            /* np_0f   */[ 2] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.PNP),
            /* 66_0f   */[ 6] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.P66),
            /* f3_0f   */[10] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.PF3),
            /* f2_0f   */[14] = CreateOpcodeTableSelectNode(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.PF2)
            /* ....... */ /* INVALID */
        };

        defaultTable[opcodeEMVEX] = new SelectorNode(SelectorDefinitions.EMVEX, null)
        {
            /* default      */ [ 0] = defaultTable[opcodeEMVEX],
            /* evex_np      */ [ 1] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.PNP),
            /* evex_np_0f   */ [ 2] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.PNP),
            /* evex_np_0f38 */ [ 3] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.PNP),
            /* evex_np_0f3a */ [ 4] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.PNP),
            /* evex_np_map4 */ [ 5] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.PNP),
            /* evex_np_map5 */ [ 6] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.PNP),
            /* evex_np_map6 */ [ 7] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.PNP),
            /* evex_np_map7 */ [ 8] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.PNP),
            /* evex_66      */ [ 9] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.P66),
            /* evex_66_0f   */ [10] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.P66),
            /* evex_66_0f38 */ [11] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.P66),
            /* evex_66_0f3a */ [12] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.P66),
            /* evex_66_map4 */ [13] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.P66),
            /* evex_66_map5 */ [14] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.P66),
            /* evex_66_map6 */ [15] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.P66),
            /* evex_66_map7 */ [16] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.P66),
            /* evex_f3      */ [17] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.PF3),
            /* evex_f3_0f   */ [18] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.PF3),
            /* evex_f3_0f38 */ [19] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.PF3),
            /* evex_f3_0f3a */ [20] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.PF3),
            /* evex_f3_map4 */ [21] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.PF3),
            /* evex_f3_map5 */ [22] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.PF3),
            /* evex_f3_map6 */ [23] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.PF3),
            /* evex_f3_map7 */ [24] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.PF3),
            /* evex_f2      */ [25] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP0 , RefiningPrefix.PF2),
            /* evex_f2_0f   */ [26] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F  , RefiningPrefix.PF2),
            /* evex_f2_0f38 */ [27] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F38, RefiningPrefix.PF2),
            /* evex_f2_0f3a */ [28] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.M0F3A, RefiningPrefix.PF2),
            /* evex_f2_map4 */ [29] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP4 , RefiningPrefix.PF2),
            /* evex_f2_map5 */ [30] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP5 , RefiningPrefix.PF2),
            /* evex_f2_map6 */ [31] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP6 , RefiningPrefix.PF2),
            /* evex_f2_map7 */ [32] = CreateOpcodeTableSelectNode(InstructionEncoding.EVEX, OpcodeMap.MAP7 , RefiningPrefix.PF2),
            /* mvex_np      */ [33] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.PNP),
            /* mvex_np_0f   */ [34] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.PNP),
            /* mvex_np_0f38 */ [35] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.PNP),
            /* mvex_np_0f3a */ [36] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.PNP),
            /* mvex_66      */ [37] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.P66),
            /* mvex_66_0f   */ [38] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.P66),
            /* mvex_66_0f38 */ [39] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.P66),
            /* mvex_66_0f3a */ [40] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.P66),
            /* mvex_f3      */ [41] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.PF3),
            /* mvex_f3_0f   */ [42] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.PF3),
            /* mvex_f3_0f38 */ [43] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.PF3),
            /* mvex_f3_0f3a */ [44] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.PF3),
            /* mvex_f2      */ [45] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.MAP0 , RefiningPrefix.PF2),
            /* mvex_f2_0f   */ [46] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F  , RefiningPrefix.PF2),
            /* mvex_f2_0f38 */ [47] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F38, RefiningPrefix.PF2),
            /* mvex_f2_0f3a */ [48] = CreateOpcodeTableSelectNode(InstructionEncoding.MVEX, OpcodeMap.M0F3A, RefiningPrefix.PF2)
        };

#pragma warning restore IDE0055

        return;

        SelectOpcodeTableNode? CreateOpcodeTableSelectNode(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
        {
            if (!OpcodeTables.GetTable(encoding, map, prefix).HasNonZeroEntries)
            {
                return null;
            }

            return new SelectOpcodeTableNode(encoding, map, prefix);
        }
    }

    public void Optimize()
    {
        // TODO: Refactor.

        foreach (var table in OpcodeTables.Tables)
        {
            Optimize(table);
        }

        return;

        void Optimize(SelectorNode node)
        {
            for (var i = 0; i < node.Definition.NumberOfEntries - 1; ++i)
            {
                var index = SelectorTableIndex.ForIndex(i);

                if (node[index] is not SelectorNode sn)
                {
                    continue;
                }

                Optimize(sn);

                var optimized = GetOptimizedSelector(sn);

                if (optimized is null)
                {
                    continue;
                }

                node[index] = optimized;
            }

            for (var i = 0; i < node.Definition.NumberOfEntries - 1; ++i)
            {
                var index = SelectorTableIndex.ForNegatedIndex(i);

                if (node[index] is not SelectorNode sn)
                {
                    continue;
                }

                Optimize(sn);

                var optimized = GetOptimizedSelector(sn);
                if (optimized is null)
                {
                    continue;
                }

                node[index] = optimized;
            }
        }

        DecoderTreeNode? GetOptimizedSelector(SelectorNode node)
        {
            if (node.Definition == SelectorDefinitions.ModrmMod)
            {
                if (node.NegatedEntries[3] is not null)
                {
                    return new SelectorNode(SelectorDefinitions.ModrmModCompact, null)
                    {
                        [SelectorDefinitions.ModrmModCompact.ParseIndex("3")] = node.Entries[3],
                        [SelectorDefinitions.ModrmModCompact.ParseIndex("!3")] = node.NegatedEntries[3]
                    };
                }

                if (node.NegatedEntries.Any(x => x is not null))
                {
                    return null;
                }

                if (node.Entries.Take(3).All(x => x is null))
                {
                    return new SelectorNode(SelectorDefinitions.ModrmModCompact, null)
                    {
                        [SelectorDefinitions.ModrmModCompact.ParseIndex("3")] = node.Entries[3],
                        [SelectorDefinitions.ModrmModCompact.ParseIndex("!3")] = node.NegatedEntries[3]
                    };
                }
            }

            if (node.Definition == SelectorDefinitions.Mode)
            {
                if (node.NegatedEntries[2] is not null)
                {
                    return new SelectorNode(SelectorDefinitions.ModeCompact, null)
                    {
                        [SelectorDefinitions.ModeCompact.ParseIndex("64")] = node.Entries[2],
                        [SelectorDefinitions.ModeCompact.ParseIndex("!64")] = node.NegatedEntries[2]
                    };
                }

                if (node.NegatedEntries.Any(x => x is not null))
                {
                    return null;
                }

                if (node.Entries.Take(2).All(x => x is null))
                {
                    return new SelectorNode(SelectorDefinitions.ModeCompact, null)
                    {
                        [SelectorDefinitions.ModeCompact.ParseIndex("64")] = node.Entries[2],
                        [SelectorDefinitions.ModeCompact.ParseIndex("!64")] = node.NegatedEntries[2]
                    };
                }
            }

            return null;
        }
    }

    private static IEnumerable<(string Type, string Value)> EnumerateSelectorValues(InstructionDefinition definition)
    {
        if (definition.SelectorValues is null)
        {
            return [];
        }

        var order = FilterOrder[definition.Encoding];
        var lookup = order.Select((x, i) => (x, i)).ToDictionary(k => k.x, v => v.i);

        return definition.SelectorValues
            .Where(x => order.Contains(x.Key))
            .Select(x => (type: x.Key, value: x.Value.GetString()!))
            .OrderBy(x => lookup[x.type]);
    }

    private static readonly IReadOnlyDictionary<InstructionEncoding, string[]> FilterOrder = new Dictionary<InstructionEncoding, string[]>
    {
        [InstructionEncoding.Default] =
        [
            "rex_2",
            "feature_mpx",
            "feature_ud0_compat",
            "modrm_mod",
            "feature_cldemote",
            "prefix_group1",
            "mandatory_prefix",
            "modrm_reg",
            "modrm_rm",
            "mode",
            "address_size",
            "operand_size",
            "rex_w",
            "rex_b",
            "feature_amd",
            "feature_knc",
            "feature_cet",
            "feature_lzcnt",
            "feature_tzcnt",
            "feature_wbnoinvd",
            "feature_centaur",
            "feature_iprefetch"
        ],
        [InstructionEncoding.AMD3DNOW] =
        [
            "rex2",
            "modrm_mod",
            "mode_cldemote",
            "prefix_group1",
            "mandatory_prefix",
            "modrm_reg",
            "modrm_rm",
            "mode",
            "address_size",
            "operand_size",
            "rex_w",
            "rex_b",
        ],
        [InstructionEncoding.VEX] =
        [
            "modrm_reg",
            "modrm_rm",
            "vector_length",
            "mode",
            "modrm_mod",
            "rex_w",
            "operand_size",
            "address_size",
            "feature_knc"
        ],
        [InstructionEncoding.EVEX] =
        [
            "modrm_mod",
            "evex_u",
            "modrm_reg",
            "modrm_rm",
            "rex_w",
            "mode",
            "operand_size",
            "address_size",
            "evex_b",
            "vector_length",
            "evex_nd",
            "evex_nf",
            "evex_scc"
        ],
        [InstructionEncoding.MVEX] =
        [
            "modrm_mod",
            "modrm_reg",
            "modrm_rm",
            "rex_w",
            "mode",
            "operand_size",
            "address_size",
            "mvex_e",
            "vector_length"
        ],
        [InstructionEncoding.XOP] =
        [
            "modrm_reg",
            "modrm_rm",
            "vector_length",
            "mode",
            "modrm_mod",
            "rex_w",
            "operand_size",
            "address_size"
        ]
    }.ToFrozenDictionary();
}

[JsonSerializable(typeof(RefiningPrefix))]
internal sealed partial class DecoderTreeBuilderSerializerContext :
    JsonSerializerContext
{
}

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

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

        DecisionNode currentTarget = OpcodeTables.GetTable(definition.Encoding, definition.OpcodeMap, prefix);
        DecisionNodeIndex currentTargetIndex = definition.Opcode;

        foreach (var filter in EnumerateSelectorValues(definition))
        {
            var (nextDecisionNodeDefinition, nextDecisionNodeArguments) = DecisionNodes.ParseDecisionNodeType(filter.Type);
            var nextTargetIndex = nextDecisionNodeDefinition.ParseSlotIndex(filter.Value);

            switch (currentTarget[currentTargetIndex])
            {
                case null:
                {
                    // Target slot is empty.
                    // Create a new selector node and insert it.
                    var next = nextDecisionNodeDefinition.Create(nextDecisionNodeArguments);
                    currentTarget[currentTargetIndex] = next;
                    currentTarget = next;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case DecisionNode dn when dn.IsConstructedFrom(nextDecisionNodeDefinition, nextDecisionNodeArguments):
                {
                    // Target slot contains a selector node of the correct type.
                    // Continue inserting into the existing selector node.
                    currentTarget = dn;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case OverflowNode on when FindDecisionNode(on, nextDecisionNodeDefinition, nextDecisionNodeArguments) is { } dn:
                {
                    // Target slots contains an overflow node that has a selector node of correct type.
                    // Continue inserting into the existing selector node.
                    currentTarget = dn;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                case OverflowNode on:
                {
                    // Target slot contains an overflow node that does NOT have selector node of correct type.
                    // Create a new selector node and insert it.
                    var next = nextDecisionNodeDefinition.Create(nextDecisionNodeArguments);
                    on.Add(next);
                    currentTarget = next;
                    currentTargetIndex = nextTargetIndex;
                    break;
                }
                default:
                {
                    // Target slot contains a selector node of the wrong type or a definition node.
                    // Create a new overflow node and insert both the existing node and the new selector node.
                    var next = nextDecisionNodeDefinition.Create(nextDecisionNodeArguments);
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

        static DecisionNode? FindDecisionNode(OverflowNode haystack, DecisionNodeDefinition definition, string[] arguments)
        {
            return haystack.Children
                .FirstOrDefault(x => x is DecisionNode fn && fn.IsConstructedFrom(definition, arguments)) as DecisionNode;
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
            if (!OpcodeTables.GetTable(encoding, map, prefix).EnumerateSlots().Any(x => x is not null))
            {
                return null;
            }

            return new OpcodeTableSwitchNode(encoding, map, prefix);
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

        void Optimize(DecisionNode node)
        {
            foreach (var (index, value) in node.EnumerateVirtualSlots())
            {
                if (value is not DecisionNode dn)
                {
                    continue;
                }

                Optimize(dn);

                var optimized = dn switch
                {
                    ModeNode => GetOptimizedDecisionNode<ModeNode, ModeNode.Slot, ModeCompactNode>(dn, ModeNode.Slot.M64),
                    ModrmModNode => GetOptimizedDecisionNode<ModrmModNode, ModrmModNode.Slot, ModrmModCompactNode>(dn, ModrmModNode.Slot.M3),
                    _ => null
                };

                if (optimized is null)
                {
                    continue;
                }

                node[index] = optimized;
            }
        }

        DecoderTreeNode? GetOptimizedDecisionNode<TNode, TNodeIndexEnum, TOptimizedNode>(DecisionNode node, TNodeIndexEnum indexOfInterest)
            where TNode : DecisionNode<TNodeIndexEnum>
            where TOptimizedNode : DecisionNode, new()
            where TNodeIndexEnum : struct, Enum
        {
            if (node is not TNode inputNode)
            {
                return null;
            }

            var numberOfUnreducibleSlots = inputNode
                .EnumerateVirtualSlots()
                .Count(x => !EqualityComparer<TNodeIndexEnum>.Default.Equals(x.Key.Index, indexOfInterest) && (x.Value is not null));

            if (numberOfUnreducibleSlots is not 0)
            {
                return null;
            }

            return new TOptimizedNode
            {
                [0] = inputNode[indexOfInterest],
                [DecisionNodeIndex.ForNegatedIndex(0)] = inputNode[DecisionNodeIndex.ForNegatedIndex(indexOfInterest)]
            };
        }
    }

    private static IEnumerable<(string Type, string Value)> EnumerateSelectorValues(InstructionDefinition definition)
    {
        if (definition.Pattern is null)
        {
            return [];
        }

        var order = FilterOrder[definition.Encoding];
        var lookup = order.Select((x, i) => (x, i)).ToDictionary(k => k.x, v => v.i);

        return definition.Pattern
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

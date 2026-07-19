using System;
using System.Collections.Frozen;
using System.Collections.Generic;
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

        var prefix = OpcodeTableRouting.GetRefiningPrefix(definition);

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
        OpcodeTableRouting.WireSwitchNodes(OpcodeTables);
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

    internal static readonly IReadOnlyDictionary<InstructionEncoding, string[]> FilterOrder = new Dictionary<InstructionEncoding, string[]>
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

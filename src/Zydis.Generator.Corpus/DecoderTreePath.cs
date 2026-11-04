using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// One decision on the route from an opcode table slot to a definition.
/// </summary>
/// <param name="Node">The name of the decision node type.</param>
/// <param name="Slots">The names of the slots that lead to the next node, in slot order.</param>
/// <param name="IsElse">
/// <see langword="true"/> if the <paramref name="Slots"/> reach the next node through the else entry of the decision
/// node.
/// </param>
public sealed record PathStep(string Node, IReadOnlyList<string> Slots, bool IsElse);

/// <summary>
/// A route through the built decoder tree from an opcode table slot to the definition it decodes to.
/// </summary>
public sealed record DecoderTreePath(
    InstructionEncoding Encoding,
    OpcodeMap Map,
    RefiningPrefix? Prefix,
    byte Opcode,
    IReadOnlyList<PathStep> Steps,
    InstructionDefinition Definition)
{
    /// <summary>
    /// The <see cref="Steps"/> as <c>node=slot</c> pairs joined by <c>,</c>, with several slots joined by <c>|</c>
    /// and an else step written as <c>node=else:slot|slot</c>.
    /// </summary>
    public string PathString { get; } = string.Join(',', Steps.Select(FormatStep));

    /// <summary>
    /// Renders one step the way <see cref="PathString"/> does.
    /// </summary>
    internal static string FormatStep(PathStep step)
    {
        var slots = string.Join('|', step.Slots);

        return step.IsElse ? $"{step.Node}=else:{slots}" : $"{step.Node}={slots}";
    }
}

/// <summary>
/// Enumerates the routes through the built decoder tree.
/// </summary>
public static class DecoderTreePathEnumerator
{
    /// <summary>
    /// Yields one <see cref="DecoderTreePath"/> per route from an opcode table slot to a definition, in table order,
    /// then opcode order, then slot order with the else entry of each decision node last.
    /// </summary>
    /// <param name="tables">The opcode tables of a built decoder tree.</param>
    /// <returns>The routes to every reachable definition.</returns>
    /// <exception cref="InvalidOperationException">
    /// The tree contains a node kind the enumerator does not know, for example an <see cref="OverflowNode"/>, or a
    /// decision node that <see cref="DecisionNode.EnumerateSlots"/> rejects.
    /// </exception>
    public static IEnumerable<DecoderTreePath> Enumerate(OpcodeTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        foreach (var table in tables.Tables)
        {
            foreach (var (opcode, entry) in table.EnumerateSlots().Index())
            {
                if (entry is null)
                {
                    continue;
                }

                foreach (var path in Descend(table, (byte)opcode, entry, []))
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<DecoderTreePath> Descend(OpcodeTableNode table, byte opcode, DecoderTreeNode node,
        IReadOnlyList<PathStep> steps)
    {
        switch (node)
        {
            case DefinitionNode definition:
                yield return new DecoderTreePath(table.Encoding, table.Map, table.Prefix, opcode, steps,
                    definition.InstructionDefinition);
                break;

            // The target table is enumerated on its own, under its own identity.
            case OpcodeTableSwitchNode:
                break;

            case DecisionNode decision:
                foreach (var (step, child) in EnumerateBranches(decision))
                {
                    foreach (var path in Descend(table, opcode, child, [.. steps, step]))
                    {
                        yield return path;
                    }
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unexpected '{node.Definition.Name}' node in opcode table '{table}' at opcode 0x{opcode:X2}.");
        }
    }

    private static IEnumerable<(PathStep Step, DecoderTreeNode Child)> EnumerateBranches(DecisionNode node)
    {
        var definition = node.Definition;
        var slots = node.EnumerateSlots().ToArray();

        // EnumerateSlots rejects a node with both a negated entry and an else entry, so a slot reaches the else entry
        // exactly when the node has one and the slot holds no explicit entry.
        bool IsElseSlot(int slot) => (node.ElseEntry is not null) && (node[DecisionNodeIndex.ForIndex(slot)] is null);

        // The interner shares identical subtrees, so one child object can occupy several slots. Such a child is one
        // branch that lists every slot it occupies.
        var branches = Enumerable.Range(0, slots.Length)
            .Where(slot => (slots[slot] is not null) && !IsElseSlot(slot))
            .GroupBy<int, DecoderTreeNode>(slot => slots[slot]!, ReferenceEqualityComparer.Instance);

        foreach (var branch in branches)
        {
            yield return (new PathStep(definition.Name, SlotNames(definition, branch), IsElse: false), branch.Key);
        }

        // An else entry that no slot resolves to is unreachable for the decoder.
        var elseSlots = Enumerable.Range(0, slots.Length).Where(IsElseSlot).ToArray();
        if (elseSlots.Length > 0)
        {
            yield return (new PathStep(definition.Name, SlotNames(definition, elseSlots), IsElse: true),
                node.ElseEntry!);
        }
    }

    private static string[] SlotNames(DecisionNodeDefinition definition, IEnumerable<int> slots)
    {
        return [.. slots.Select(definition.GetSlotName)];
    }
}

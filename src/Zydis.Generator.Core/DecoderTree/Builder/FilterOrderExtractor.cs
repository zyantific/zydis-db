using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Recovers, per definition, the actual root-to-leaf sequence of its own filters as tested in a group's constructed
/// decoder subtree.
/// </summary>
/// <remarks>
/// A "don't-care" definition can be the tree's leaf on more than one root-to-leaf path - e.g. when it serves as the
/// fallback/else-branch for several more-specific siblings. <see cref="TreeConstructor"/>'s DP memoizes on a
/// canonical <c>(member set, residual constraints)</c> key, so every path reaching a given definition's terminal is
/// expected to resolve that definition's own constrained filters in the same relative order. This extractor does not
/// merely assume that: it visits every root-to-leaf path and throws if any two of them disagree on a definition's own
/// filter order, so a future violation of the invariant (e.g. from a <see cref="TreeConstructor"/> cost-model change)
/// fails loudly for whichever caller triggers it, instead of silently picking an arbitrary order.
/// </remarks>
internal static class FilterOrderExtractor
{
    /// <summary>
    /// Walks <paramref name="group"/>'s constructed subtree and records, for every member, the root-to-leaf order in
    /// which its own declared filters were tested.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A definition is reachable via two root-to-leaf paths that disagree on the relative order of its own filters.
    /// </exception>
    public static IReadOnlyDictionary<InstructionDefinition, IReadOnlyList<FilterKey>> ExtractOrders(
        ConstructedGroup group)
    {
        var ownFilterKeys = group.Members.ToDictionary(
            member => member.Definition, member => new HashSet<FilterKey>(member.Constraints.Constraints.Keys));

        var orders = new Dictionary<InstructionDefinition, IReadOnlyList<FilterKey>>();
        Walk(group.Result.Root, []);
        return orders;

        void Walk(DecoderTreeNode? node, List<FilterKey> path)
        {
            switch (node)
            {
                case null:
                    return;

                case DefinitionNode definitionNode:
                    Record(definitionNode.InstructionDefinition, path);
                    return;

                case DecisionNode decisionNode:
                    path.Add(CanonicalFilterKey(decisionNode));
                    foreach (var child in decisionNode.EnumerateSlots())
                    {
                        Walk(child, path);
                    }
                    path.RemoveAt(path.Count - 1);
                    return;

                default:
                    // A correctness gate must never silently discard a node: an unhandled type would drop the
                    // definitions beneath it and could mask a mis-extracted order.
                    throw new NotSupportedException(
                        $"Unexpected decoder tree node type '{node.GetType().Name}' while extracting filter orders.");
            }
        }

        void Record(InstructionDefinition definition, List<FilterKey> path)
        {
            if (!ownFilterKeys.TryGetValue(definition, out var keys))
            {
                return;
            }

            var order = path.Where(keys.Contains).ToList();

            if (!orders.TryGetValue(definition, out var existing))
            {
                orders[definition] = order;
                return;
            }

            if (!existing.SequenceEqual(order))
            {
                throw new InvalidOperationException(
                    $"'{definition.Mnemonic}' resolves to different filter-test orders depending on which " +
                    $"root-to-leaf path is walked: [{string.Join(", ", existing.Select(f => f.Name))}] via one " +
                    $"path vs [{string.Join(", ", order.Select(f => f.Name))}] via another. This violates the " +
                    "path-independence invariant every consumer of FilterOrderExtractor relies on.");
            }
        }
    }

    // TreeConstructor.Compact() rewrites a two-slot mode/modrm_mod test into its compact node counterpart, whose
    // Definition.Name ("mode_compact"/"modrm_mod_compact") no longer matches the filter key a definition declares
    // ("mode"/"modrm_mod"). Mirrors RegionEquivalenceChecker's NodeView mapping so the two stay in sync.
    private static FilterKey CanonicalFilterKey(DecisionNode node)
    {
        return node switch
        {
            ModeCompactNode => new FilterKey("mode"),
            ModrmModCompactNode => new FilterKey("modrm_mod"),
            _ => new FilterKey(node.Definition.Name)
        };
    }
}

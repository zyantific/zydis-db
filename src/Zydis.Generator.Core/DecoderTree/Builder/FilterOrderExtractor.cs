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
/// expected to resolve that definition's own constrained filters in the same relative order; this extractor records
/// the order found by the first path a deterministic depth-first walk encounters and relies on that expectation
/// rather than reconciling every path.
/// </remarks>
internal static class FilterOrderExtractor
{
    /// <summary>
    /// Walks <paramref name="group"/>'s constructed subtree and records, for every member, the root-to-leaf order in
    /// which its own declared filters were tested.
    /// </summary>
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
                    var definition = definitionNode.InstructionDefinition;
                    // First path wins; every other path reaching this definition's terminal is expected to agree
                    // (see remarks) rather than being reconciled here.
                    if (ownFilterKeys.TryGetValue(definition, out var keys) && !orders.ContainsKey(definition))
                    {
                        orders[definition] = path.Where(keys.Contains).ToList();
                    }
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
                    return;
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

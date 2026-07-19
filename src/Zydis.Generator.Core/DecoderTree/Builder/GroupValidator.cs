using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// A single instruction definition participating in a decoder-tree group, paired with its parsed filter constraints.
/// </summary>
/// <param name="Definition">The instruction definition.</param>
/// <param name="Constraints">The definition's parsed filter constraints.</param>
internal sealed record GroupMember(InstructionDefinition Definition, ConstraintSet Constraints);

/// <summary>
/// Validates that the members of a decoder-tree group describe non-conflicting regions of the instruction space.
/// </summary>
internal static class GroupValidator
{
    private static readonly FilterKey MandatoryPrefixKey = new("mandatory_prefix");
    private static readonly FilterKey OperandSizeKey = new("operand_size");

    /// <summary>
    /// Validates that the regions described by <paramref name="members"/> do not conflict.
    /// </summary>
    /// <param name="groupName">The name of the group, used to identify it in returned error messages.</param>
    /// <param name="members">The members of the group.</param>
    /// <returns>
    /// Human-readable error messages, sorted for determinism; empty if <paramref name="members"/> is valid.
    /// </returns>
    public static IReadOnlyList<string> Validate(string groupName, IReadOnlyList<GroupMember> members)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        ArgumentNullException.ThrowIfNull(members);

        var errors = new List<string>();

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            if (HasMandatory66WithOperandSize(member.Constraints))
            {
                errors.Add(
                    $"Group '{groupName}': member '{member.Definition.Mnemonic}' ({RenderFilters(member.Constraints)}) " +
                    "combines mandatory_prefix=66 with an operand_size constraint, which the decoder cannot express " +
                    "once prefix consumption moves to definition time.");
            }

            for (var j = i + 1; j < members.Count; j++)
            {
                var other = members[j];
                var relation = RegionAlgebra.Relate(member.Constraints, other.Constraints);

                if (relation is not (RegionRelation.IncomparableOverlap or RegionRelation.Equal))
                {
                    continue;
                }

                // Order the pair independently of their position in `members`, so permuting a set-identical
                // members list still yields byte-identical error text.
                var (first, second) = CanonicalOrder(member, other);

                var error = relation switch
                {
                    RegionRelation.IncomparableOverlap =>
                        $"Group '{groupName}': members '{first.Definition.Mnemonic}' ({RenderFilters(first.Constraints)}) " +
                        $"and '{second.Definition.Mnemonic}' ({RenderFilters(second.Constraints)}) overlap ambiguously; " +
                        "neither region fully contains the other.",
                    _ =>
                        $"Group '{groupName}': members '{first.Definition.Mnemonic}' ({RenderFilters(first.Constraints)}) " +
                        $"and '{second.Definition.Mnemonic}' ({RenderFilters(second.Constraints)}) describe identical " +
                        "regions (duplicate)."
                };

                errors.Add(error);
            }
        }

        errors.Sort(StringComparer.Ordinal);
        return errors;
    }

    // The decoder consumes the 66 mandatory prefix at definition time, which is incompatible with an
    // operand_size constraint on the same member; any other mandatory prefix value can still coexist with one.
    private static bool HasMandatory66WithOperandSize(ConstraintSet constraints)
    {
        if (!constraints.TryGet(MandatoryPrefixKey, out var mandatoryPrefix) ||
            !constraints.TryGet(OperandSizeKey, out _))
        {
            return false;
        }

        var sixtySixIndex = mandatoryPrefix.NodeDefinition.ParseSlotIndex("66");

        return !mandatoryPrefix.Index.IsNegated && mandatoryPrefix.Index.Index == sixtySixIndex.Index;
    }

    // Ties (identical mnemonics) fall back to the rendered filters, giving a total order over any two
    // members so the pairwise error text never depends on which one was passed as `member` vs. `other`.
    private static (GroupMember First, GroupMember Second) CanonicalOrder(GroupMember a, GroupMember b)
    {
        var byMnemonic = string.CompareOrdinal(a.Definition.Mnemonic, b.Definition.Mnemonic);
        var comparison = byMnemonic != 0 ? byMnemonic : string.CompareOrdinal(RenderFilters(a.Constraints), RenderFilters(b.Constraints));

        return comparison <= 0 ? (a, b) : (b, a);
    }

    private static string RenderFilters(ConstraintSet constraints)
    {
        if (constraints.Constraints.Count == 0)
        {
            return "no filters";
        }

        var parts = constraints.Constraints
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key.Name}={(x.Value.Index.IsNegated ? "!" : "")}{x.Value.NodeDefinition.GetSlotName(x.Value.Index.Index)}");

        return string.Join(", ", parts);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// A definition whose checked-in filter order (its <see cref="InstructionDefinition"/> <c>Pattern</c>'s key order)
/// disagrees with the order <see cref="TreeConstructor"/> currently tests its filters in.
/// </summary>
/// <param name="Definition">The definition whose recorded order has drifted.</param>
/// <param name="RecordedOrder">The definition's checked-in order, restricted to the filters <paramref name="CurrentOrder"/> covers.</param>
/// <param name="CurrentOrder">The order <see cref="FilterOrderExtractor"/> currently derives for the definition.</param>
internal readonly record struct LintFinding(
    InstructionDefinition Definition, IReadOnlyList<FilterKey> RecordedOrder, IReadOnlyList<FilterKey> CurrentOrder);

/// <summary>
/// Compares each definition's checked-in filter order against what <see cref="FilterOrderExtractor"/> derives from
/// the actual constructed tree today - the same primitive Task 6's migration tool used to write the data, so this
/// check is exactly "does the checked-in data match what re-running the migration would write."
/// </summary>
internal static class FilterOrderLint
{
    /// <summary>
    /// Evaluates every group member's recorded filter order against its current one, returning one
    /// <see cref="LintFinding"/> per definition whose recorded order has drifted from the current optimum.
    /// </summary>
    public static IReadOnlyList<LintFinding> Run(IEnumerable<ConstructedGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var findings = new List<LintFinding>();

        foreach (var group in groups)
        {
            var currentOrders = FilterOrderExtractor.ExtractOrders(group);

            foreach (var member in group.Members)
            {
                if (!currentOrders.TryGetValue(member.Definition, out var currentOrder))
                {
                    continue; // 0-1 own filters or otherwise nothing for the extractor to report - never a finding
                }

                var currentSet = new HashSet<FilterKey>(currentOrder);
                var recordedOrder = (member.Definition.Pattern?.Keys ?? [])
                    .Select(key => new FilterKey(key))
                    .Where(currentSet.Contains)
                    .ToList();

                if (!recordedOrder.SequenceEqual(currentOrder))
                {
                    findings.Add(new LintFinding(member.Definition, recordedOrder, currentOrder));
                }
            }
        }

        return findings;
    }
}

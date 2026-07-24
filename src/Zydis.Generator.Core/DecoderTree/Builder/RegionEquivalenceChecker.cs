using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Selects how the legacy mandatory-prefix node is interpreted while collecting regions.
/// </summary>
internal enum LegacyFallbackModel
{
    /// <summary>Every decision node is a plain filter test; used for the variable-position DAG.</summary>
    None,

    /// <summary>
    /// The mandatory-prefix node uses the runtime ignore-slot fallback: a definition parked in the ignore slot is
    /// reached, for a concrete prefix value, only where that value's slot subtree dead-ends. Used for the legacy tree.
    /// </summary>
    IgnoreSlot
}

/// <summary>
/// A single region a definition is reached under, expressed as a conjunction of filter constraints. A non-null
/// <see cref="Fallback"/> marks a region governed by the legacy ignore-slot complement rather than a plain conjunction.
/// </summary>
internal sealed class Region
{
    public Region(IReadOnlyDictionary<FilterKey, SlotMask> constraints, IgnoreFallback? fallback)
    {
        Constraints = constraints;
        Fallback = fallback;
    }

    /// <summary>
    /// The plain (non-mandatory, for a fallback region) filter constraints that must hold on the path to the definition.
    /// </summary>
    public IReadOnlyDictionary<FilterKey, SlotMask> Constraints { get; }

    /// <summary>
    /// The ignore-slot complement data, or <see langword="null"/> for a plain region.
    /// </summary>
    public IgnoreFallback? Fallback { get; }
}

/// <summary>
/// Describes the legacy ignore-slot complement for a definition parked in the mandatory-prefix ignore slot. For each
/// concrete prefix value, the definition is reached only where the value's own slot subtree covers no definition.
/// </summary>
internal sealed class IgnoreFallback
{
    public IgnoreFallback(
        FilterKey mandatoryKey, int ignoreSlot, IReadOnlyList<int> candidateSlots,
        IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyDictionary<FilterKey, SlotMask>>> covered)
    {
        MandatoryKey = mandatoryKey;
        IgnoreSlot = ignoreSlot;
        CandidateSlots = candidateSlots;
        Covered = covered;
    }

    public FilterKey MandatoryKey { get; }

    public int IgnoreSlot { get; }

    public IReadOnlyList<int> CandidateSlots { get; }

    /// <summary>
    /// For each concrete prefix slot value, the list of conjunctions its slot subtree routes to a definition under.
    /// A point is covered (so the ignore definition is not reached there) iff it satisfies any of these conjunctions.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<IReadOnlyDictionary<FilterKey, SlotMask>>> Covered { get; }
}

/// <summary>
/// The equivalence outcome for a single opcode table.
/// </summary>
/// <param name="Table">The table identity (encoding, prefix, map).</param>
/// <param name="Differences">Sorted, human-readable region differences; empty when the table is equivalent.</param>
/// <param name="MaxPointCount">The largest per-definition cross-product enumerated while checking this table.</param>
public sealed record TableVerification(string Table, IReadOnlyList<string> Differences, long MaxPointCount);

/// <summary>
/// Verifies that the frozen fixed-order reference tree and the variable-position DAG route every real decode point of
/// every opcode group to the same definition. Owns the reference model so the advisory verify mode has a stable
/// baseline to compare the production output against.
/// </summary>
/// <remarks>
/// Both trees are built in-process from the same <see cref="InstructionDefinition"/> instances over the current
/// datafiles, then compared per <c>(table, opcode)</c> group. Each side is walked into a per-definition set of regions
/// (conjunctions of <see cref="FilterKey"/> to <see cref="SlotMask"/>); the two sets are then compared pointwise over
/// the cross-product of every filter either side mentions.
/// <para>
/// The reference mandatory-prefix node keeps its physical ignore slot. Its runtime fallback is modelled as a
/// per-candidate complement: a definition in the ignore slot is reached, for a concrete prefix value, only where that
/// value's slot subtree covers no definition. The complement is evaluated pointwise during comparison rather than
/// materialised symbolically. For the vector encodings the mandatory prefix is the opcode-table identity (it selects the
/// table) rather than an in-group filter, so it is dropped from the DAG regions exactly where the reference tree has no
/// mandatory node.
/// </para>
/// </remarks>
public static class RegionEquivalenceChecker
{
    private const string MandatoryFilterName = "mandatory_prefix";

    // The mandatory-prefix ignore slot (index 0) is a fallback bucket, never an actual decoded prefix value, so it is
    // excluded from the enumerated value domain. The concrete values are none/66/f3/f2 at indices 1..4.
    private const int MandatoryIgnoreSlot = 0;

    private static readonly int[] MandatoryCandidateSlots = [1, 2, 3, 4];

    private const long PointBudget = 10_000_000;

    /// <summary>
    /// Builds the frozen fixed-order reference tree for <paramref name="definitions"/>. This is the baseline the
    /// advisory verify mode compares the variable-position output against; it is never emitted.
    /// </summary>
    public static OpcodeTables BuildReferenceModel(IEnumerable<InstructionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var builder = new DecoderTreeBuilder();

        foreach (var definition in definitions)
        {
            builder.InsertDefinition(definition);
        }

        builder.InsertOpcodeTableSwitchNodes();
        builder.Optimize();

        return builder.OpcodeTables;
    }

    /// <summary>
    /// Verifies every opcode table of <paramref name="legacy"/> against <paramref name="dp"/>.
    /// </summary>
    public static IReadOnlyList<TableVerification> Verify(OpcodeTables legacy, OpcodeTables dp)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(dp);

        if (legacy.Tables.Count != dp.Tables.Count)
        {
            throw new InvalidOperationException("The two opcode-table sets have a different number of tables.");
        }

        var results = new List<TableVerification>(legacy.Tables.Count);

        for (var t = 0; t < legacy.Tables.Count; t++)
        {
            var legacyTable = legacy.Tables[t];
            var dpTable = dp.Tables[t];
            var tableName = legacyTable.ToString()!;

            var differences = new List<string>();
            var maxPoints = 0L;

            for (var opcode = 0; opcode < 256; opcode++)
            {
                var index = DecisionNodeIndex.ForIndex(opcode);
                CompareNodes(tableName, opcode, legacyTable[index], dpTable[index], differences, ref maxPoints);
            }

            differences.Sort(StringComparer.Ordinal);
            results.Add(new TableVerification(tableName, differences, maxPoints));
        }

        return results;
    }

    /// <summary>
    /// Compares a single <c>(table, opcode)</c> group root from each side and returns the sorted differences.
    /// </summary>
    public static IReadOnlyList<string> CompareGroup(
        string table, int opcode, DecoderTreeNode? legacyRoot, DecoderTreeNode? dpRoot)
    {
        ArgumentNullException.ThrowIfNull(table);

        var differences = new List<string>();
        var maxPoints = 0L;

        CompareNodes(table, opcode, legacyRoot, dpRoot, differences, ref maxPoints);

        differences.Sort(StringComparer.Ordinal);
        return differences;
    }

    /// <summary>
    /// Walks a built group subtree into the set of regions each definition is reached under.
    /// </summary>
    /// <param name="root">The group root (a decision node or a bare definition node).</param>
    /// <param name="fallback">Whether to apply the legacy ignore-slot complement at the mandatory-prefix node.</param>
    /// <param name="dropMandatory">
    /// Drops the mandatory-prefix constraint from every collected region, used for the DAG side of the vector encodings
    /// where the mandatory prefix is the table identity and the legacy tree therefore has no mandatory node.
    /// </param>
    internal static IReadOnlyDictionary<InstructionDefinition, IReadOnlyList<Region>> CollectRegions(
        DecoderTreeNode? root, LegacyFallbackModel fallback, bool dropMandatory)
    {
        var collector = new Collector(fallback, dropMandatory);
        collector.Walk(root, new Dictionary<FilterKey, SlotMask>());

        var result = new Dictionary<InstructionDefinition, IReadOnlyList<Region>>(ReferenceEqualityComparer.Instance);
        foreach (var (definition, regions) in collector.Result)
        {
            result[definition] = regions;
        }

        return result;
    }

    private static void CompareNodes(
        string table, int opcode, DecoderTreeNode? legacy, DecoderTreeNode? dp, List<string> differences,
        ref long maxPoints)
    {
        var legacyKind = Categorize(legacy);
        var dpKind = Categorize(dp);

        if (legacyKind != dpKind)
        {
            differences.Add(FormattableString.Invariant(
                $"{table}[0x{opcode:X2}]: structural mismatch (legacy={Describe(legacy)}, dp={Describe(dp)})"));
            return;
        }

        switch (legacyKind)
        {
            case NodeKind.Empty:
                return;

            case NodeKind.Marker:
            {
                var legacyMarker = (OpcodeTableSwitchNode)legacy!;
                var dpMarker = (OpcodeTableSwitchNode)dp!;
                if (legacyMarker.OpcodeTableId != dpMarker.OpcodeTableId)
                {
                    differences.Add(FormattableString.Invariant(
                        $"{table}[0x{opcode:X2}]: opcode-table switch target mismatch (legacy={legacyMarker}, dp={dpMarker})"));
                }

                return;
            }

            case NodeKind.Routing:
            {
                if (legacy!.GetType() != dp!.GetType())
                {
                    differences.Add(FormattableString.Invariant(
                        $"{table}[0x{opcode:X2}]: routing-node type mismatch (legacy={legacy.Definition.Name}, dp={dp.Definition.Name})"));
                    return;
                }

                var legacyRouting = (DecisionNode)legacy;
                var dpRouting = (DecisionNode)dp;

                // Routing nodes are wired identically by the shared switch-node pass, so match them slot for slot; the
                // Default slot carries the real group that lived at this opcode and is compared as a group below.
                for (var i = 0; i < legacyRouting.Definition.NumberOfSlots; i++)
                {
                    var index = DecisionNodeIndex.ForIndex(i);
                    CompareNodes(table, opcode, legacyRouting[index], dpRouting[index], differences, ref maxPoints);
                }

                return;
            }

            default:
                CompareGroupNodes(table, opcode, legacy, dp, differences, ref maxPoints);
                return;
        }
    }

    private static void CompareGroupNodes(
        string table, int opcode, DecoderTreeNode? legacyRoot, DecoderTreeNode? dpRoot, List<string> differences,
        ref long maxPoints)
    {
        var legacyHasMandatory = ContainsMandatoryNode(legacyRoot);

        var mapOld = CollectRegions(
            legacyRoot, legacyHasMandatory ? LegacyFallbackModel.IgnoreSlot : LegacyFallbackModel.None, false);
        var mapNew = CollectRegions(dpRoot, LegacyFallbackModel.None, !legacyHasMandatory);

        Compare(table, opcode, mapOld, mapNew, differences, ref maxPoints);
    }

    private static void Compare(
        string table, int opcode,
        IReadOnlyDictionary<InstructionDefinition, IReadOnlyList<Region>> mapOld,
        IReadOnlyDictionary<InstructionDefinition, IReadOnlyList<Region>> mapNew,
        List<string> differences, ref long maxPoints)
    {
        var definitions = new HashSet<InstructionDefinition>(ReferenceEqualityComparer.Instance);
        definitions.UnionWith(mapOld.Keys);
        definitions.UnionWith(mapNew.Keys);

        foreach (var definition in definitions.OrderBy(DefinitionSortKey, StringComparer.Ordinal))
        {
            var oldRegions = mapOld.TryGetValue(definition, out var o) ? o : [];
            var newRegions = mapNew.TryGetValue(definition, out var n) ? n : [];

            var filters = new SortedSet<FilterKey>();
            AddFilters(oldRegions, filters);
            AddFilters(newRegions, filters);

            var filterList = filters.ToArray();
            var domains = new int[filterList.Length][];
            var positions = new Dictionary<FilterKey, int>(filterList.Length);

            var product = 1L;
            for (var i = 0; i < filterList.Length; i++)
            {
                positions[filterList[i]] = i;
                domains[i] = Domain(filterList[i]);
                product *= domains[i].Length;
            }

            maxPoints = Math.Max(maxPoints, product);

            Debug.Assert(product < PointBudget,
                $"Point cross-product {product} for '{definition.Mnemonic}' exceeds the {PointBudget} budget.");

            if (product >= PointBudget)
            {
                differences.Add(FormattableString.Invariant(
                    $"{table}[0x{opcode:X2}] {definition.Mnemonic}: point cross-product {product} exceeds budget {PointBudget}; skipped."));
                continue;
            }

            var oldMatchers = oldRegions.Select(region => new Matcher(region, positions)).ToArray();
            var newMatchers = newRegions.Select(region => new Matcher(region, positions)).ToArray();

            EnumerateDifferences(
                table, opcode, definition, filterList, domains, oldMatchers, newMatchers, differences);
        }
    }

    private static void EnumerateDifferences(
        string table, int opcode, InstructionDefinition definition, FilterKey[] filterList, int[][] domains,
        Matcher[] oldMatchers, Matcher[] newMatchers, List<string> differences)
    {
        var point = new int[filterList.Length];
        var cursor = new int[filterList.Length];
        var examples = 0;

        while (true)
        {
            for (var i = 0; i < filterList.Length; i++)
            {
                point[i] = domains[i][cursor[i]];
            }

            var oldIn = Any(oldMatchers, point);
            var newIn = Any(newMatchers, point);

            if (oldIn != newIn && examples < 3)
            {
                var side = oldIn ? "legacy" : "dp";
                differences.Add(FormattableString.Invariant(
                    $"{table}[0x{opcode:X2}] {definition.Mnemonic} [{RenderPattern(definition)}]: reachable in {side} only at {RenderPoint(filterList, point)}"));
                examples++;
            }

            var position = 0;
            while (position < cursor.Length)
            {
                cursor[position]++;
                if (cursor[position] < domains[position].Length)
                {
                    break;
                }

                cursor[position] = 0;
                position++;
            }

            if (position == cursor.Length)
            {
                break;
            }
        }
    }

    private static bool Any(Matcher[] matchers, int[] point)
    {
        foreach (var matcher in matchers)
        {
            if (matcher.Matches(point))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddFilters(IReadOnlyList<Region> regions, SortedSet<FilterKey> filters)
    {
        foreach (var region in regions)
        {
            foreach (var key in region.Constraints.Keys)
            {
                filters.Add(key);
            }

            if (region.Fallback is not { } fallback)
            {
                continue;
            }

            filters.Add(fallback.MandatoryKey);

            foreach (var conjunctions in fallback.Covered.Values)
            {
                foreach (var conjunction in conjunctions)
                {
                    foreach (var key in conjunction.Keys)
                    {
                        filters.Add(key);
                    }
                }
            }
        }
    }

    private static int[] Domain(FilterKey filter)
    {
        var slotCount = DecisionNodes.ParseDecisionNodeType(filter.Name).Definition.NumberOfSlots;

        if (filter.Name == MandatoryFilterName)
        {
            return [.. Enumerable.Range(0, slotCount).Where(slot => slot != MandatoryIgnoreSlot)];
        }

        return [.. Enumerable.Range(0, slotCount)];
    }

    private static bool ContainsMandatoryNode(DecoderTreeNode? node)
    {
        switch (node)
        {
            case null:
            case DefinitionNode:
                return false;
            case MandatoryPrefixNode:
                return true;
            case OverflowNode overflow:
                return overflow.Children.Any(ContainsMandatoryNode);
            case DecisionNode decision when !IsRouting(decision):
            {
                foreach (var (_, child) in decision.EnumerateVirtualSlots())
                {
                    if (ContainsMandatoryNode(child))
                    {
                        return true;
                    }
                }

                return ContainsMandatoryNode(decision.ElseEntry);
            }
            default:
                return false;
        }
    }

    private static string DefinitionSortKey(InstructionDefinition definition)
    {
        return FormattableString.Invariant(
            $"{definition.Mnemonic}|{definition.Opcode:X2}|{definition.Encoding}|{RenderPattern(definition)}");
    }

    private static string RenderPattern(InstructionDefinition definition)
    {
        if (definition.Pattern is null || definition.Pattern.Count == 0)
        {
            return "no filters";
        }

        return string.Join(",", definition.Pattern
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => FormattableString.Invariant($"{entry.Key}={entry.Value}")));
    }

    private static string RenderPoint(FilterKey[] filterList, int[] point)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < filterList.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var name = filterList[i].Name;
            var slotName = DecisionNodes.ParseDecisionNodeType(name).Definition.GetSlotName(point[i]);
            builder.Append(name).Append('=').Append(slotName);
        }

        return builder.Length == 0 ? "(unconstrained)" : builder.ToString();
    }

    private static NodeKind Categorize(DecoderTreeNode? node)
    {
        return node switch
        {
            null => NodeKind.Empty,
            OpcodeTableSwitchNode => NodeKind.Marker,
            DecisionNode decision when IsRouting(decision) => NodeKind.Routing,
            _ => NodeKind.Group
        };
    }

    private static bool IsRouting(DecisionNode node)
    {
        return node is OpcodeTableNode or SwitchTableVEXNode or SwitchTableEMVEXNode or SwitchTableREX2Node
            or SwitchTableXOPNode;
    }

    private static string Describe(DecoderTreeNode? node)
    {
        return node switch
        {
            null => "empty",
            DefinitionNode definition => $"definition({definition.InstructionDefinition.Mnemonic})",
            _ => node.Definition.Name
        };
    }

    private enum NodeKind
    {
        Empty,
        Marker,
        Routing,
        Group
    }

    // Precomputed, position-indexed form of a region for fast pointwise membership tests.
    private sealed class Matcher
    {
        private readonly int[] _basePositions;
        private readonly ulong[] _baseMasks;
        private readonly Fallback? _fallback;

        public Matcher(Region region, Dictionary<FilterKey, int> positions)
        {
            (_basePositions, _baseMasks) = Compile(region.Constraints, positions);

            if (region.Fallback is { } fallback)
            {
                var covered = new Dictionary<int, (int[] Positions, ulong[] Masks)[]>();
                foreach (var (value, conjunctions) in fallback.Covered)
                {
                    covered[value] = conjunctions.Select(conjunction => Compile(conjunction, positions)).ToArray();
                }

                _fallback = new Fallback(
                    positions[fallback.MandatoryKey], fallback.IgnoreSlot, [.. fallback.CandidateSlots], covered);
            }
        }

        public bool Matches(int[] point)
        {
            for (var i = 0; i < _basePositions.Length; i++)
            {
                if (((_baseMasks[i] >> point[_basePositions[i]]) & 1) == 0)
                {
                    return false;
                }
            }

            if (_fallback is not { } fallback)
            {
                return true;
            }

            var value = point[fallback.MandatoryPosition];
            if (value == fallback.IgnoreSlot || !fallback.CandidateSlots.Contains(value))
            {
                return false;
            }

            if (!fallback.Covered.TryGetValue(value, out var conjunctions))
            {
                return true;
            }

            foreach (var (positions, masks) in conjunctions)
            {
                var covered = true;
                for (var i = 0; i < positions.Length; i++)
                {
                    if (((masks[i] >> point[positions[i]]) & 1) == 0)
                    {
                        covered = false;
                        break;
                    }
                }

                if (covered)
                {
                    return false;
                }
            }

            return true;
        }

        private static (int[] Positions, ulong[] Masks) Compile(
            IReadOnlyDictionary<FilterKey, SlotMask> constraints, Dictionary<FilterKey, int> positions)
        {
            var compiledPositions = new int[constraints.Count];
            var compiledMasks = new ulong[constraints.Count];

            var i = 0;
            foreach (var (key, mask) in constraints)
            {
                compiledPositions[i] = positions[key];
                compiledMasks[i] = mask.Bits;
                i++;
            }

            return (compiledPositions, compiledMasks);
        }

        private sealed record Fallback(
            int MandatoryPosition, int IgnoreSlot, HashSet<int> CandidateSlots,
            IReadOnlyDictionary<int, (int[] Positions, ulong[] Masks)[]> Covered);
    }

    // Accumulates per-definition regions while walking a group subtree.
    private sealed class Collector
    {
        private static readonly FilterKey MandatoryKey = new(MandatoryFilterName);

        private readonly LegacyFallbackModel _fallback;
        private readonly bool _dropMandatory;

        public Collector(LegacyFallbackModel fallback, bool dropMandatory)
        {
            _fallback = fallback;
            _dropMandatory = dropMandatory;
        }

        public Dictionary<InstructionDefinition, List<Region>> Result { get; } =
            new(ReferenceEqualityComparer.Instance);

        public void Walk(DecoderTreeNode? node, Dictionary<FilterKey, SlotMask> conjunction)
        {
            switch (node)
            {
                case null:
                    return;
                case DefinitionNode definition:
                    Record(definition.InstructionDefinition, new Region(Freeze(conjunction), null));
                    return;
                case OverflowNode overflow:
                    foreach (var child in overflow.Children)
                    {
                        Walk(child, conjunction);
                    }

                    return;
                case MandatoryPrefixNode mandatory when _fallback is LegacyFallbackModel.IgnoreSlot:
                    WalkMandatory(mandatory, conjunction);
                    return;
                case DecisionNode routing when IsRouting(routing):
                    return;
                case DecisionNode decision:
                    WalkGeneric(decision, conjunction);
                    return;
                default:
                    // A correctness gate must never silently discard a node: an unhandled type would drop the
                    // definitions beneath it and could mask a real difference.
                    throw new NotSupportedException(
                        $"Unexpected decoder tree node type '{node.GetType().Name}' while collecting regions.");
            }
        }

        private void WalkGeneric(DecisionNode node, Dictionary<FilterKey, SlotMask> conjunction)
        {
            var (filter, entries) = NodeView(node);

            var byChild = new Dictionary<DecoderTreeNode, ulong>(ReferenceEqualityComparer.Instance);
            foreach (var (mask, child) in entries)
            {
                byChild[child] = byChild.GetValueOrDefault(child) | mask;
            }

            foreach (var (child, maskBits) in byChild)
            {
                Walk(child, With(conjunction, filter, new SlotMask(maskBits)));
            }
        }

        private void WalkMandatory(MandatoryPrefixNode node, Dictionary<FilterKey, SlotMask> conjunction)
        {
            var ignoreChild = node[DecisionNodeIndex.ForIndex(MandatoryIgnoreSlot)];

            if (ignoreChild is null)
            {
                // No definition is parked in the ignore slot, so there is nothing to complement; the concrete and
                // negated slots are ordinary filter tests (the ignore value itself is never enumerated).
                WalkGeneric(node, conjunction);
                return;
            }

            // With an ignore-slot definition present, candidate coverage is read from the regular slots only, so a
            // negated entry on the same node would be invisible and its definitions silently dropped. Today's corpus
            // never combines the two on one node, so fail loudly rather than mis-model a future one.
            if (HasNegatedEntry(node))
            {
                throw new InvalidOperationException(
                    "A mandatory-prefix node carries both an ignore-slot definition and a negated entry; the " +
                    "ignore-slot fallback complement cannot model both on the same node.");
            }

            var covered = new Dictionary<int, IReadOnlyList<IReadOnlyDictionary<FilterKey, SlotMask>>>();

            foreach (var value in MandatoryCandidateSlots)
            {
                var child = node[DecisionNodeIndex.ForIndex(value)];
                if (child is null)
                {
                    covered[value] = [];
                    continue;
                }

                var subRegions = CollectSubRegions(child);
                covered[value] = [.. subRegions.Select(entry => entry.Constraints)];

                foreach (var (definition, below) in subRegions)
                {
                    var full = With(Merge(conjunction, below), MandatoryKey, SlotMask.Single(value));
                    Record(definition, new Region(Freeze(full), null));
                }
            }

            var frozenCovered = covered.ToDictionary(entry => entry.Key, entry => entry.Value);

            foreach (var (definition, below) in CollectSubRegions(ignoreChild))
            {
                var full = Merge(conjunction, below);
                var fallback = new IgnoreFallback(MandatoryKey, MandatoryIgnoreSlot, MandatoryCandidateSlots, frozenCovered);
                Record(definition, new Region(Freeze(full), fallback));
            }
        }

        private static bool HasNegatedEntry(DecisionNode node)
        {
            for (var i = 0; i < node.Definition.NumberOfSlots; i++)
            {
                if (node[DecisionNodeIndex.ForNegatedIndex(i)] is not null)
                {
                    return true;
                }
            }

            return false;
        }

        private List<(InstructionDefinition Definition, IReadOnlyDictionary<FilterKey, SlotMask> Constraints)>
            CollectSubRegions(DecoderTreeNode child)
        {
            // A candidate/ignore subtree lives below the mandatory node, so it holds no further mandatory node and
            // never drops a constraint; a plain walk yields its below-mandatory conjunctions.
            var collector = new Collector(LegacyFallbackModel.None, false);
            collector.Walk(child, new Dictionary<FilterKey, SlotMask>());

            var result = new List<(InstructionDefinition, IReadOnlyDictionary<FilterKey, SlotMask>)>();
            foreach (var (definition, regions) in collector.Result)
            {
                foreach (var region in regions)
                {
                    result.Add((definition, region.Constraints));
                }
            }

            return result;
        }

        private void Record(InstructionDefinition definition, Region region)
        {
            if (!Result.TryGetValue(definition, out var regions))
            {
                regions = [];
                Result[definition] = regions;
            }

            regions.Add(region);
        }

        private IReadOnlyDictionary<FilterKey, SlotMask> Freeze(Dictionary<FilterKey, SlotMask> conjunction)
        {
            if (_dropMandatory && conjunction.ContainsKey(MandatoryKey))
            {
                var copy = new Dictionary<FilterKey, SlotMask>(conjunction);
                copy.Remove(MandatoryKey);
                return copy;
            }

            return new Dictionary<FilterKey, SlotMask>(conjunction);
        }

        private static Dictionary<FilterKey, SlotMask> With(
            Dictionary<FilterKey, SlotMask> conjunction, FilterKey filter, SlotMask mask)
        {
            var copy = new Dictionary<FilterKey, SlotMask>(conjunction);
            copy[filter] = copy.TryGetValue(filter, out var existing) ? existing.Intersect(mask) : mask;
            return copy;
        }

        private static Dictionary<FilterKey, SlotMask> Merge(
            Dictionary<FilterKey, SlotMask> conjunction, IReadOnlyDictionary<FilterKey, SlotMask> other)
        {
            var copy = new Dictionary<FilterKey, SlotMask>(conjunction);
            foreach (var (filter, mask) in other)
            {
                copy[filter] = copy.TryGetValue(filter, out var existing) ? existing.Intersect(mask) : mask;
            }

            return copy;
        }

        private static (FilterKey Filter, List<(ulong Mask, DecoderTreeNode Child)> Entries) NodeView(DecisionNode node)
        {
            var entries = new List<(ulong, DecoderTreeNode)>();

            switch (node)
            {
                case ModeCompactNode:
                {
                    // Slot 0 is mode=64; the else slot is everything else (mode in {16, 32}), mapped onto the full
                    // three-slot mode filter so it lines up with an uncompacted mode node on the other side.
                    var slots = node.EnumerateSlots().ToArray();
                    AddCompactEntry(entries, SlotMask.Single(2).Bits, slots[0]);
                    AddCompactEntry(entries, new SlotMask(0b011).Bits, slots[1]);
                    return (new FilterKey("mode"), entries);
                }
                case ModrmModCompactNode:
                {
                    // Slot 0 is modrm_mod=3 (register form); the else slot is modrm_mod in {0, 1, 2} (memory forms).
                    var slots = node.EnumerateSlots().ToArray();
                    AddCompactEntry(entries, SlotMask.Single(3).Bits, slots[0]);
                    AddCompactEntry(entries, new SlotMask(0b0111).Bits, slots[1]);
                    return (new FilterKey("modrm_mod"), entries);
                }
                default:
                {
                    var slots = node.EnumerateSlots().ToArray();
                    for (var i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] is { } child)
                        {
                            entries.Add((1UL << i, child));
                        }
                    }

                    return (new FilterKey(node.Definition.Name), entries);
                }
            }
        }

        private static void AddCompactEntry(List<(ulong, DecoderTreeNode)> entries, ulong mask, DecoderTreeNode? child)
        {
            if (child is not null)
            {
                entries.Add((mask, child));
            }
        }
    }
}

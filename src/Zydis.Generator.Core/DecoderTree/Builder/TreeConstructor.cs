using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Tuning knobs for <see cref="TreeConstructor"/>.
/// </summary>
/// <param name="ExpansionBudget">
/// The maximum number of (subproblem, candidate) evaluations the exact search may spend before it falls back to a
/// deterministic greedy choice for the remaining subproblems.
/// </param>
internal sealed record ConstructorOptions(int ExpansionBudget = 200_000);

/// <summary>
/// The outcome of a <see cref="TreeConstructor.Construct"/> run.
/// </summary>
/// <param name="Root">The root of the constructed decoder subtree.</param>
/// <param name="BudgetExhausted">Whether the exact search ran out of budget and used the greedy fallback anywhere.</param>
/// <param name="MemoHits">The number of subproblems served from the memo instead of being re-solved.</param>
/// <param name="SubproblemCount">The number of distinct subproblems solved.</param>
internal sealed record ConstructionResult(
    DecoderTreeNode Root, bool BudgetExhausted, int MemoHits, int SubproblemCount);

/// <summary>
/// Builds a minimal-cost decoder subtree for a validated group of instruction definitions.
/// </summary>
/// <remarks>
/// The constructor treats every filter of every member as a "variable position" constraint: rather than testing
/// filters in a fixed schema order, it searches, with memoization over the residual member state, for the filter test
/// order that yields the cheapest interned sub-DAG. A region-based specific-wins rule resolves terminals where several
/// nested members remain, and structurally identical subtrees are shared through the injected
/// <see cref="NodeInterner"/>.
/// </remarks>
internal sealed class TreeConstructor
{
    private readonly NodeInterner _interner;
    private readonly IReadOnlyList<string> _tieBreakPriority;
    private readonly ConstructorOptions _options;
    private readonly Dictionary<string, int> _priorityByName;

    /// <summary>
    /// Creates a new <see cref="TreeConstructor"/>.
    /// </summary>
    /// <param name="interner">The interner that canonicalizes and identifies the constructed nodes.</param>
    /// <param name="tieBreakPriority">
    /// Filter names in ascending priority. Used to break ties between equal-cost candidates and to steer the greedy
    /// fallback; typically the legacy per-encoding filter order.
    /// </param>
    /// <param name="options">Search tuning knobs.</param>
    public TreeConstructor(NodeInterner interner, IReadOnlyList<string> tieBreakPriority, ConstructorOptions options)
    {
        ArgumentNullException.ThrowIfNull(interner);
        ArgumentNullException.ThrowIfNull(tieBreakPriority);
        ArgumentNullException.ThrowIfNull(options);

        _interner = interner;
        _tieBreakPriority = tieBreakPriority;
        _options = options;
        _priorityByName = BuildPriorityLookup(tieBreakPriority);
    }

    /// <summary>
    /// Constructs the cheapest decoder subtree that routes every member of <paramref name="members"/> to its
    /// definition.
    /// </summary>
    public ConstructionResult Construct(IReadOnlyList<GroupMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var solver = new Solver(_interner, _priorityByName, _options.ExpansionBudget, Strategy.Exact, null);
        var root = solver.Solve(InitialState(members));

        return new ConstructionResult(root, solver.BudgetExhausted, solver.MemoHits, solver.SubproblemCount);
    }

    /// <summary>
    /// Compares the byte cost of the optimal subtree against the subtree obtained when the filter test order is forced
    /// to follow <paramref name="imposedOrder"/>. Advisory only; both searches are exact and neither uses the budget.
    /// </summary>
    public (long OptimalCost, long ImposedCost) Evaluate(
        IReadOnlyList<GroupMember> members, IReadOnlyList<FilterKey> imposedOrder)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(imposedOrder);

        // Separate interners keep this advisory measurement from polluting the caller's node identities.
        var optimalSolver = new Solver(new NodeInterner(), _priorityByName, int.MaxValue, Strategy.Exact, null);
        var optimalRoot = optimalSolver.Solve(InitialState(members));

        var imposedSolver = new Solver(new NodeInterner(), _priorityByName, int.MaxValue, Strategy.Imposed, imposedOrder);
        var imposedRoot = imposedSolver.Solve(InitialState(members));

        return (optimalSolver.Cost(optimalRoot).Bytes, imposedSolver.Cost(imposedRoot).Bytes);
    }

    private static ImmutableArray<MemberState> InitialState(IReadOnlyList<GroupMember> members)
    {
        return [.. members.Select((member, index) => new MemberState(
            index,
            member,
            member.Constraints.Constraints.ToImmutableSortedDictionary(entry => entry.Key, entry => entry.Value)))];
    }

    private static Dictionary<string, int> BuildPriorityLookup(IReadOnlyList<string> tieBreakPriority)
    {
        var lookup = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < tieBreakPriority.Count; i++)
        {
            // First occurrence wins; a duplicated name keeps its highest priority.
            lookup.TryAdd(tieBreakPriority[i], i);
        }

        return lookup;
    }

    private enum Strategy
    {
        Exact,
        Imposed
    }

    // The residual state of a single member on the path to a subproblem: its original definition plus the constraints
    // not yet consumed by an enclosing filter test.
    private sealed record MemberState(
        int Index, GroupMember Member, ImmutableSortedDictionary<FilterKey, FilterConstraint> Residuals)
    {
        public bool IsResolved => Residuals.Count == 0;

        public MemberState Consume(FilterKey key)
        {
            return this with { Residuals = Residuals.Remove(key) };
        }
    }

    private readonly record struct Cost(long Bytes, int MaxDepth, long SumDepth);

    private static readonly FilterKey Rex2Key = new("rex_2");

    private sealed class Solver
    {
        private readonly NodeInterner _interner;
        private readonly Dictionary<string, int> _priorityByName;
        private readonly Strategy _strategy;
        private readonly IReadOnlyList<FilterKey>? _imposedOrder;
        private readonly Dictionary<string, DecoderTreeNode> _memo = new(StringComparer.Ordinal);
        private readonly Dictionary<int, DecoderTreeNode> _nodeById = new();
        private int _budget;

        public Solver(
            NodeInterner interner, Dictionary<string, int> priorityByName, int budget, Strategy strategy,
            IReadOnlyList<FilterKey>? imposedOrder)
        {
            _interner = interner;
            _priorityByName = priorityByName;
            _budget = budget;
            _strategy = strategy;
            _imposedOrder = imposedOrder;
        }

        public bool BudgetExhausted { get; private set; }

        public int MemoHits { get; private set; }

        public int SubproblemCount { get; private set; }

        public DecoderTreeNode Solve(ImmutableArray<MemberState> members)
        {
            var key = BuildMemoKey(members);
            if (_memo.TryGetValue(key, out var cached))
            {
                MemoHits++;
                return cached;
            }

            SubproblemCount++;

            var result = members.All(member => member.IsResolved)
                ? _interner.Intern(new DefinitionNode(SelectWinner(members).Member.Definition))
                : SolveNonTerminal(members);

            _memo[key] = result;
            return result;
        }

        public Cost Cost(DecoderTreeNode root)
        {
            // The root is measured at depth 0 whether or not it is interned; every descendant is already interned, so
            // its stable id gives it a single identity across the sub-DAG.
            var depthById = new Dictionary<int, int>();
            CollectDescendants(root, depthById);

            long bytes = root.Definition.EncodedSize;
            foreach (var (id, _) in depthById)
            {
                bytes += NodeById(id).Definition.EncodedSize;
            }

            // Longest path from the root: a node's id always exceeds every descendant's id (interning is bottom-up),
            // so relaxing edges in descending-id order visits every node after all of its parents.
            foreach (var child in ChildrenOf(root))
            {
                Relax(depthById, child, 1);
            }

            foreach (var id in depthById.Keys.OrderByDescending(id => id))
            {
                var depth = depthById[id];
                foreach (var child in ChildrenOf(NodeById(id)))
                {
                    Relax(depthById, child, depth + 1);
                }
            }

            var maxDepth = depthById.Count == 0 ? 0 : depthById.Values.Max();
            long sumDepth = depthById.Values.Aggregate(0L, (sum, depth) => sum + depth);

            return new Cost(bytes, maxDepth, sumDepth);
        }

        private DecoderTreeNode SolveNonTerminal(ImmutableArray<MemberState> members)
        {
            var candidates = Candidates(members);

            if (_strategy is Strategy.Imposed)
            {
                return _interner.Intern(BuildCandidate(members, PickImposed(candidates)));
            }

            if (_budget <= 0)
            {
                BudgetExhausted = true;
                return _interner.Intern(BuildCandidate(members, PickGreedy(members, candidates)));
            }

            DecoderTreeNode? best = null;
            (Cost Cost, int Priority, string Name) bestKey = default;

            foreach (var candidate in candidates)
            {
                if (_budget <= 0)
                {
                    BudgetExhausted = true;
                    break;
                }

                _budget--;

                var node = BuildCandidate(members, candidate);
                var key = (Cost(node), Priority(candidate), candidate.Name);

                if (best is null || Compare(key, bestKey) < 0)
                {
                    best = node;
                    bestKey = key;
                }
            }

            // Every non-terminal has at least one candidate, and the budget is checked before the first evaluation, so
            // the exact branch always evaluates at least one candidate.
            return _interner.Intern(best!);
        }

        // Distinct residual filters across the members, ordered by tie-break priority then name. A residual `rex_2`
        // pins the choice: it must be tested before anything else can refine the tree.
        private ImmutableArray<FilterKey> Candidates(ImmutableArray<MemberState> members)
        {
            if (members.Any(member => member.Residuals.ContainsKey(Rex2Key)))
            {
                return [Rex2Key];
            }

            var keys = new SortedSet<FilterKey>(Comparer<FilterKey>.Create(CompareByPriority));

            foreach (var member in members)
            {
                foreach (var key in member.Residuals.Keys)
                {
                    keys.Add(key);
                }
            }

            return [.. keys];
        }

        private DecoderTreeNode BuildCandidate(ImmutableArray<MemberState> members, FilterKey filter)
        {
            var sample = members.First(member => member.Residuals.ContainsKey(filter)).Residuals[filter];
            var slotCount = sample.NodeDefinition.NumberOfSlots;

            var claimed = ClaimedSlots(members, filter);

            var slotGroups = new Dictionary<int, List<MemberState>>();
            foreach (var slot in claimed)
            {
                slotGroups[slot] = [];
            }

            var elseGroup = new List<MemberState>();

            foreach (var member in members)
            {
                var consumed = member.Consume(filter);

                if (!member.Residuals.TryGetValue(filter, out var constraint))
                {
                    // No constraint on the candidate: the member matches every slot, so it is replicated into every
                    // claimed slot and into the else branch that serves the unclaimed slots.
                    foreach (var slot in claimed)
                    {
                        slotGroups[slot].Add(consumed);
                    }

                    elseGroup.Add(consumed);
                    continue;
                }

                if (!constraint.Index.IsNegated)
                {
                    slotGroups[constraint.Index.Index].Add(consumed);
                    continue;
                }

                // A negated constraint matches every slot inside its mask. Those slots plus the else branch (which
                // serves the unclaimed slots, all of which lie inside the mask) reproduce the mask exactly.
                foreach (var slot in claimed)
                {
                    if (SlotMask.Single(slot).IsSubsetOf(constraint.Slots))
                    {
                        slotGroups[slot].Add(consumed);
                    }
                }

                elseGroup.Add(consumed);
            }

            AssertPartitionCoversMasks(members, filter, slotCount, claimed, slotGroups, elseGroup);

            var slotChildren = new Dictionary<int, DecoderTreeNode>();
            foreach (var slot in claimed)
            {
                if (slotGroups[slot].Count > 0)
                {
                    slotChildren[slot] = Solve([.. slotGroups[slot]]);
                }
            }

            var elseChild = elseGroup.Count > 0 ? Solve([.. elseGroup]) : null;

            // Interned identity is keyed on the node definition and its slot children only; `Create` discards the
            // arguments, so two filters differing solely by arguments would collapse onto one shared node. Refuse
            // loudly rather than mis-share. No corpus filter carries arguments today; this guards future ones.
            if (sample.NodeArguments.Length > 0)
            {
                throw new NotSupportedException(
                    $"Filter '{sample.Key.Name}' carries node arguments, which the interned decoder tree cannot represent.");
            }

            var node = sample.NodeDefinition.Create(sample.NodeArguments);

            foreach (var (slot, child) in slotChildren)
            {
                node[DecisionNodeIndex.ForIndex(slot)] = child;
            }

            if (elseChild is not null)
            {
                var hasEmptyClaimedSlot = claimed.Any(slot => !slotChildren.ContainsKey(slot));

                if (hasEmptyClaimedSlot)
                {
                    // An empty claimed slot must stay invalid, so the else child cannot be a catch-all here: it is
                    // materialized into each unclaimed slot instead, leaving the empty claimed slots null.
                    for (var slot = 0; slot < slotCount; slot++)
                    {
                        if (!claimed.Contains(slot))
                        {
                            node[DecisionNodeIndex.ForIndex(slot)] = elseChild;
                        }
                    }
                }
                else
                {
                    node.ElseEntry = elseChild;
                }
            }

            return Compact(node);
        }

        // Slots that need an explicit path: every value some member pins positively, plus every value a negated member
        // excludes (the else branch cannot express exclusion, so the excluded slot must be claimed to stay invalid).
        private static SortedSet<int> ClaimedSlots(ImmutableArray<MemberState> members, FilterKey filter)
        {
            var claimed = new SortedSet<int>();

            foreach (var member in members)
            {
                if (member.Residuals.TryGetValue(filter, out var constraint))
                {
                    claimed.Add(constraint.Index.Index);
                }
            }

            return claimed;
        }

        private FilterKey PickImposed(ImmutableArray<FilterKey> candidates)
        {
            var order = _imposedOrder!;

            return candidates
                .OrderBy(candidate => ImposedRank(order, candidate))
                .ThenBy(Priority)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                .First();
        }

        private static int ImposedRank(IReadOnlyList<FilterKey> order, FilterKey candidate)
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (order[i].Equals(candidate))
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        // Greedy fallback: prefer a filter every member constrains, then one that replicates the fewest members, then
        // the tie-break priority, then the name.
        private FilterKey PickGreedy(ImmutableArray<MemberState> members, ImmutableArray<FilterKey> candidates)
        {
            return candidates
                .OrderBy(candidate => members.All(member => member.Residuals.ContainsKey(candidate)) ? 0 : 1)
                .ThenBy(candidate => ReplicatedCount(members, candidate))
                .ThenBy(Priority)
                .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
                .First();
        }

        private static int ReplicatedCount(ImmutableArray<MemberState> members, FilterKey filter)
        {
            return members.Count(member =>
                !member.Residuals.TryGetValue(filter, out var constraint) || constraint.Index.IsNegated);
        }

        private static DecoderTreeNode Compact(DecisionNode node)
        {
            // A mode/modrm_mod test that only distinguishes one interesting slot from "everything else" collapses to
            // its compact two-slot counterpart; the interesting slot becomes slot 0 and the rest becomes the else
            // branch. Mirrors the retired `DecoderTreeBuilder.Optimize` pass, ahead of interning and costing.
            switch (node)
            {
                case ModeNode
                    when node[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M16)] is null &&
                         node[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M32)] is null:
                {
                    var compact = new ModeCompactNode
                    {
                        [DecisionNodeIndex.ForIndex(0)] = node[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M64)],
                        ElseEntry = node.ElseEntry
                    };

                    return compact;
                }
                case ModrmModNode
                    when node[DecisionNodeIndex.ForIndex((int)ModrmModNode.Slot.M0)] is null &&
                         node[DecisionNodeIndex.ForIndex((int)ModrmModNode.Slot.M1)] is null &&
                         node[DecisionNodeIndex.ForIndex((int)ModrmModNode.Slot.M2)] is null:
                {
                    var compact = new ModrmModCompactNode
                    {
                        [DecisionNodeIndex.ForIndex(0)] = node[DecisionNodeIndex.ForIndex((int)ModrmModNode.Slot.M3)],
                        ElseEntry = node.ElseEntry
                    };

                    return compact;
                }
                default:
                    return node;
            }
        }

        // The most specific member wins a terminal: the one whose original region is contained in every other's.
        // Validation guarantees the members form a containment chain, so a unique minimum exists.
        private static MemberState SelectWinner(ImmutableArray<MemberState> members)
        {
            var winner = members[0];

            for (var i = 1; i < members.Length; i++)
            {
                if (IsMoreSpecific(members[i], winner))
                {
                    winner = members[i];
                }
            }

            Debug.Assert(
                members.All(member => ReferenceEquals(member, winner) ||
                    RegionAlgebra.Relate(winner.Member.Constraints, member.Member.Constraints) is RegionRelation.SecondContainsFirst),
                "Terminal members must form a containment chain with a unique most-specific member.");

            return winner;
        }

        private static bool IsMoreSpecific(MemberState candidate, MemberState incumbent)
        {
            var relation = RegionAlgebra.Relate(candidate.Member.Constraints, incumbent.Member.Constraints);

            return relation switch
            {
                RegionRelation.SecondContainsFirst => true,
                // Equal regions are rejected by validation; keep the lower index only to stay deterministic if they slip through.
                RegionRelation.Equal => candidate.Index < incumbent.Index,
                _ => false
            };
        }

        private static string BuildMemoKey(ImmutableArray<MemberState> members)
        {
            var builder = new StringBuilder();

            foreach (var member in members.OrderBy(member => member.Index))
            {
                builder.Append(member.Index).Append(':');

                // Residuals iterate in sorted key order, so equal residual states serialize identically regardless of
                // the path that produced them.
                foreach (var (key, constraint) in member.Residuals)
                {
                    builder.Append(key.Name).Append('=').Append(constraint.Slots.Bits).Append(',');
                }

                builder.Append(';');
            }

            return builder.ToString();
        }

        private void CollectDescendants(DecoderTreeNode root, Dictionary<int, int> depthById)
        {
            foreach (var child in ChildrenOf(root))
            {
                var id = _interner.GetId(child);
                _nodeById[id] = child;

                if (depthById.TryAdd(id, 0))
                {
                    CollectDescendants(child, depthById);
                }
            }
        }

        private void Relax(Dictionary<int, int> depthById, DecoderTreeNode child, int depth)
        {
            var id = _interner.GetId(child);
            if (depth > depthById[id])
            {
                depthById[id] = depth;
            }
        }

        private DecoderTreeNode NodeById(int id) => _nodeById[id];

        private static IEnumerable<DecoderTreeNode> ChildrenOf(DecoderTreeNode node)
        {
            if (node is not DecisionNode decision)
            {
                yield break;
            }

            foreach (var (_, child) in decision.EnumerateVirtualSlots())
            {
                if (child is not null)
                {
                    yield return child;
                }
            }

            if (decision.ElseEntry is not null)
            {
                yield return decision.ElseEntry;
            }
        }

        private int Priority(FilterKey filter)
        {
            return _priorityByName.TryGetValue(filter.Name, out var priority) ? priority : int.MaxValue;
        }

        private int CompareByPriority(FilterKey left, FilterKey right)
        {
            var byPriority = Priority(left).CompareTo(Priority(right));

            return byPriority != 0 ? byPriority : string.CompareOrdinal(left.Name, right.Name);
        }

        private static int Compare((Cost Cost, int Priority, string Name) left, (Cost Cost, int Priority, string Name) right)
        {
            var byBytes = left.Cost.Bytes.CompareTo(right.Cost.Bytes);
            if (byBytes != 0)
            {
                return byBytes;
            }

            var byMaxDepth = left.Cost.MaxDepth.CompareTo(right.Cost.MaxDepth);
            if (byMaxDepth != 0)
            {
                return byMaxDepth;
            }

            var bySumDepth = left.Cost.SumDepth.CompareTo(right.Cost.SumDepth);
            if (bySumDepth != 0)
            {
                return bySumDepth;
            }

            var byPriority = left.Priority.CompareTo(right.Priority);

            return byPriority != 0 ? byPriority : string.CompareOrdinal(left.Name, right.Name);
        }

        private static void AssertPartitionCoversMasks(
            ImmutableArray<MemberState> members, FilterKey filter, int slotCount, SortedSet<int> claimed,
            Dictionary<int, List<MemberState>> slotGroups, List<MemberState> elseGroup)
        {
            var unclaimed = 0UL;
            for (var slot = 0; slot < slotCount; slot++)
            {
                if (!claimed.Contains(slot))
                {
                    unclaimed |= 1UL << slot;
                }
            }

            foreach (var member in members)
            {
                var expected = member.Residuals.TryGetValue(filter, out var constraint)
                    ? constraint.Slots.Bits
                    : SlotMask.All(slotCount).Bits;

                var reachable = 0UL;
                foreach (var slot in claimed)
                {
                    if (slotGroups[slot].Any(state => state.Index == member.Index))
                    {
                        reachable |= 1UL << slot;
                    }
                }

                if (elseGroup.Any(state => state.Index == member.Index))
                {
                    reachable |= unclaimed;
                }

                Debug.Assert(reachable == expected,
                    "The partition must reproduce each member's slot mask across its reachable slots.");
            }
        }
    }
}

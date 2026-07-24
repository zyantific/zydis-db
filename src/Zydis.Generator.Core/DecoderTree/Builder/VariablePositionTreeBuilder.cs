using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Thrown when <see cref="VariablePositionTreeBuilder.Build"/> finds one or more groups it cannot build, either because
/// their filters fail to parse or because their regions conflict. The complete, sorted list of problems is exposed via
/// <see cref="Errors"/> so the caller can report every one at once.
/// </summary>
public sealed class DecoderTreeBuildException :
    Exception
{
    /// <inheritdoc cref="DecoderTreeBuildException"/>
    public DecoderTreeBuildException()
    {
        Errors = [];
    }

    /// <inheritdoc cref="DecoderTreeBuildException"/>
    public DecoderTreeBuildException(string message) :
        base(message)
    {
        Errors = [];
    }

    /// <inheritdoc cref="DecoderTreeBuildException"/>
    public DecoderTreeBuildException(string message, Exception innerException) :
        base(message, innerException)
    {
        Errors = [];
    }

    /// <inheritdoc cref="DecoderTreeBuildException"/>
    /// <param name="errors">The complete, sorted list of problems that prevented the build.</param>
    public DecoderTreeBuildException(IReadOnlyList<string> errors) :
        base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>
    /// The complete, sorted list of problems that prevented the build.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return $"The decoder tree could not be built due to {errors.Count} problem(s):" +
            Environment.NewLine + string.Join(Environment.NewLine, errors);
    }
}

/// <summary>
/// Builds the decoder tree by treating every filter as a variable-position constraint. Definitions are bucketed by
/// their opcode table and opcode byte; each bucket is validated and then handed to a shared
/// <see cref="TreeConstructor"/> that searches for the cheapest interned sub-DAG. A single <see cref="NodeInterner"/>
/// backs every group, so structurally identical subtrees are shared across the whole tree.
/// </summary>
public sealed class VariablePositionTreeBuilder
{
    // One TreeConstructor takes a single tie-break list, but each encoding has its own fixed filter order. The
    // tie-break only decides between equal-cost candidates, so per-encoding fidelity is not load-bearing: the default
    // order leads, then each other encoding's remaining filters follow in declaration order.
    private static readonly IReadOnlyList<string> MergedTieBreakPriority = BuildMergedTieBreakPriority();

    private static readonly FilterKey MandatoryPrefixKey = new("mandatory_prefix");

    private readonly SortedDictionary<(int TableId, int Opcode), List<InstructionDefinition>> _buckets = new();
    private readonly List<string> _bucketErrors = [];
    private readonly NodeInterner _interner = new();
    private readonly TreeConstructor _constructor;

    /// <summary>
    /// Creates a new <see cref="VariablePositionTreeBuilder"/> with the default search tuning.
    /// </summary>
    public VariablePositionTreeBuilder() :
        this(new ConstructorOptions())
    {
    }

    internal VariablePositionTreeBuilder(ConstructorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _constructor = new TreeConstructor(_interner, MergedTieBreakPriority, options);
    }

    /// <summary>
    /// The opcode tables populated by <see cref="Build"/> and <see cref="InsertOpcodeTableSwitchNodes"/>.
    /// </summary>
    public OpcodeTables OpcodeTables { get; } = new();

    /// <summary>
    /// Statistics describing the most recent <see cref="Build"/>; <see cref="BuilderStatistics.Empty"/> until then.
    /// </summary>
    public BuilderStatistics Statistics { get; private set; } = BuilderStatistics.Empty;

    /// <summary>
    /// Buckets <paramref name="definition"/> by its opcode table and opcode byte. Routing problems are collected and
    /// surfaced together with every other problem when <see cref="Build"/> runs.
    /// </summary>
    public void InsertDefinition(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        try
        {
            var prefix = OpcodeTableRouting.GetRefiningPrefix(definition);
            var tableId = OpcodeTables.GetTableId(definition.Encoding, definition.OpcodeMap, prefix);
            var key = (tableId, (int)definition.Opcode);

            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = [];
                _buckets[key] = bucket;
            }

            bucket.Add(definition);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or InvalidDataException)
        {
            _bucketErrors.Add(
                $"Definition '{definition.Mnemonic}' (encoding {definition.Encoding}, opcode 0x{definition.Opcode:X2}) " +
                $"could not be routed to an opcode table: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates every bucketed group, constructs the cheapest subtree for each, and assigns the results into
    /// <see cref="OpcodeTables"/>.
    /// </summary>
    /// <exception cref="DecoderTreeBuildException">
    /// One or more groups could not be built. Every problem across all groups is aggregated into a single exception.
    /// </exception>
    public void Build()
    {
        var errors = new List<string>(_bucketErrors);
        var buildable = new List<(int TableId, int Opcode, string Table, IReadOnlyList<GroupMember> Members)>();

        foreach (var ((tableId, opcode), definitions) in _buckets)
        {
            var table = OpcodeTables.Tables[tableId];
            var groupName = FormattableString.Invariant($"{table}[0x{opcode:X2}]");

            if (!TryParseGroup(groupName, definitions, out var members, errors))
            {
                continue;
            }

            var groupErrors = GroupValidator.Validate(groupName, members);
            if (groupErrors.Count > 0)
            {
                errors.AddRange(groupErrors);
                continue;
            }

            buildable.Add((tableId, opcode, table.ToString()!, members));
        }

        if (errors.Count > 0)
        {
            errors.Sort(StringComparer.Ordinal);
            throw new DecoderTreeBuildException(errors);
        }

        Statistics = ConstructGroups(buildable);
    }

    /// <summary>
    /// Wires the top-level opcode-map switch nodes into <see cref="OpcodeTables"/>, using the shared
    /// <see cref="OpcodeTableRouting"/> so every builder buckets and routes identically.
    /// </summary>
    public void InsertOpcodeTableSwitchNodes()
    {
        OpcodeTableRouting.WireSwitchNodes(OpcodeTables);
    }

    private static bool TryParseGroup(
        string groupName, IReadOnlyList<InstructionDefinition> definitions, out IReadOnlyList<GroupMember> members,
        List<string> errors)
    {
        var parsed = new List<GroupMember>(definitions.Count);
        var failed = false;

        foreach (var definition in definitions)
        {
            try
            {
                var constraints = ConstraintSet.Parse(definition);

                // For every encoding except Default and AMD3DNOW, the mandatory prefix is the opcode-table identity
                // that OpcodeTableRouting.GetRefiningPrefix already consumed to bucket this definition, not an
                // in-group filter. Inside these vector tables the decoder's mandatory candidate is always "none", so
                // a retained constraint would build a MandatoryPrefixNode whose only live slot is the table's own
                // refining prefix - a dead end that resolves to INVALID at runtime. Drop it so the group refines on
                // its real filters.
                if (definition.Encoding is not (InstructionEncoding.Default or InstructionEncoding.AMD3DNOW))
                {
                    constraints = constraints.Without(MandatoryPrefixKey);
                }

                parsed.Add(new GroupMember(definition, constraints));
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or InvalidDataException)
            {
                errors.Add($"Group '{groupName}': could not parse filters for '{definition.Mnemonic}': {ex.Message}");
                failed = true;
            }
        }

        members = parsed;
        return !failed;
    }

    private BuilderStatistics ConstructGroups(
        IReadOnlyList<(int TableId, int Opcode, string Table, IReadOnlyList<GroupMember> Members)> buildable)
    {
        var accumulators = new SortedDictionary<int, TableStatisticsAccumulator>();

        foreach (var (tableId, opcode, tableName, members) in buildable)
        {
            var result = _constructor.Construct(members);
            OpcodeTables.Tables[tableId][DecisionNodeIndex.ForIndex(opcode)] = result.Root;

            if (!accumulators.TryGetValue(tableId, out var accumulator))
            {
                accumulator = new TableStatisticsAccumulator(tableName);
                accumulators[tableId] = accumulator;
            }

            accumulator.Add(Depth(result.Root), result.MemoHits, result.BudgetExhausted);
        }

        var tables = accumulators.Values.Select(accumulator => accumulator.ToStatistics()).ToArray();

        var groupCount = tables.Sum(table => table.GroupCount);
        var maxDepth = tables.Length == 0 ? 0 : tables.Max(table => table.MaxDepth);
        var memoHits = tables.Sum(table => table.MemoHits);
        var budgetBailouts = tables.Sum(table => table.BudgetBailouts);
        var averageDepth = groupCount == 0
            ? 0.0
            : tables.Sum(table => table.AverageDepth * table.GroupCount) / groupCount;

        return new BuilderStatistics(
            groupCount, _interner.Count, maxDepth, averageDepth, memoHits, budgetBailouts, tables);
    }

    // The longest chain of decision nodes from `root` to a leaf. Nodes are interned, so a shared subtree is measured
    // once by keying the memo on reference identity.
    private static int Depth(DecoderTreeNode root)
    {
        var memo = new Dictionary<DecoderTreeNode, int>(ReferenceEqualityComparer.Instance);

        return Height(root, memo);

        static int Height(DecoderTreeNode node, Dictionary<DecoderTreeNode, int> memo)
        {
            if (node is not DecisionNode decision)
            {
                return 0;
            }

            if (memo.TryGetValue(node, out var cached))
            {
                return cached;
            }

            var deepest = 0;

            foreach (var (_, child) in decision.EnumerateVirtualSlots())
            {
                if (child is not null)
                {
                    deepest = Math.Max(deepest, Height(child, memo));
                }
            }

            if (decision.ElseEntry is not null)
            {
                deepest = Math.Max(deepest, Height(decision.ElseEntry, memo));
            }

            var height = 1 + deepest;
            memo[node] = height;

            return height;
        }
    }

    private static IReadOnlyList<string> BuildMergedTieBreakPriority()
    {
        InstructionEncoding[] declarationOrder =
        [
            InstructionEncoding.Default,
            InstructionEncoding.AMD3DNOW,
            InstructionEncoding.VEX,
            InstructionEncoding.EVEX,
            InstructionEncoding.MVEX,
            InstructionEncoding.XOP
        ];

        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var encoding in declarationOrder)
        {
            foreach (var filter in FixedFilterOrder.ByEncoding[encoding])
            {
                if (seen.Add(filter))
                {
                    merged.Add(filter);
                }
            }
        }

        return merged;
    }
}

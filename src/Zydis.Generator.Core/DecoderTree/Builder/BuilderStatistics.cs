using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Per-table statistics gathered while constructing the decoder tree for a single opcode table.
/// </summary>
/// <param name="Table">The identity of the opcode table (encoding, prefix, and map).</param>
/// <param name="GroupCount">The number of opcode groups constructed for this table.</param>
/// <param name="MaxDepth">The greatest decision-node depth of any group root in this table.</param>
/// <param name="AverageDepth">The mean decision-node depth across this table's group roots.</param>
/// <param name="MemoHits">The number of subproblems served from the memo while constructing this table.</param>
/// <param name="BudgetBailouts">The number of groups that fell back to the greedy search on this table.</param>
public sealed record TableStatistics(
    string Table,
    int GroupCount,
    int MaxDepth,
    double AverageDepth,
    long MemoHits,
    int BudgetBailouts);

/// <summary>
/// Aggregate statistics describing a completed <see cref="VariablePositionTreeBuilder.Build"/>.
/// </summary>
/// <param name="GroupCount">The total number of opcode groups constructed across all tables.</param>
/// <param name="NodeCount">The number of distinct interned nodes shared across the whole tree.</param>
/// <param name="MaxDepth">The greatest decision-node depth of any group root.</param>
/// <param name="AverageDepth">The mean decision-node depth across all group roots.</param>
/// <param name="MemoHits">The total number of subproblems served from the memo.</param>
/// <param name="BudgetBailouts">The total number of groups that fell back to the greedy search.</param>
/// <param name="Tables">Per-table statistics, ordered by table identity.</param>
public sealed record BuilderStatistics(
    int GroupCount,
    int NodeCount,
    int MaxDepth,
    double AverageDepth,
    long MemoHits,
    int BudgetBailouts,
    IReadOnlyList<TableStatistics> Tables)
{
    /// <summary>
    /// An empty statistics instance, used before a build has run.
    /// </summary>
    public static BuilderStatistics Empty { get; } = new(0, 0, 0, 0.0, 0, 0, []);

    /// <summary>
    /// Renders the statistics as a human-readable, multi-line summary followed by one line per table.
    /// </summary>
    public string Render()
    {
        var builder = new StringBuilder();

        builder.Append(CultureInfo.InvariantCulture,
            $"groups={GroupCount}, nodes={NodeCount}, max_depth={MaxDepth}, avg_depth={AverageDepth:0.00}, " +
            $"memo_hits={MemoHits}, budget_bailouts={BudgetBailouts}");

        foreach (var table in Tables)
        {
            builder.AppendLine();
            builder.Append(CultureInfo.InvariantCulture,
                $"  {table.Table}: groups={table.GroupCount}, max_depth={table.MaxDepth}, " +
                $"avg_depth={table.AverageDepth:0.00}, memo_hits={table.MemoHits}, budget_bailouts={table.BudgetBailouts}");
        }

        return builder.ToString();
    }
}

/// <summary>
/// Accumulates the raw per-table measurements taken during construction, before they are frozen into a
/// <see cref="TableStatistics"/> record.
/// </summary>
internal sealed class TableStatisticsAccumulator(string table)
{
    private readonly List<int> _depths = [];

    public void Add(int depth, long memoHits, bool budgetExhausted)
    {
        _depths.Add(depth);
        MemoHits += memoHits;

        if (budgetExhausted)
        {
            BudgetBailouts++;
        }
    }

    public long MemoHits { get; private set; }

    public int BudgetBailouts { get; private set; }

    public int MaxDepth => _depths.Count == 0 ? 0 : _depths.Max();

    public double AverageDepth => _depths.Count == 0 ? 0.0 : _depths.Average();

    public int GroupCount => _depths.Count;

    public TableStatistics ToStatistics()
    {
        return new TableStatistics(table, GroupCount, MaxDepth, AverageDepth, MemoHits, BudgetBailouts);
    }
}

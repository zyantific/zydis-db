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

/// <summary>
/// Per-table measurements taken while an opcode table is laid out into decoder-table elements.
/// </summary>
/// <param name="Table">The identity of the opcode table (encoding, prefix, and map).</param>
/// <param name="Size">The number of decoder-table elements the table occupies once laid out.</param>
/// <param name="NodeCount">
/// The number of distinct nodes emitted for the table. A subtree shared across parents is counted once; a subtree the
/// range guard cloned is counted per copy.
/// </param>
/// <param name="CloneCount">The number of subtree clones the range guard produced while laying out the table.</param>
public sealed record TableEmissionStatistics(string Table, int Size, int NodeCount, int CloneCount);

/// <summary>
/// A console-facing report that joins the tree-construction statistics with the per-table emission measurements taken
/// during code generation. Rows are ordered by table identity so the rendered text is stable across runs.
/// </summary>
public sealed class GenerationReport
{
    // Column widths chosen so every header and the widest observed value fit without wrapping; the separator spans the
    // full row so the header, body, and totals align under one rule.
    private const string RowFormat = "{0,-16}{1,8}{2,8}{3,10}{4,11}{5,11}{6,12}{7,10}{8,8}";
    private const int RowWidth = 16 + 8 + 8 + 10 + 11 + 11 + 12 + 10 + 8;

    private readonly int _distinctNodeCount;
    private readonly IReadOnlyList<Row> _rows;
    private readonly Row _totals;

    private GenerationReport(int distinctNodeCount, IReadOnlyList<Row> rows, Row totals)
    {
        _distinctNodeCount = distinctNodeCount;
        _rows = rows;
        _totals = totals;
    }

    /// <summary>
    /// Joins <paramref name="builder"/> and <paramref name="emission"/> per table into an ordered report.
    /// </summary>
    public static GenerationReport Create(
        BuilderStatistics builder, IReadOnlyList<TableEmissionStatistics> emission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(emission);

        var byTable = builder.Tables.ToDictionary(table => table.Table, StringComparer.Ordinal);
        var byEmission = emission.ToDictionary(table => table.Table, StringComparer.Ordinal);

        var names = new SortedSet<string>(StringComparer.Ordinal);
        names.UnionWith(byTable.Keys);
        names.UnionWith(byEmission.Keys);

        var rows = new List<Row>(names.Count);
        foreach (var name in names)
        {
            byTable.TryGetValue(name, out var build);
            byEmission.TryGetValue(name, out var emit);

            rows.Add(new Row(
                name,
                build?.GroupCount ?? 0,
                emit?.NodeCount ?? 0,
                emit?.Size ?? 0,
                build?.MaxDepth ?? 0,
                build?.AverageDepth ?? 0.0,
                build?.MemoHits ?? 0,
                build?.BudgetBailouts ?? 0,
                emit?.CloneCount ?? 0));
        }

        var totals = new Row(
            "TOTAL",
            rows.Sum(row => row.Groups),
            rows.Sum(row => row.Nodes),
            rows.Sum(row => row.Size),
            rows.Count == 0 ? 0 : rows.Max(row => row.MaxDepth),
            builder.AverageDepth,
            rows.Sum(row => row.MemoHits),
            rows.Sum(row => row.Bailouts),
            rows.Sum(row => row.Clones));

        return new GenerationReport(builder.NodeCount, rows, totals);
    }

    /// <summary>
    /// Renders the report as a fixed-width table: a header, one line per table, a totals line, and the distinct
    /// interned-node count.
    /// </summary>
    public string Render()
    {
        var separator = new string('-', RowWidth);
        var builder = new StringBuilder();

        builder.Append(Header());
        builder.AppendLine().Append(separator);

        foreach (var row in _rows)
        {
            builder.AppendLine().Append(Format(row));
        }

        builder.AppendLine().Append(separator);
        builder.AppendLine().Append(Format(_totals));
        builder.AppendLine().Append(CultureInfo.InvariantCulture, $"distinct interned nodes: {_distinctNodeCount}");

        return builder.ToString();
    }

    private static string Header()
    {
        return string.Format(
            CultureInfo.InvariantCulture, RowFormat,
            "table", "groups", "nodes", "size", "max_depth", "avg_depth", "memo_hits", "bailouts", "clones");
    }

    private static string Format(Row row)
    {
        return string.Format(
            CultureInfo.InvariantCulture, RowFormat,
            row.Table, row.Groups, row.Nodes, row.Size, row.MaxDepth,
            row.AverageDepth.ToString("0.00", CultureInfo.InvariantCulture), row.MemoHits, row.Bailouts, row.Clones);
    }

    private sealed record Row(
        string Table, int Groups, int Nodes, int Size, int MaxDepth, double AverageDepth, long MemoHits, int Bailouts,
        int Clones);
}

/// <summary>
/// A console-facing report that compares the emitted decoder-table size of the legacy fixed-order tree against the
/// variable-position DAG, per table and in total. Rows are ordered by table identity for stable output.
/// </summary>
public sealed class SizeComparisonReport
{
    private const string RowFormat = "{0,-16}{1,12}{2,12}{3,12}";
    private const int RowWidth = 16 + 12 + 12 + 12;

    private readonly IReadOnlyList<Row> _rows;
    private readonly Row _totals;

    private SizeComparisonReport(IReadOnlyList<Row> rows, Row totals)
    {
        _rows = rows;
        _totals = totals;
    }

    /// <summary>
    /// Joins <paramref name="legacy"/> and <paramref name="dp"/> emission sizes per table into an ordered comparison.
    /// </summary>
    public static SizeComparisonReport Create(
        IReadOnlyList<TableEmissionStatistics> legacy, IReadOnlyList<TableEmissionStatistics> dp)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(dp);

        var byLegacy = legacy.ToDictionary(table => table.Table, table => table.Size, StringComparer.Ordinal);
        var byDp = dp.ToDictionary(table => table.Table, table => table.Size, StringComparer.Ordinal);

        var names = new SortedSet<string>(StringComparer.Ordinal);
        names.UnionWith(byLegacy.Keys);
        names.UnionWith(byDp.Keys);

        var rows = new List<Row>(names.Count);
        var totalLegacy = 0L;
        var totalDp = 0L;

        foreach (var name in names)
        {
            long legacySize = byLegacy.GetValueOrDefault(name);
            long dpSize = byDp.GetValueOrDefault(name);

            rows.Add(new Row(name, legacySize, dpSize));
            totalLegacy += legacySize;
            totalDp += dpSize;
        }

        return new SizeComparisonReport(rows, new Row("TOTAL", totalLegacy, totalDp));
    }

    /// <summary>
    /// Renders the comparison as a fixed-width table: a header, one line per table, and a totals line.
    /// </summary>
    public string Render()
    {
        var separator = new string('-', RowWidth);
        var builder = new StringBuilder();

        builder.Append(Header());
        builder.AppendLine().Append(separator);

        foreach (var row in _rows)
        {
            builder.AppendLine().Append(Format(row));
        }

        builder.AppendLine().Append(separator);
        builder.AppendLine().Append(Format(_totals));

        return builder.ToString();
    }

    private static string Header()
    {
        return string.Format(CultureInfo.InvariantCulture, RowFormat, "table", "legacy", "dp", "delta");
    }

    private static string Format(Row row)
    {
        return string.Format(
            CultureInfo.InvariantCulture, RowFormat, row.Table, row.Legacy, row.Dp, Delta(row.Legacy, row.Dp));
    }

    // Percentage change from legacy to dp. A shrink reads negative; growth reads positive. With no legacy baseline the
    // ratio is undefined, so report it as not-applicable rather than dividing by zero.
    private static string Delta(long legacy, long dp)
    {
        if (legacy == 0)
        {
            return dp == 0 ? "0.00%" : "n/a";
        }

        var percent = (dp - legacy) * 100.0 / legacy;

        return string.Format(CultureInfo.InvariantCulture, "{0:+0.00;-0.00;0.00}%", percent);
    }

    private sealed record Row(string Table, long Legacy, long Dp);
}

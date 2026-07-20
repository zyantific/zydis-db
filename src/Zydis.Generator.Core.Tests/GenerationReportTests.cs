using System.Collections.Generic;
using System.Linq;

using Xunit;

using Zydis.Generator.Core.DecoderTree.Builder;

namespace Zydis.Generator.Core.Tests;

public class GenerationReportTests
{
    [Fact]
    public void GenerationReport_Render_IsIndependentOfInputOrder()
    {
        var builder = new BuilderStatistics(
            GroupCount: 5,
            NodeCount: 42,
            MaxDepth: 4,
            AverageDepth: 3.5,
            MemoHits: 900,
            BudgetBailouts: 1,
            Tables:
            [
                new TableStatistics("PRIMARY", 3, 4, 3.0, 500, 0),
                new TableStatistics("VEX_66_0F", 2, 4, 4.0, 400, 1)
            ]);

        var emissionForward = new List<TableEmissionStatistics>
        {
            new("PRIMARY", 120, 30, 0),
            new("VEX_66_0F", 80, 20, 2)
        };

        var emissionReversed = Enumerable.Reverse(emissionForward).ToList();

        var tablesReversed = new BuilderStatistics(
            builder.GroupCount, builder.NodeCount, builder.MaxDepth, builder.AverageDepth, builder.MemoHits,
            builder.BudgetBailouts, builder.Tables.Reverse().ToArray());

        var first = GenerationReport.Create(builder, emissionForward).Render();
        var second = GenerationReport.Create(tablesReversed, emissionReversed).Render();

        Assert.Equal(first, second);

        // The totals line aggregates every table; ordering must not change what it reports.
        Assert.Contains("distinct interned nodes: 42", first);
        Assert.Contains("TOTAL", first);
    }

    [Fact]
    public void GenerationReport_Render_ReflectsJoinedRowValues()
    {
        var builder = new BuilderStatistics(
            GroupCount: 3,
            NodeCount: 10,
            MaxDepth: 2,
            AverageDepth: 1.5,
            MemoHits: 7,
            BudgetBailouts: 0,
            Tables: [new TableStatistics("PRIMARY", 3, 2, 1.5, 7, 0)]);

        var emission = new List<TableEmissionStatistics> { new("PRIMARY", 64, 16, 3) };

        var lines = GenerationReport.Create(builder, emission).Render().Split('\n');

        var primary = Assert.Single(lines, line => line.StartsWith("PRIMARY", System.StringComparison.Ordinal));

        // Builder columns (groups, depths, memo hits, bailouts) join with emission columns (nodes, size, clones).
        foreach (var value in new[] { "3", "16", "64", "1.50", "7" })
        {
            Assert.Contains(value, primary);
        }
    }

    [Fact]
    public void SizeComparisonReport_Render_ReportsSignedDeltaAndTotals()
    {
        var legacy = new List<TableEmissionStatistics>
        {
            new("PRIMARY", 100, 0, 0),
            new("VEX_66_0F", 200, 0, 0)
        };

        var dp = new List<TableEmissionStatistics>
        {
            new("PRIMARY", 90, 0, 0),
            new("VEX_66_0F", 200, 0, 0)
        };

        var rendered = SizeComparisonReport.Create(legacy, dp).Render();
        var lines = rendered.Split('\n');

        var primary = Assert.Single(lines, line => line.StartsWith("PRIMARY", System.StringComparison.Ordinal));
        Assert.Contains("-10.00%", primary);

        var unchanged = Assert.Single(lines, line => line.StartsWith("VEX_66_0F", System.StringComparison.Ordinal));
        Assert.Contains("0.00%", unchanged);

        // Totals: 300 legacy, 290 dp -> -3.33%.
        var totals = Assert.Single(lines, line => line.StartsWith("TOTAL", System.StringComparison.Ordinal));
        Assert.Contains("300", totals);
        Assert.Contains("290", totals);
        Assert.Contains("-3.33%", totals);
    }

    [Fact]
    public void SizeComparisonReport_Render_MarksMissingLegacyBaselineAsNotApplicable()
    {
        var legacy = new List<TableEmissionStatistics>();
        var dp = new List<TableEmissionStatistics> { new("PRIMARY", 50, 0, 0) };

        var primary = SizeComparisonReport.Create(legacy, dp).Render()
            .Split('\n')
            .Single(line => line.StartsWith("PRIMARY", System.StringComparison.Ordinal));

        Assert.Contains("n/a", primary);
    }
}

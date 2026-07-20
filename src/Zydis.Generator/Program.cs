using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Zydis.Generator.Core;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.DecoderTree.Emitters;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator;

internal sealed class Program
{
    private enum TreeMode
    {
        Dp,
        Verify,
        MigrateOrder,
        Lint
    }

    private static async Task<int> Main(string[] args)
    {
        var mode = TreeMode.Dp;
        var positional = new List<string>();

        foreach (var arg in args)
        {
            if (arg.StartsWith("--tree=", StringComparison.Ordinal))
            {
                var value = arg["--tree=".Length..];
                switch (value)
                {
                    case "dp": mode = TreeMode.Dp; break;
                    case "verify": mode = TreeMode.Verify; break;
                    case "migrate-order": mode = TreeMode.MigrateOrder; break;
                    case "lint": mode = TreeMode.Lint; break;
                    default:
                        await Console.Error.WriteLineAsync(
                                $"unknown --tree value '{value}'. Available modes:")
                            .ConfigureAwait(false);
                        await Console.Error.WriteLineAsync("  dp - generate decoder tables")
                            .ConfigureAwait(false);
                        await Console.Error.WriteLineAsync("  verify - verify decoder table equivalence")
                            .ConfigureAwait(false);
                        await Console.Error.WriteLineAsync(
                                "  migrate-order - one-time: reserialize filters in optimizer-chosen order")
                            .ConfigureAwait(false);
                        await Console.Error.WriteLineAsync(
                                "  lint - report definitions whose filter arrangement is no longer optimal")
                            .ConfigureAwait(false);
                        return 1;
                }

                continue;
            }

            positional.Add(arg);
        }

        return mode switch
        {
            TreeMode.Dp => await RunDpAsync(positional).ConfigureAwait(false),
            TreeMode.Verify => await RunVerifyAsync(positional).ConfigureAwait(false),
            TreeMode.MigrateOrder => await RunMigrateOrderAsync(positional).ConfigureAwait(false),
            TreeMode.Lint => await RunLintAsync(positional).ConfigureAwait(false),
            _ => 1
        };
    }

    private static async Task<int> RunDpAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count != 2)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator path/to/datafiles/ path/to/zydis/").ConfigureAwait(false);
            return 1;
        }

        var generator = new ZydisGenerator();

        await generator.ReadDefinitionsAsync(positional[0]).ConfigureAwait(false);
        await generator.GenerateDataTablesAsync(positional[1]).ConfigureAwait(false);

        var report = GenerationReport.Create(
            generator.DecoderTreeStatistics, generator.DecoderTableEmissionStatistics);

        Console.WriteLine("decoder tables (variable-position):");
        Console.WriteLine(report.Render());

        return 0;
    }

    private static async Task<int> RunVerifyAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count is < 1 or > 2)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator --tree=verify path/to/datafiles/ [path/to/zydis/ (ignored)]").ConfigureAwait(false);
            return 1;
        }

        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in ReadDefinitionsAsync(positional[0]).ConfigureAwait(false))
        {
            definitions.Add(definition);
        }

        var referenceTables = RegionEquivalenceChecker.BuildReferenceModel(definitions);

        var dpBuilder = new VariablePositionTreeBuilder();
        foreach (var definition in definitions)
        {
            dpBuilder.InsertDefinition(definition);
        }

        dpBuilder.Build();
        dpBuilder.InsertOpcodeTableSwitchNodes();

        var results = RegionEquivalenceChecker.Verify(referenceTables, dpBuilder.OpcodeTables);

        var equivalent = true;
        var maxPoints = 0L;

        foreach (var result in results.OrderBy(result => result.Table, StringComparer.Ordinal))
        {
            maxPoints = Math.Max(maxPoints, result.MaxPointCount);

            if (result.Differences.Count == 0)
            {
                Console.WriteLine($"REGION EQUIVALENCE OK {result.Table}");
                continue;
            }

            equivalent = false;
            Console.WriteLine($"REGION EQUIVALENCE FAILED {result.Table} ({result.Differences.Count} difference(s))");
            foreach (var difference in result.Differences)
            {
                Console.WriteLine($"  {difference}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("variable-position tree:");
        Console.WriteLine(dpBuilder.Statistics.Render());
        Console.WriteLine();
        Console.WriteLine($"max per-definition point cross-product: {maxPoints}");

        // Emitted sizes come from laying out both trees into throwaway buffers, so verify mode never writes output.
        var comparison = SizeComparisonReport.Create(
            DecoderTableEmissionMeasurer.Measure(referenceTables),
            DecoderTableEmissionMeasurer.Measure(dpBuilder.OpcodeTables));

        Console.WriteLine();
        Console.WriteLine("decoder table size (reference vs variable-position):");
        Console.WriteLine(comparison.Render());

        if (!equivalent)
        {
            Console.WriteLine();
            Console.WriteLine("result: DIFFERENCES FOUND");
            return 2;
        }

        Console.WriteLine();

        Console.WriteLine("result: ALL TABLES EQUIVALENT");
        return 0;
    }

    // One-time migration (see docs/superpowers/plans/2026-07-20-filter-order-followups.md, Task 6): records each
    // definition's optimizer-chosen filter-test order as its own "filters" key order, so a future filter-order
    // change is visible in the data diff instead of only in decoder table output.
    private static async Task<int> RunMigrateOrderAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count != 1)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator --tree=migrate-order path/to/datafiles/").ConfigureAwait(false);
            return 1;
        }

        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in ReadDefinitionsAsync(positional[0]).ConfigureAwait(false))
        {
            definitions.Add(definition);
        }

        var builder = new VariablePositionTreeBuilder();
        foreach (var definition in definitions)
        {
            builder.InsertDefinition(definition);
        }

        var orders = new Dictionary<InstructionDefinition, IReadOnlyList<FilterKey>>();
        foreach (var group in builder.BuildGroups())
        {
            foreach (var (definition, order) in FilterOrderExtractor.ExtractOrders(group))
            {
                orders[definition] = order;
            }
        }

        var reordered = definitions.Select(definition => ReorderPattern(definition, orders.GetValueOrDefault(definition))).ToList();
        await DefinitionWriter.WriteAsync(Path.Join(positional[0], "instructions.json"), reordered).ConfigureAwait(false);

        Console.WriteLine(
            $"Reordered {orders.Count} of {definitions.Count} definitions " +
            $"({definitions.Count - orders.Count} had no group-derived order and were left as-is).");
        return 0;
    }

    // Rewrites 'definition's Pattern so its key order matches 'order' (the root-to-leaf order the optimizer
    // actually tested it in). A definition with no group-derived order (e.g. one that failed to route to any
    // opcode table) or with 0-1 filters is returned unchanged, since there is nothing meaningful to reorder.
    private static InstructionDefinition ReorderPattern(InstructionDefinition definition, IReadOnlyList<FilterKey>? order)
    {
        if (order is null || definition.Pattern is null || definition.Pattern.Count <= 1)
        {
            return definition;
        }

        var reordered = new Dictionary<string, JsonElement>();
        foreach (var key in order)
        {
            if (definition.Pattern.TryGetValue(key.Name, out var value))
            {
                reordered[key.Name] = value;
            }
        }

        // Fails safe rather than silently dropping a filter: every key of 'definition.Pattern' is expected to
        // already be covered by 'order' per Task 5's invariant, so this loop should be a no-op in practice.
        foreach (var (key, value) in definition.Pattern)
        {
            reordered.TryAdd(key, value);
        }

        return definition with { Pattern = reordered };
    }

    // Advisory check (see docs/superpowers/plans/2026-07-20-filter-order-followups.md, Task 9): compares each
    // definition's checked-in "filters" key order against what re-running --tree=migrate-order would write, without
    // touching any file. Always exits 0 so it can run unconditionally in CI without gating the build on findings.
    private static async Task<int> RunLintAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count != 1)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator --tree=lint path/to/datafiles/").ConfigureAwait(false);
            return 1;
        }

        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in ReadDefinitionsAsync(positional[0]).ConfigureAwait(false))
        {
            definitions.Add(definition);
        }

        var builder = new VariablePositionTreeBuilder();
        foreach (var definition in definitions)
        {
            builder.InsertDefinition(definition);
        }

        var findings = FilterOrderLint.Run(builder.BuildGroups());

        if (findings.Count == 0)
        {
            Console.WriteLine("No stale filter arrangements found.");
        }
        else
        {
            Console.WriteLine($"{findings.Count} definition(s) have a stale filter arrangement " +
                               "(run --tree=migrate-order to fix):");
            foreach (var finding in findings.OrderBy(f => f.Definition.Mnemonic, StringComparer.Ordinal))
            {
                Console.WriteLine(
                    $"  {finding.Definition.Mnemonic}: recorded=[{string.Join(",", finding.RecordedOrder)}] " +
                    $"current=[{string.Join(",", finding.CurrentOrder)}]");
            }
        }

        return 0; // always advisory - never fail the build on findings
    }

    private static IAsyncEnumerable<InstructionDefinition> ReadDefinitionsAsync(string datafilesPath)
    {
        return DefinitionReader.ReadAsync<InstructionDefinition>(Path.Join(datafilesPath, "instructions.json"));
    }
}

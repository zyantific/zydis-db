using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Zydis.Generator.Core;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator;

internal sealed class Program
{
    private enum TreeMode
    {
        Legacy,
        Dp,
        Verify
    }

    private static async Task<int> Main(string[] args)
    {
        var mode = TreeMode.Legacy;
        var positional = new List<string>();

        foreach (var arg in args)
        {
            if (arg.StartsWith("--tree=", StringComparison.Ordinal))
            {
                var value = arg["--tree=".Length..];
                switch (value)
                {
                    case "legacy": mode = TreeMode.Legacy; break;
                    case "dp": mode = TreeMode.Dp; break;
                    case "verify": mode = TreeMode.Verify; break;
                    default:
                        await Console.Error.WriteLineAsync($"unknown --tree value '{value}' (expected legacy|dp|verify)")
                            .ConfigureAwait(false);
                        return 1;
                }

                continue;
            }

            positional.Add(arg);
        }

        return mode switch
        {
            TreeMode.Legacy => await RunLegacyAsync(positional).ConfigureAwait(false),
            TreeMode.Dp => await RunDpAsync(positional).ConfigureAwait(false),
            TreeMode.Verify => await RunVerifyAsync(positional).ConfigureAwait(false),
            _ => 1
        };
    }

    private static async Task<int> RunLegacyAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count != 2)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator [--tree=legacy] [path/to/datafiles/] [path/to/zydis/]").ConfigureAwait(false);
            return 1;
        }

        var generator = new ZydisGenerator();

        await generator.ReadDefinitionsAsync(positional[0]).ConfigureAwait(false);
        await generator.GenerateDataTablesAsync(positional[1]).ConfigureAwait(false);

        return 0;
    }

    private static async Task<int> RunDpAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count is < 1 or > 2)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator --tree=dp [path/to/datafiles/] [path/to/zydis/]").ConfigureAwait(false);
            return 1;
        }

        var builder = new VariablePositionTreeBuilder();
        await foreach (var definition in ReadDefinitionsAsync(positional[0]).ConfigureAwait(false))
        {
            builder.InsertDefinition(definition);
        }

        builder.Build();
        builder.InsertOpcodeTableSwitchNodes();

        Console.WriteLine("variable-position tree:");
        Console.WriteLine(builder.Statistics.Render());

        // Emission for the variable-position tree is not wired yet, so no output files are written here.
        return 0;
    }

    private static async Task<int> RunVerifyAsync(IReadOnlyList<string> positional)
    {
        if (positional.Count is < 1 or > 2)
        {
            await Console.Error.WriteLineAsync(
                "usage: Zydis.Generator --tree=verify [path/to/datafiles/] [path/to/zydis/ (ignored)]").ConfigureAwait(false);
            return 1;
        }

        var legacyBuilder = new DecoderTreeBuilder();
        var dpBuilder = new VariablePositionTreeBuilder();

        await foreach (var definition in ReadDefinitionsAsync(positional[0]).ConfigureAwait(false))
        {
            legacyBuilder.InsertDefinition(definition);
            dpBuilder.InsertDefinition(definition);
        }

        legacyBuilder.InsertOpcodeTableSwitchNodes();
        legacyBuilder.Optimize();

        dpBuilder.Build();
        dpBuilder.InsertOpcodeTableSwitchNodes();

        var results = RegionEquivalenceChecker.Verify(legacyBuilder.OpcodeTables, dpBuilder.OpcodeTables);

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

        if (!equivalent)
        {
            Console.WriteLine("result: DIFFERENCES FOUND");
            return 2;
        }

        Console.WriteLine("result: ALL TABLES EQUIVALENT");
        return 0;
    }

    private static IAsyncEnumerable<InstructionDefinition> ReadDefinitionsAsync(string datafilesPath)
    {
        return DefinitionReader.ReadAsync<InstructionDefinition>(Path.Join(datafilesPath, "instructions.json"));
    }
}

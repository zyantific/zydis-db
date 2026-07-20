using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Tests;

public class VariablePositionBuilderTests
{
    [Fact]
    public async Task FullCorpus_BuildsWithoutErrors()
    {
        var builder = new VariablePositionTreeBuilder();
        await InsertCorpusAsync(builder.InsertDefinition);

        builder.Build();
        builder.InsertOpcodeTableSwitchNodes();

        Assert.True(builder.Statistics.GroupCount > 0, "expected at least one constructed group");
        Assert.True(builder.Statistics.NodeCount > 0, "expected interned nodes");
        Assert.Equal(0, builder.Statistics.BudgetBailouts);
        Assert.Empty(CollectOverflowNodes(builder.OpcodeTables));

        // 3DNow! groups build like any other; every 0F0F suffix splits on modrm_mod, so the table is at least one deep.
        var amd3dnow = builder.Statistics.Tables.Single(table => table.Table.Contains("3DNW", StringComparison.Ordinal));
        Assert.True(amd3dnow.MaxDepth >= 1, $"expected 3DNow! depth >= 1, got {amd3dnow.MaxDepth}");

        // The mandatory prefix is the opcode-table identity for every vector encoding, so no vector table may
        // contain a MandatoryPrefixNode.
        Assert.Empty(CollectVectorMandatoryPrefixNodes(builder.OpcodeTables));
    }

    [Fact]
    public async Task VectorGroup_StripsMandatoryPrefixConstraint()
    {
        // Both members route to the VEX 66 MAP0 table via their mandatory prefix, then refine on modrm_reg. The
        // mandatory prefix is the table identity here, so the built subtree must refine on modrm_reg alone and never
        // materialise a MandatoryPrefixNode (whose only live slot would dead-end to INVALID at runtime).
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await DefinitionAsync("A", "10", """{"mandatory_prefix":"66","modrm_reg":"0"}""", "vex"));
        builder.InsertDefinition(await DefinitionAsync("B", "10", """{"mandatory_prefix":"66","modrm_reg":"1"}""", "vex"));

        builder.Build();

        var root = builder.OpcodeTables
            .GetTable(InstructionEncoding.VEX, OpcodeMap.MAP0, RefiningPrefix.P66)[DecisionNodeIndex.ForIndex(0x10)];

        Assert.NotNull(root);
        Assert.IsNotType<MandatoryPrefixNode>(root);
        Assert.Empty(CollectVectorMandatoryPrefixNodes(builder.OpcodeTables));
    }

    [Fact]
    public async Task ReferenceModel_FullCorpus_ContainsNoOverflowNode()
    {
        // OverflowNode has EncodedSize 0, so the emitter treats it as a leaf and silently drops its collided children.
        // The fixed-order reference tree must therefore never contain one for the real corpus.
        var corpus = new List<InstructionDefinition>();
        await InsertCorpusAsync(corpus.Add);

        var tables = RegionEquivalenceChecker.BuildReferenceModel(corpus);

        Assert.Empty(CollectOverflowNodes(tables));
    }

    [Fact]
    public async Task Build_ValidGroup_AssignsRootAndPopulatesStatistics()
    {
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await DefinitionAsync("A", "10", """{"rex_w":"0"}"""));
        builder.InsertDefinition(await DefinitionAsync("B", "10", """{"rex_w":"1"}"""));

        builder.Build();

        var root = builder.OpcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null)[DecisionNodeIndex.ForIndex(0x10)];
        Assert.IsType<RexWNode>(root);
        Assert.Equal(1, builder.Statistics.GroupCount);
        Assert.True(builder.Statistics.NodeCount > 0);
    }

    [Fact]
    public async Task BuildGroups_YieldsOneEntryPerBucketWithMatchingMembersAndResult()
    {
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"3"}"""));
        builder.InsertDefinition(await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"!3"}"""));

        var groups = builder.BuildGroups().ToList();

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Members.Count);
        Assert.NotNull(group.Result.Root);
    }

    [Fact]
    public async Task Build_ConflictsAcrossGroups_AggregatesEveryProblem()
    {
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await DefinitionAsync("A", "10", """{"rex_w":"0"}"""));
        builder.InsertDefinition(await DefinitionAsync("B", "10", """{"mode":"64"}"""));
        builder.InsertDefinition(await DefinitionAsync("C", "20", """{"rex_w":"0"}"""));
        builder.InsertDefinition(await DefinitionAsync("D", "20", """{"mode":"64"}"""));

        var exception = Assert.Throws<DecoderTreeBuildException>(builder.Build);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains(exception.Errors, error => error.Contains("PRIMARY[0x10]", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("PRIMARY[0x20]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Build_AggregatedErrors_AreSortedDeterministically()
    {
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await DefinitionAsync("A", "20", """{"rex_w":"0"}"""));
        builder.InsertDefinition(await DefinitionAsync("B", "20", """{"mode":"64"}"""));
        builder.InsertDefinition(await DefinitionAsync("C", "10", """{"rex_w":"0"}"""));
        builder.InsertDefinition(await DefinitionAsync("D", "10", """{"mode":"64"}"""));

        var exception = Assert.Throws<DecoderTreeBuildException>(builder.Build);

        var sorted = new List<string>(exception.Errors);
        sorted.Sort(StringComparer.Ordinal);
        Assert.Equal(sorted, exception.Errors);
    }

    [Fact]
    public async Task Build_GroupNameCarriesTableIdentityAndOpcode()
    {
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await DefinitionAsync("A", "1F", """{"mode":"64"}"""));
        builder.InsertDefinition(await DefinitionAsync("B", "1F", """{"mode":"64"}"""));

        var exception = Assert.Throws<DecoderTreeBuildException>(builder.Build);

        Assert.Single(exception.Errors);
        Assert.Contains("PRIMARY[0x1F]", exception.Errors[0], StringComparison.Ordinal);
    }

    private static async Task InsertCorpusAsync(Action<InstructionDefinition> insert)
    {
        await foreach (var definition in DefinitionReader
            .ReadAsync<InstructionDefinition>(LocateDatafile("instructions.json")).ConfigureAwait(false))
        {
            insert(definition);
        }
    }

    // Walks only the vector tables (VEX/EVEX/MVEX/XOP), where the mandatory prefix is the table identity. The Default
    // and 3DNow! tables legitimately test the mandatory prefix as an in-group filter, so they are excluded.
    private static List<MandatoryPrefixNode> CollectVectorMandatoryPrefixNodes(OpcodeTables tables)
    {
        var found = new List<MandatoryPrefixNode>();
        var visited = new HashSet<DecoderTreeNode>(ReferenceEqualityComparer.Instance);

        foreach (var table in tables.Tables)
        {
            var name = table.ToString()!;
            if (name.StartsWith("VEX", StringComparison.Ordinal) ||
                name.StartsWith("EVEX", StringComparison.Ordinal) ||
                name.StartsWith("MVEX", StringComparison.Ordinal) ||
                name.StartsWith("XOP", StringComparison.Ordinal))
            {
                Walk(table);
            }
        }

        return found;

        void Walk(DecoderTreeNode? node)
        {
            if (node is null || !visited.Add(node))
            {
                return;
            }

            if (node is MandatoryPrefixNode mandatory)
            {
                found.Add(mandatory);
            }

            if (node is DecisionNode decision)
            {
                foreach (var (_, child) in decision.EnumerateVirtualSlots())
                {
                    Walk(child);
                }

                Walk(decision.ElseEntry);
            }
        }
    }

    private static List<OverflowNode> CollectOverflowNodes(OpcodeTables tables)
    {
        var overflow = new List<OverflowNode>();
        var visited = new HashSet<DecoderTreeNode>(ReferenceEqualityComparer.Instance);

        foreach (var table in tables.Tables)
        {
            Walk(table);
        }

        return overflow;

        void Walk(DecoderTreeNode? node)
        {
            if (node is null || !visited.Add(node))
            {
                return;
            }

            switch (node)
            {
                case OverflowNode overflowNode:
                    overflow.Add(overflowNode);
                    foreach (var child in overflowNode.Children)
                    {
                        Walk(child);
                    }

                    break;
                case DecisionNode decision:
                    foreach (var (_, child) in decision.EnumerateVirtualSlots())
                    {
                        Walk(child);
                    }

                    Walk(decision.ElseEntry);
                    break;
            }
        }
    }

    private static string LocateDatafile(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "datafiles", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate 'datafiles/{name}' above '{AppContext.BaseDirectory}'.");
    }

    private static string Definition(string mnemonic, string opcode, string filtersJson, string encoding) =>
        $$$"""{"mnemonic":"{{{mnemonic}}}","opcode":"{{{opcode}}}","encoding":"{{{encoding}}}","filters":{{{filtersJson}}},"meta_info":{}}""";

    private static async Task<InstructionDefinition> DefinitionAsync(
        string mnemonic, string opcode, string filtersJson, string encoding = "default")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, $"[{Definition(mnemonic, opcode, filtersJson, encoding)}]").ConfigureAwait(false);

            await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(path).ConfigureAwait(false))
            {
                return definition;
            }

            throw new InvalidOperationException("No definition was parsed from the test fixture.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

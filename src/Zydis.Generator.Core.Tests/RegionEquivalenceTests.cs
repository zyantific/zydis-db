using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Tests;

public class RegionEquivalenceTests
{
    // Two trees describing identical regions but testing the filters in a different order must compare equal.
    // Legacy fixed order tests rex_w first; the differently-shaped DAG tests rex_b first.
    [Fact]
    public async Task DifferentFilterOrder_SameRegions_AreEquivalent()
    {
        var a = await DefinitionAsync("A", "10", "{}");
        var b = await DefinitionAsync("B", "10", "{}");
        var c = await DefinitionAsync("C", "10", "{}");

        // rex_w first: A@{w0,b0}, C@{w0,b1}, B@{w1}.
        var legacy = new RexWNode
        {
            [DecisionNodeIndex.ForIndex(0)] = new RexBNode
            {
                [DecisionNodeIndex.ForIndex(0)] = new DefinitionNode(a),
                [DecisionNodeIndex.ForIndex(1)] = new DefinitionNode(c)
            },
            [DecisionNodeIndex.ForIndex(1)] = new DefinitionNode(b)
        };

        // rex_b first: same regions, B replicated into both rex_b branches' rex_w=1 slot.
        var dp = new RexBNode
        {
            [DecisionNodeIndex.ForIndex(0)] = new RexWNode
            {
                [DecisionNodeIndex.ForIndex(0)] = new DefinitionNode(a),
                [DecisionNodeIndex.ForIndex(1)] = new DefinitionNode(b)
            },
            [DecisionNodeIndex.ForIndex(1)] = new RexWNode
            {
                [DecisionNodeIndex.ForIndex(0)] = new DefinitionNode(c),
                [DecisionNodeIndex.ForIndex(1)] = new DefinitionNode(b)
            }
        };

        var differences = RegionEquivalenceChecker.CompareGroup("PRIMARY", 0x10, legacy, dp);

        Assert.Empty(differences);
    }

    // A mandatory ignore-fallback group: the reference tree splits on the mandatory prefix and parks the
    // unconstrained member in the ignore slot; the DAG replicates that member instead. The ignore member
    // must be reachable at every prefix where the concrete slot dead-ends (here: none/66/f2, and f3 with
    // mode != 64), which only matches if the ignore-slot complement is modelled.
    [Fact]
    public async Task MandatoryIgnoreFallback_ReferenceAndDp_AreEquivalent()
    {
        var members = new[]
        {
            await DefinitionAsync("A", "10", """{"mandatory_prefix":"f3","mode":"64"}"""),
            await DefinitionAsync("B", "10", "{}")
        };

        var (reference, dp) = BuildBoth(members, 0x10);

        var differences = RegionEquivalenceChecker.CompareGroup("PRIMARY", 0x10, reference, dp);

        Assert.Empty(differences);
    }

    // The frozen legacy builder inserts a mandatory-prefix node purely from whether a definition's raw pattern
    // carries the key - it has no notion of "ignore" as a value. A definition with no mandatory-prefix opinion at
    // all (how "no constraint" is expressed once the retired "ignore" alias is gone from the data) sharing a
    // bucket with an explicit-value sibling would otherwise land at the same tree position as that sibling's node
    // and collide into an OverflowNode; BuildReferenceModel must reconstruct the retired marker to keep them apart.
    [Fact]
    public async Task BuildReferenceModel_UnconstrainedSiblingOfExplicitValue_DoesNotOverflow()
    {
        var members = new[]
        {
            await DefinitionAsync("A", "10", """{"mandatory_prefix":"f3"}"""),
            await DefinitionAsync("B", "10", "{}")
        };

        var tables = RegionEquivalenceChecker.BuildReferenceModel(members);

        var root = tables.GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null)[DecisionNodeIndex.ForIndex(0x10)];

        Assert.IsNotType<OverflowNode>(root);
    }

    // Dropping a branch from the DAG makes a definition unreachable that the legacy tree still reaches;
    // the checker must report a difference that names the missing definition.
    [Fact]
    public async Task DroppedElse_ProducesDifferenceNamingDefinition()
    {
        var a = await DefinitionAsync("A", "10", "{}");
        var b = await DefinitionAsync("B", "10", "{}");

        var legacy = new RexWNode
        {
            [DecisionNodeIndex.ForIndex(0)] = new DefinitionNode(a),
            ElseEntry = new DefinitionNode(b)
        };

        // The else that carried B is gone, so B is unreachable in the DAG.
        var dp = new RexWNode
        {
            [DecisionNodeIndex.ForIndex(0)] = new DefinitionNode(a)
        };

        var differences = RegionEquivalenceChecker.CompareGroup("PRIMARY", 0x10, legacy, dp);

        Assert.NotEmpty(differences);
        Assert.Contains(differences, difference => difference.Contains('B'));
    }

    private static (DecoderTreeNode? Reference, DecoderTreeNode? Dp) BuildBoth(
        IReadOnlyList<InstructionDefinition> members, int opcode)
    {
        var referenceTables = RegionEquivalenceChecker.BuildReferenceModel(members);

        var dpBuilder = new VariablePositionTreeBuilder();
        foreach (var member in members)
        {
            dpBuilder.InsertDefinition(member);
        }

        dpBuilder.Build();
        dpBuilder.InsertOpcodeTableSwitchNodes();

        var referenceRoot = referenceTables
            .GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null)[DecisionNodeIndex.ForIndex(opcode)];
        var dpRoot = dpBuilder.OpcodeTables
            .GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null)[DecisionNodeIndex.ForIndex(opcode)];

        return (referenceRoot, dpRoot);
    }

    private static string DefinitionJson(string mnemonic, string opcode, string filtersJson) =>
        $$$"""{"mnemonic":"{{{mnemonic}}}","opcode":"{{{opcode}}}","filters":{{{filtersJson}}},"meta_info":{}}""";

    private static async Task<InstructionDefinition> DefinitionAsync(string mnemonic, string opcode, string filtersJson)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, $"[{DefinitionJson(mnemonic, opcode, filtersJson)}]").ConfigureAwait(false);

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

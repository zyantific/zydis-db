using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Tests;

public class TreeConstructorTests
{
    // Fixed `FilterOrder` for the default encoding; index doubles as tie-break priority (lower wins).
    private static readonly string[] DefaultTieBreak =
    [
        "rex_2", "feature_mpx", "feature_ud0_compat", "modrm_mod", "feature_cldemote", "prefix_group1",
        "mandatory_prefix", "modrm_reg", "modrm_rm", "mode", "address_size", "operand_size", "rex_w", "rex_b",
        "feature_amd", "feature_knc", "feature_cet", "feature_lzcnt", "feature_tzcnt", "feature_wbnoinvd",
        "feature_centaur", "feature_iprefetch"
    ];

    [Fact]
    public async Task AllConstrainSameFilter_SingleNodePerFilter()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"rex_w":"0"}"""),
            await MemberAsync("B", """{"rex_w":"1"}""")
        };

        var result = Construct(members);

        var root = Assert.IsType<RexWNode>(result.Root);
        AssertLeaf("A", root[DecisionNodeIndex.ForIndex(0)]);
        AssertLeaf("B", root[DecisionNodeIndex.ForIndex(1)]);
        Assert.Null(root.ElseEntry);
    }

    [Fact]
    public async Task DontCare_LandsInElse()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"rex_w":"1"}"""),
            await MemberAsync("B", """{}""")
        };

        var result = Construct(members);

        var root = Assert.IsType<RexWNode>(result.Root);
        AssertLeaf("A", root[DecisionNodeIndex.ForIndex(1)]);
        AssertLeaf("B", root.ElseEntry);
        // The unclaimed slot 0 is served by the else entry, not an explicit child.
        Assert.Null(root[DecisionNodeIndex.ForIndex(0)]);
    }

    [Fact]
    public async Task MandatoryStaticFallback_DeepDeadEnd()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"mandatory_prefix":"f3","mode":"64"}"""),
            await MemberAsync("B", """{}""")
        };

        var result = Construct(members);

        var root = Assert.IsType<MandatoryPrefixNode>(result.Root);

        // The f3 slot dead-ends into a mode split; every other prefix falls through to the unconstrained member.
        var pf3 = Assert.IsType<ModeCompactNode>(root[DecisionNodeIndex.ForIndex((int)MandatoryPrefixNode.Slot.PF3)]);
        AssertLeaf("A", pf3[DecisionNodeIndex.ForIndex(0)]);
        AssertLeaf("B", pf3.ElseEntry);

        AssertLeaf("B", root.ElseEntry);

        // The unconstrained member is shared, not duplicated.
        Assert.Same(root.ElseEntry, pf3.ElseEntry);
    }

    [Fact]
    public async Task NegatedMember_ExcludedFromItsSlot()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"mode":"!64"}"""),
            await MemberAsync("B", """{"mode":"64"}""")
        };

        var result = Construct(members);

        var root = Assert.IsType<ModeCompactNode>(result.Root);
        AssertLeaf("B", root[DecisionNodeIndex.ForIndex(0)]);
        AssertLeaf("A", root.ElseEntry);
    }

    [Fact]
    public async Task LoneNegatedMember_MaterializesElseAndKeepsClaimedSlotInvalid()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"mode":"!64"}""")
        };

        var result = Construct(members);

        // The excluded M64 slot must stay invalid, so the else child cannot be a catch-all: it is copied into every
        // other slot instead. With M16 and M32 populated the compaction to `ModeCompactNode` does not apply, so the
        // node stays a full `ModeNode`.
        var root = Assert.IsType<ModeNode>(result.Root);
        AssertLeaf("A", root[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M16)]);
        AssertLeaf("A", root[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M32)]);
        Assert.Null(root[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M64)]);
        Assert.Null(root.ElseEntry);

        // The materialized else child is interned once and shared across the reachable slots.
        Assert.Same(
            root[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M16)],
            root[DecisionNodeIndex.ForIndex((int)ModeNode.Slot.M32)]);
    }

    [Fact]
    public async Task Rex2Pin_ChosenFirst()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"rex_2":"rex2","rex_w":"0"}"""),
            await MemberAsync("B", """{"rex_2":"rex2","rex_w":"1"}""")
        };

        var result = Construct(members);

        Assert.IsType<Rex2Node>(result.Root);
    }

    [Fact]
    public async Task CheaperOrderWins()
    {
        var members = new[]
        {
            await MemberAsync("D1", """{"mode":"64","rex_w":"0"}"""),
            await MemberAsync("D2", """{"mode":"64","rex_w":"1"}"""),
            await MemberAsync("D3", """{"mode":"!64"}""")
        };

        var result = Construct(members);

        // Splitting on mode first shares the not-64 tail once; splitting on rex_w first duplicates it.
        Assert.IsType<ModeCompactNode>(result.Root);
    }

    [Fact]
    public async Task SharedSubtree_InternedOnce()
    {
        var members = new[]
        {
            await MemberAsync("A", """{"rex_w":"1","rex_b":"1"}"""),
            await MemberAsync("B", """{}""")
        };

        var result = Construct(members);

        var root = Assert.IsType<RexWNode>(result.Root);
        var inner = Assert.IsType<RexBNode>(root[DecisionNodeIndex.ForIndex(1)]);

        AssertLeaf("B", root.ElseEntry);
        AssertLeaf("B", inner.ElseEntry);

        // The replicated don't-care collapses to a single interned node.
        Assert.Same(root.ElseEntry, inner.ElseEntry);
    }

    [Fact]
    public async Task Determinism_TwoRunsIdenticalStructure()
    {
        var members = new[]
        {
            await MemberAsync("D1", """{"mode":"64","rex_w":"0"}"""),
            await MemberAsync("D2", """{"mode":"64","rex_w":"1"}"""),
            await MemberAsync("D3", """{"mode":"!64"}"""),
            await MemberAsync("D4", """{}""")
        };

        var first = Construct(members);
        var second = Construct(members);

        Assert.Equal(Serialize(first.Root), Serialize(second.Root));
        Assert.Equal(first.SubproblemCount, second.SubproblemCount);
        Assert.Equal(first.MemoHits, second.MemoHits);
        Assert.False(first.BudgetExhausted);
    }

    [Fact]
    public async Task Budget_FallbackStillCorrect()
    {
        var members = new[]
        {
            await MemberAsync("D1", """{"mode":"64","rex_w":"0"}"""),
            await MemberAsync("D2", """{"mode":"64","rex_w":"1"}"""),
            await MemberAsync("D3", """{"mode":"!64"}""")
        };

        var exact = Construct(members, new ConstructorOptions(ExpansionBudget: 200_000));
        var starved = Construct(members, new ConstructorOptions(ExpansionBudget: 1));

        Assert.False(exact.BudgetExhausted);
        Assert.True(starved.BudgetExhausted);

        // The starved run may pick a worse shape, but must still route to exactly the same definitions.
        Assert.Equal(ReachableDefinitions(exact.Root), ReachableDefinitions(starved.Root));
    }

    [Fact]
    public async Task Evaluate_ImposedExpensiveOrder_CostsAtLeastOptimal()
    {
        var members = new[]
        {
            await MemberAsync("D1", """{"mode":"64","rex_w":"0"}"""),
            await MemberAsync("D2", """{"mode":"64","rex_w":"1"}"""),
            await MemberAsync("D3", """{"mode":"!64"}""")
        };

        var constructor = new TreeConstructor(new NodeInterner(), DefaultTieBreak, new ConstructorOptions());

        // Forcing rex_w before mode duplicates the not-64 tail the optimal tree shares once.
        var (optimal, imposed) = constructor.Evaluate(members, [new FilterKey("rex_w"), new FilterKey("mode")]);

        Assert.True(optimal < imposed, $"expected optimal ({optimal}) < imposed ({imposed})");
    }

    private static ConstructionResult Construct(IReadOnlyList<GroupMember> members, ConstructorOptions? options = null)
    {
        var constructor = new TreeConstructor(new NodeInterner(), DefaultTieBreak, options ?? new ConstructorOptions());

        return constructor.Construct(members);
    }

    private static void AssertLeaf(string mnemonic, DecoderTreeNode? node)
    {
        var definition = Assert.IsType<DefinitionNode>(node);
        Assert.Equal(mnemonic, definition.InstructionDefinition.Mnemonic);
    }

    private static SortedSet<string> ReachableDefinitions(DecoderTreeNode root)
    {
        var result = new SortedSet<string>(StringComparer.Ordinal);
        Walk(root);
        return result;

        void Walk(DecoderTreeNode node)
        {
            switch (node)
            {
                case DefinitionNode definition:
                    result.Add(definition.InstructionDefinition.Mnemonic);
                    break;
                case DecisionNode decision:
                    foreach (var child in Children(decision))
                    {
                        Walk(child);
                    }

                    break;
            }
        }
    }

    private static string Serialize(DecoderTreeNode node)
    {
        switch (node)
        {
            case DefinitionNode definition:
                return $"Def({definition.InstructionDefinition.Mnemonic})";
            case DecisionNode decision:
            {
                var builder = new StringBuilder();
                builder.Append(decision.Definition.Name).Append('[');

                foreach (var (index, child) in decision.EnumerateVirtualSlots())
                {
                    if (child is not null)
                    {
                        builder.Append(index).Append('=').Append(Serialize(child)).Append(',');
                    }
                }

                if (decision.ElseEntry is not null)
                {
                    builder.Append("else=").Append(Serialize(decision.ElseEntry));
                }

                return builder.Append(']').ToString();
            }
            default:
                return node.ToString() ?? string.Empty;
        }
    }

    private static IEnumerable<DecoderTreeNode> Children(DecisionNode node)
    {
        foreach (var (_, child) in node.EnumerateVirtualSlots())
        {
            if (child is not null)
            {
                yield return child;
            }
        }

        if (node.ElseEntry is not null)
        {
            yield return node.ElseEntry;
        }
    }

    private static string WithMnemonic(string mnemonic, string filtersJson) =>
        $$$"""{"mnemonic":"{{{mnemonic}}}","opcode":"00","filters":{{{filtersJson}}},"meta_info":{}}""";

    private static async Task<GroupMember> MemberAsync(string mnemonic, string filtersJson)
    {
        var definition = await ParseDefinitionAsync(WithMnemonic(mnemonic, filtersJson)).ConfigureAwait(false);

        return new GroupMember(definition, ConstraintSet.Parse(definition));
    }

    private static async Task<InstructionDefinition> ParseDefinitionAsync(string definitionJson)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, $"[{definitionJson}]").ConfigureAwait(false);

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

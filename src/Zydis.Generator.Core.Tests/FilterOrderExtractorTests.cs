using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.Tests;

public class FilterOrderExtractorTests
{
    [Fact]
    public async Task ExtractOrders_SimpleGroup_RecordsRootToLeafOrderOfOwnFilters()
    {
        var builder = new VariablePositionTreeBuilder();
        var a = await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"3","rex_w":"1"}""");
        var b = await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"3","rex_w":"0"}""");
        var c = await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"!3"}""");
        builder.InsertDefinition(a);
        builder.InsertDefinition(b);
        builder.InsertDefinition(c);

        var group = builder.BuildGroups().Single();
        var orders = FilterOrderExtractor.ExtractOrders(group);

        // 'a' and 'b' both declare modrm_mod and rex_w; whichever the optimizer tests first, the *order relative to
        // each other* is fixed for both siblings since they share that prefix in the tree.
        Assert.Equal(orders[a].Where(f => f.Name != "rex_w"), orders[b].Where(f => f.Name != "rex_w"));
        Assert.Contains(new FilterKey("modrm_mod"), orders[a]);
        Assert.Contains(new FilterKey("rex_w"), orders[a]);
        // 'c' only declares modrm_mod, so its own order is exactly that single filter.
        Assert.Equal([new FilterKey("modrm_mod")], orders[c]);
    }

    [Fact]
    public async Task ExtractOrders_DontCareLeafReachableViaMultiplePaths_OrderIsPathIndependent()
    {
        // Mirrors the design doc's fallback pattern (an F3-mandatory, mode=64-specific definition with a don't-care
        // else), strengthened two ways over the design doc's own example:
        //  - 'fallback' owns *two* filters of its own (rex_w, rex_b), not zero or one, so there is a genuine
        //    relative order between them for ExtractOrders to get wrong - a single-filter fallback can only ever
        //    produce a trivially "consistent" singleton list regardless of whether extraction is even correct.
        //  - 'specific' shares both exact values with 'fallback', so neither filter ever discriminates between them
        //    and cannot be used to isolate 'fallback' in a single cheap test. That forces the constructor to
        //    replicate 'fallback' across both the mandatory_prefix decision's uncovered slots *and* the nested mode
        //    decision under its f3 slot - two genuinely different decision-node ancestries, confirmed below rather
        //    than assumed - so ExtractOrders' cross-path disagreement check in Record() is actually exercised
        //    against more than one real occurrence, not just a single recorded path.
        var builder = new VariablePositionTreeBuilder();
        var specific = await TestHelpers.ParseDefinitionAsync(
            "x", """{"mandatory_prefix":"f3","mode":"64","rex_w":"0","rex_b":"0"}""");
        var fallback = await TestHelpers.ParseDefinitionAsync("x", """{"rex_w":"0","rex_b":"0"}""");
        builder.InsertDefinition(specific);
        builder.InsertDefinition(fallback);

        var group = builder.BuildGroups().Single();

        // Confirm this is a genuine multi-path leaf - not an accident of one decision node's replicated slots, all
        // of which share a single depth - by checking 'fallback' is reached at more than one distinct tree depth.
        // Only true DAG sharing across different ancestries produces that.
        var depths = CollectLeafDepths(group.Result.Root, fallback, 0);
        Assert.True(depths.Distinct().Count() > 1,
            $"expected 'fallback' to be reachable at more than one tree depth (found: {string.Join(',', depths)}); " +
            "the fixture no longer exercises a genuine multi-path leaf.");

        // ExtractOrders itself throws InvalidOperationException if any two of those paths disagree on 'fallback's
        // own filter order, so simply not throwing here is already part of what this test verifies.
        var orders = FilterOrderExtractor.ExtractOrders(group);

        Assert.Equal([new FilterKey("rex_w"), new FilterKey("rex_b")], orders[fallback]);
    }

    // Records the depth (root = 0) at which every occurrence of `target`'s DefinitionNode is reached, independent of
    // FilterOrderExtractor itself, so the multi-path assertion above does not rely on the code under test.
    private static List<int> CollectLeafDepths(DecoderTreeNode? node, InstructionDefinition target, int depth)
    {
        var depths = new List<int>();
        Walk(node, depth);
        return depths;

        void Walk(DecoderTreeNode? current, int currentDepth)
        {
            switch (current)
            {
                case null:
                    return;
                case DefinitionNode definitionNode:
                    if (ReferenceEquals(definitionNode.InstructionDefinition, target))
                    {
                        depths.Add(currentDepth);
                    }
                    return;
                case DecisionNode decisionNode:
                    foreach (var child in decisionNode.EnumerateSlots())
                    {
                        Walk(child, currentDepth + 1);
                    }
                    return;
            }
        }
    }
}

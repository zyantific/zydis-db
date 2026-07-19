using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Tests;

public class ConstraintsTests
{
    [Fact]
    public async Task Parse_NegatedValue_ProducesComplementMask()
    {
        var definition = await ParseDefinitionAsync(WithFilters("""{"mode":"!64"}"""));

        var set = ConstraintSet.Parse(definition);

        Assert.True(set.TryGet(new FilterKey("mode"), out var constraint));

        var negatedIndex = ModeNode.NodeDefinition.Instance.ParseSlotIndex("64");
        var expected = SlotMask.AllExcept(ModeNode.NodeDefinition.Instance.NumberOfSlots, negatedIndex.Index);

        Assert.Equal(expected, constraint.Slots);
        Assert.True(constraint.Index.IsNegated);
    }

    [Fact]
    public async Task Parse_UnknownFilterKey_Throws()
    {
        var definition = await ParseDefinitionAsync(WithFilters("""{"bogus_filter":"1"}"""));

        Assert.Throws<NotSupportedException>(() => ConstraintSet.Parse(definition));
    }

    [Fact]
    public async Task Parse_MandatoryIgnore_YieldsNoConstraint()
    {
        var definition = await ParseDefinitionAsync(WithFilters("""{"mandatory_prefix":"ignore"}"""));

        var set = ConstraintSet.Parse(definition);

        Assert.Empty(set.Constraints);
    }

    [Fact]
    public async Task Parse_InformationalKeys_AreSkipped()
    {
        // Informational flags with no decision-node counterpart; must not throw and must not constrain.
        var definition = await ParseDefinitionAsync(WithFilters("""{"force_modrm_reg":true,"force_modrm_rm":true}"""));

        var set = ConstraintSet.Parse(definition);

        Assert.Empty(set.Constraints);
    }

    [Fact]
    public async Task Relate_StrictNesting_Detected()
    {
        var first = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"rex_w":"1","mode":"64"}""")));
        var second = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"rex_w":"1"}""")));

        Assert.Equal(RegionRelation.SecondContainsFirst, RegionAlgebra.Relate(first, second));
    }

    [Fact]
    public async Task Relate_SharedFilterDisjointValues_Disjoint()
    {
        var first = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"rex_w":"0"}""")));
        var second = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"rex_w":"1"}""")));

        Assert.Equal(RegionRelation.Disjoint, RegionAlgebra.Relate(first, second));
    }

    [Fact]
    public async Task Relate_DifferentFilters_IncomparableOverlap()
    {
        var first = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"rex_w":"0"}""")));
        var second = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"mode":"64"}""")));

        Assert.Equal(RegionRelation.IncomparableOverlap, RegionAlgebra.Relate(first, second));
    }

    [Fact]
    public async Task Relate_NegationNesting()
    {
        var first = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"mode":"!64"}""")));
        var second = ConstraintSet.Parse(await ParseDefinitionAsync(WithFilters("""{"mode":"32","rex_w":"1"}""")));

        Assert.True(first.TryGet(new FilterKey("mode"), out var firstMode));
        Assert.True(second.TryGet(new FilterKey("mode"), out var secondMode));
        Assert.True(secondMode.Slots.IsSubsetOf(firstMode.Slots));

        Assert.Equal(RegionRelation.FirstContainsSecond, RegionAlgebra.Relate(first, second));
    }

    private static string WithFilters(string filtersJson) =>
        $$$"""{"mnemonic":"test","opcode":"00","filters":{{{filtersJson}}},"meta_info":{}}""";

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

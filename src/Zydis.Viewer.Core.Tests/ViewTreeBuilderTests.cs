using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Viewer.Core.Tree;

using static Zydis.Viewer.Core.Tests.TestDefinitions;

namespace Zydis.Viewer.Core.Tests;

public class ViewTreeBuilderTests
{
    [Fact]
    public void SecondFormWithSameFirstFilterJoinsTheDecisionNode()
    {
        var table = Build(
            Make("adc", 0x10, ("modrm_mod", "0")),
            Make("adc", 0x10, ("modrm_mod", "!0")));

        var decision = Assert.Single(Find(table, "10").Children);
        Assert.Equal(ViewNodeKind.Decision, decision.Kind);
        Assert.Equal("modrm_mod", decision.Filter);
        Assert.Equal(["00 (0)", "!00 (not 0)"], decision.Children.Select(x => x.Label));
        Assert.Equal(0, DefinitionAt(decision, "00 (0)").DefinitionIndex);
        Assert.Equal(1, DefinitionAt(decision, "!00 (not 0)").DefinitionIndex);
    }

    [Fact]
    public void FormWithoutTestForTheSlotFilterGoesToElse()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "0"), ("rex_w", "1")),
            Make("b", 0x10, ("rex_w", "0")));

        Assert.Equal("b", DefinitionAt(table, "10/modrm_mod/else/rex_w/00 (0)").Definition!.Mnemonic);
    }

    [Fact]
    public void StaleOrderUsesTheLaterEntryAtTheSlot()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "0"), ("rex_w", "1")),
            Make("b", 0x10, ("rex_w", "0"), ("modrm_mod", "0")));

        Assert.Equal("b", DefinitionAt(table, "10/modrm_mod/00 (0)/rex_w/00 (0)").Definition!.Mnemonic);
    }

    [Fact]
    public void IdenticalPathsProduceAConflictNodeAndFlagAncestors()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "0")),
            Make("a", 0x10, ("modrm_mod", "0")));

        var slot = Find(table, "10/modrm_mod/00 (0)");
        var conflict = Assert.Single(slot.Children);
        Assert.Equal(ViewNodeKind.Conflict, conflict.Kind);
        Assert.All(conflict.Children, x => Assert.Equal(ViewNodeKind.Definition, x.Kind));
        Assert.Equal([0, 1], conflict.Children.Select(x => x.DefinitionIndex));
        Assert.True(conflict.HasConflict);
        Assert.True(slot.HasConflict);
        Assert.True(Find(table, "10/modrm_mod").HasConflict);
        Assert.True(Find(table, "10").HasConflict);
        Assert.True(table.HasConflict);
    }

    [Fact]
    public void ThirdIdenticalFormJoinsTheExistingConflict()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "0")),
            Make("a", 0x10, ("modrm_mod", "0")),
            Make("a", 0x10, ("modrm_mod", "0")));

        var conflict = Assert.Single(Find(table, "10/modrm_mod/00 (0)").Children);
        Assert.Equal(ViewNodeKind.Conflict, conflict.Kind);
        Assert.Equal([0, 1, 2], conflict.Children.Select(x => x.DefinitionIndex));
    }

    [Fact]
    public void MoreSpecificFormPromotesAConflictIntoElse()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "0")),
            Make("a", 0x10, ("modrm_mod", "0")),
            Make("b", 0x10, ("modrm_mod", "0"), ("rex_w", "1")));

        var rexW = Assert.Single(Find(table, "10/modrm_mod/00 (0)").Children);
        Assert.Equal("rex_w", rexW.Filter);
        var conflict = Assert.Single(Find(rexW, "else").Children);
        Assert.Equal(ViewNodeKind.Conflict, conflict.Kind);
        Assert.Equal([0, 1], conflict.Children.Select(x => x.DefinitionIndex));
        Assert.Equal("b", DefinitionAt(rexW, "01 (1)").Definition!.Mnemonic);
    }

    [Fact]
    public void GeneralFormFirstMovesIntoTheElseSlotOfTheNewNode()
    {
        var table = Build(
            Make("a", 0xA6, ("rex_2", "no_rex2")),
            Make("b", 0xA6, ("rex_2", "no_rex2"), ("mandatory_prefix", "f3")));

        AssertGeneralFormInElse(table);
    }

    [Fact]
    public void GeneralFormLastGoesToTheElseSlot()
    {
        var table = Build(
            Make("b", 0xA6, ("rex_2", "no_rex2"), ("mandatory_prefix", "f3")),
            Make("a", 0xA6, ("rex_2", "no_rex2")));

        AssertGeneralFormInElse(table);
    }

    [Fact]
    public void NegatedAndPositiveSlotsCoexist()
    {
        // The negated form goes in first, so only the sort can put the positive slot row ahead of it.
        var table = Build(
            Make("b", 0x10, ("modrm_mod", "!3")),
            Make("a", 0x10, ("modrm_mod", "0")));

        Assert.Equal(["00 (0)", "!03 (not 3)"], Find(table, "10/modrm_mod").Children.Select(x => x.Label));
    }

    [Fact]
    public async Task ObjectFormFiltersKeepMemberOrder()
    {
        const string json = """
            [{"mnemonic":"a","opcode":"10","filters":{"rex_w":"1","modrm_mod":"3"},
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(stream))
        {
            definitions.Add(definition);
        }

        var decision = Assert.Single(Find(Build([.. definitions]), "10").Children);
        Assert.Equal("rex_w", decision.Filter);
    }

    [Fact]
    public void UnknownFilterNamesMnemonicAndFilter()
    {
        var builder = new ViewTreeBuilder();

        var exception = Assert.Throws<InvalidDataException>(() => builder.Insert(Make("a", 0x10, ("foo", "1")), 0));
        Assert.Contains("'a'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'foo'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownSlotValueNamesMnemonicFilterAndValue()
    {
        var builder = new ViewTreeBuilder();

        var exception = Assert.Throws<InvalidDataException>(
            () => builder.Insert(Make("a", 0x10, ("modrm_mod", "9")), 0));
        Assert.Contains("'a'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'modrm_mod'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'9'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormCountCountsTheSubtree()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "3")),
            Make("b", 0x10, ("modrm_mod", "!3"), ("rex_w", "0")),
            Make("c", 0x10, ("rex_w", "1")));

        Assert.Equal(3, Find(table, "10").FormCount);
        Assert.Equal(3, table.FormCount);
    }

    private static void AssertGeneralFormInElse(ViewNode table)
    {
        var prefix = Assert.Single(Find(table, "A6/rex_2/00 (no_rex2)").Children);
        Assert.Equal("mandatory_prefix", prefix.Filter);
        Assert.Equal(["03 (f3)", "else"], prefix.Children.Select(x => x.Label));
        Assert.Equal("a", DefinitionAt(prefix, "else").Definition!.Mnemonic);
        Assert.Equal("b", DefinitionAt(prefix, "03 (f3)").Definition!.Mnemonic);
        Assert.False(table.HasConflict);
    }
}

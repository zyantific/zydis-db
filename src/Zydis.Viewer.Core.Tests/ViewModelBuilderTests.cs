using System.Linq;

using Xunit;

using Zydis.Viewer.Core.Model;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Tests;

public class ViewModelBuilderTests
{
    [Fact]
    public void OperandSummaryUsesRegisterNamesForImplicitRegisters()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"},
              "operands":[{"operand_type":"implicit_reg","register":"al"},{"operand_type":"gpr16_32_64"}]}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Equal("al, gpr16_32_64", model.Definitions[0].OperandSummary);
        Assert.Equal("al, gpr16_32_64", model.Nodes.Single(x => x.Kind is ViewNodeKind.Definition).Detail);
    }

    [Fact]
    public void OperandSummarySkipsHiddenOperands()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"},
              "operands":[{"operand_type":"gpr16_32_64"},
                          {"operand_type":"implicit_reg","register":"ax","visible":false}]}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Equal("gpr16_32_64", model.Definitions[0].OperandSummary);
    }

    [Fact]
    public void CommentDecodesBase64()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"aaa","opcode":"37","comment":"SUNMQVNTOkFBQQ==",
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Equal("ICLASS:AAA", model.Definitions[0].CommentText);
    }

    [Fact]
    public void InvalidCommentYieldsNull()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"aaa","opcode":"37","comment":"not base64!",
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Null(model.Definitions[0].CommentText);
    }

    [Fact]
    public void NodeIdsArePreorderAndChildIdsResolve()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"b","opcode":"10","filters":[{"filter":"modrm_mod","value":"1"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"c","opcode":"20",
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Equal([0], model.RootIds);
        Assert.Equal(
            [
                "PRIMARY", "10", "modrm_mod", "00 (0)", "Definition", "01 (1)", "Definition", "20", "Definition",
                "D5", "switch_table_rex2", "rex2", "PRIMARY"
            ],
            model.Nodes.Select(x => x.Label));
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], model.Nodes.Select(x => x.Id));
        Assert.Equal([null, 0, 1, 2, 3, 2, 5, 0, 7, 0, 9, 10, 11], model.Nodes.Select(x => x.ParentId));
        Assert.Equal([1, 7, 9], model.Nodes[0].ChildIds);
        Assert.Equal([3, 5], model.Nodes[2].ChildIds);
        Assert.Equal(
            [null, null, null, null, 0, null, 1, null, 2, null, null, null, null],
            model.Nodes.Select(x => x.DefinitionIndex));
    }

    [Fact]
    public void FilterSlotsListEverySlotOfEachDecisionNode()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"b","opcode":"20","filters":[{"filter":"mode","value":"64"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Equal(["mode_compact", "modrm_mod"], model.FilterSlots.Keys.Order());
        Assert.Equal(["0", "1", "2", "3"], model.FilterSlots["modrm_mod"]);
        Assert.Equal(["64", "!64"], model.FilterSlots["mode_compact"]);
    }

    [Fact]
    public void StatsCountConflictRowsNotTheirAncestors()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = ViewModelBuilder.Build(database);

        Assert.Equal(new ViewStats(Definitions: 2, Nodes: 9, Conflicts: 1), model.Stats);
    }
}

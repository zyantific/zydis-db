using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Viewer.Core.Model;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Tests;

public class ViewerDatabaseTests
{
    [Fact]
    public void SchemaIsArrayForTheArrayForm()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        Assert.Equal("array", database.Schema);
    }

    [Fact]
    public void SchemaIsObjectForTheObjectForm()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10",
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"b","opcode":"20","filters":{"modrm_mod":"0"},
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        Assert.Equal("object", database.Schema);
    }

    [Fact]
    public void FormIdCollisionSetsErrorAndFallsBackToIndexIds()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var collision = Assert.Throws<InvalidOperationException>(() => FormId.AssignIds(database.Definitions));

        Assert.Equal(["x#0", "x#1"], database.FormIds);
        Assert.Equal(collision.Message, database.FormIdError);
    }

    [Fact]
    public void FilterWithoutIdAxisSetsErrorAndFallsBackToIndexIds()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"wbnoinvd","opcode":"09","opcode_map":"0f",
              "filters":[{"filter":"feature_wbnoinvd","value":"1"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var unknownAxis = Assert.Throws<NotSupportedException>(() => FormId.AssignIds(database.Definitions));

        Assert.Equal(["wbnoinvd#0"], database.FormIds);
        Assert.Equal(unknownAxis.Message, database.FormIdError);
    }

    [Theory]
    [InlineData("default", "0f38", null, "0F38", "default, 0f38")]
    [InlineData("vex", "0f", "66", "VEX_66_0F", "vex, 0f, 66")]
    public void TableDetailNamesEncodingMapAndPrefix(
        string encoding, string map, string? prefix, string table, string expectedDetail)
    {
        var filters = prefix is null ? "[]" : $$"""[{"filter":"mandatory_prefix","value":"{{prefix}}"}]""";
        using var database = ViewerDatabase.Load(
            $$$"""
            [{"mnemonic":"a","opcode":"10","encoding":"{{{encoding}}}","opcode_map":"{{{map}}}","filters":{{{filters}}},
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var row = Assert.Single(Descendants(database.Roots[0]),
            x => (x.Kind is ViewNodeKind.Table) && (x.Label == table));

        Assert.Equal(expectedDetail, row.Detail);
    }

    [Fact]
    public void ViewModelJsonRoundTripsThroughTheContext()
    {
        using var database = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var json = database.ToViewModelJson();
        var model = JsonSerializer.Deserialize(json, ViewerSerializerContext.Default.ViewModel);

        Assert.NotNull(model);
        Assert.Contains("\"kind\":\"table\"", json, StringComparison.Ordinal);
        Assert.Equal(ViewNodeKind.Table, model.Nodes[0].Kind);
        Assert.Equal("A", model.Definitions[0].FormId);
        Assert.True(JsonElement.DeepEquals(database.Document.RootElement[0], model.Definitions[0].Raw));
        Assert.Equal(["0", "1", "2", "3"], model.FilterSlots["modrm_mod"]);
    }

    private static IEnumerable<ViewNode> Descendants(ViewNode node)
    {
        return node.Children.SelectMany(Descendants).Prepend(node);
    }
}

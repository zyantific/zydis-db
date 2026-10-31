using System.Linq;
using System.Text.Json;

using Xunit;

using Zydis.Viewer.Core.Diff;
using Zydis.Viewer.Core.Model;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Tests;

public class DatabaseDiffTests
{
    [Fact]
    public void PairsByFormId()
    {
        // The filter value changes the region key, so only the form id can pair the two forms.
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"1"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        var pair = Assert.Single(model.Pairs);
        Assert.Equal((0, 0, DiffStatus.Moved), (pair.BaseIndex, pair.HeadIndex, pair.Status));
        Assert.Equal(["filters[0].value"], pair.ChangedPaths);
    }

    [Fact]
    public void FallsBackToTheRegionKeyWhenAnIdLengthened()
    {
        // The second form of the head shares the mnemonic, so the head ids grow the opcode suffix. The head records the
        // filters of the first form in a new order, which the region key ignores.
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10",
              "filters":[{"filter":"modrm_mod","value":"0"},{"filter":"mode","value":"64"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10",
              "filters":[{"filter":"mode","value":"64"},{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"a","opcode":"20","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        Assert.Equal(
            [(0, 0, DiffStatus.Moved), (null, 1, DiffStatus.Added)],
            model.Pairs.Select(x => (x.BaseIndex, x.HeadIndex, x.Status)));
    }

    [Fact]
    public void ReportsMovedWhenTheRecordedOrderChanged()
    {
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10",
              "filters":[{"filter":"modrm_mod","value":"0"},{"filter":"mode","value":"64"}],
              "operands":[{"operand_type":"gpr8"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10",
              "filters":[{"filter":"mode","value":"64"},{"filter":"modrm_mod","value":"0"}],
              "operands":[{"operand_type":"gpr8"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        var pair = Assert.Single(model.Pairs);
        Assert.Equal(DiffStatus.Moved, pair.Status);
        Assert.Equal("PRIMARY/10/modrm_mod/00 (0)/mode_compact/00 (64)", pair.BasePath);
        Assert.Equal("PRIMARY/10/mode_compact/00 (64)/modrm_mod/00 (0)", pair.HeadPath);
        Assert.Equal(
            ["filters[0].filter", "filters[0].value", "filters[1].filter", "filters[1].value"],
            pair.ChangedPaths);
        Assert.Equal(new DiffStats(Added: 0, Removed: 0, Changed: 0, Moved: 1, Unchanged: 0), model.Stats);

        var rows = model.Nodes.Where(x => x.Kind is ViewNodeKind.Definition).ToList();
        Assert.Equal(
            [(DiffStatus.Moved, 0, 0), (DiffStatus.Moved, 0, null)],
            rows.Select(x => (x.Status, x.BaseDefinitionIndex, x.HeadDefinitionIndex)));
        Assert.Equal("gpr8", rows[1].Detail);
        // The old row counts nowhere, so the root counts the form once, as the stats do.
        Assert.Equal(
            new DiffCounts(Added: 0, Removed: 0, Changed: 0, Moved: 1),
            model.Nodes[Assert.Single(model.RootIds)].Counts);
    }

    [Fact]
    public void AddsRemovedPathsFromTheBaseTree()
    {
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"b","opcode":"20","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        var root = model.Nodes[Assert.Single(model.RootIds)];
        var kept = Assert.Single(model.Nodes, x => (x.Kind is ViewNodeKind.Opcode) && (x.Label == "10"));
        var removed = Assert.Single(model.Nodes, x => (x.Kind is ViewNodeKind.Opcode) && (x.Label == "20"));
        Assert.Equal(DiffStatus.Unchanged, kept.Status);
        Assert.Equal(DiffStatus.Removed, removed.Status);
        Assert.Equal(root.Id, removed.ParentId);

        var row = model.Nodes[Assert.Single(removed.ChildIds)];
        Assert.Equal(
            (ViewNodeKind.Definition, DiffStatus.Removed, 1, null),
            (row.Kind, row.Status, row.BaseDefinitionIndex, row.HeadDefinitionIndex));
    }

    [Fact]
    public void RemovedFormAtAnOccupiedPathStaysVisible()
    {
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"b","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        var opcode = Assert.Single(model.Nodes, x => (x.Kind is ViewNodeKind.Opcode) && (x.Label == "10"));
        Assert.Equal(
            [(DiffStatus.Added, null, 0), (DiffStatus.Removed, 0, null)],
            opcode.ChildIds.Select(x => model.Nodes[x])
                .Select(x => (x.Status, x.BaseDefinitionIndex, x.HeadDefinitionIndex)));
    }

    [Fact]
    public void CountsPropagateToAncestors()
    {
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"b","opcode":"10","filters":[{"filter":"modrm_mod","value":"1"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"c","opcode":"20","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","filters":[{"filter":"modrm_mod","value":"0"}],"comment":"QQ==",
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"c","opcode":"20","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"d","opcode":"10","filters":[{"filter":"modrm_mod","value":"2"}],
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        var root = model.Nodes[Assert.Single(model.RootIds)];
        var decision = Assert.Single(model.Nodes, x => x.Kind is ViewNodeKind.Decision);
        var untouched = Assert.Single(model.Nodes, x => (x.Kind is ViewNodeKind.Opcode) && (x.Label == "20"));
        Assert.Equal(new DiffCounts(Added: 1, Removed: 1, Changed: 1, Moved: 0), root.Counts);
        Assert.Equal(new DiffCounts(Added: 1, Removed: 1, Changed: 1, Moved: 0), decision.Counts);
        Assert.Equal(new DiffCounts(Added: 0, Removed: 0, Changed: 0, Moved: 0), untouched.Counts);
    }

    [Fact]
    public void DuplicateFormsFallBackToIndexIds()
    {
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"x","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        Assert.NotNull(baseDb.FormIdError);
        Assert.Null(headDb.FormIdError);
        Assert.Equal(baseDb.FormIdError, model.FormIdError);
        // The base forms share a conflict row, and the head form sits at the opcode row itself.
        Assert.Equal(
            [(0, 0, DiffStatus.Moved), (1, null, DiffStatus.Removed)],
            model.Pairs.Select(x => (x.BaseIndex, x.HeadIndex, x.Status)));
    }

    [Fact]
    public void FallbackIdsPairByRegionKeyNotPosition()
    {
        // The duplicate x forms make both sides fall back to the ids a#0 and a#1, which name different forms of a
        // after the swap.
        using var baseDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"a","opcode":"20","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"30","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"30","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);
        using var headDb = ViewerDatabase.Load(
            """
            [{"mnemonic":"a","opcode":"20","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"a","opcode":"10","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"30","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}},
             {"mnemonic":"x","opcode":"30","meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var model = Diff(baseDb, headDb);

        Assert.Equal(
            [
                (1, 0, DiffStatus.Unchanged), (0, 1, DiffStatus.Unchanged), (2, 2, DiffStatus.Unchanged),
                (3, 3, DiffStatus.Unchanged)
            ],
            model.Pairs.Select(x => (x.BaseIndex, x.HeadIndex, x.Status)));
    }

    /// <summary>
    /// Compares <paramref name="baseDb"/> to <paramref name="headDb"/> and reads the JSON document back, so each test
    /// also covers the serializer context the browser build depends on.
    /// </summary>
    private static DiffModel Diff(ViewerDatabase baseDb, ViewerDatabase headDb)
    {
        var json = DatabaseDiff.Compare(baseDb, headDb).ToJson();
        var model = JsonSerializer.Deserialize(json, ViewerSerializerContext.Default.DiffModel);

        Assert.NotNull(model);

        return model;
    }
}

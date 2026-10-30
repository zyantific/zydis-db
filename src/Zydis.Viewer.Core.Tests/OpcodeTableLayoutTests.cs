using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;
using Zydis.Viewer.Core.Tree;

using static Zydis.Viewer.Core.Tests.TestDefinitions;

namespace Zydis.Viewer.Core.Tests;

public class OpcodeTableLayoutTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("default", "default", null, "PRIMARY")]
    [InlineData("default", "0f38", null, "0F38")]
    [InlineData("evex", "map4", "none", "EVEX_NP_MAP4")]
    [InlineData("3dnow", "0f0f", null, "3DNW_NP_0F0F")]
    public async Task TableOfPicksTheGeneratorTable(string encoding, string map, string? prefix, string expectedLabel)
    {
        var filters = prefix is null ? "[]" : $$"""[{"filter":"mandatory_prefix","value":"{{prefix}}"}]""";
        var definition = await ParseAsync(
            $$$"""
            [{"mnemonic":"a","opcode":"10","encoding":"{{{encoding}}}","opcode_map":"{{{map}}}","filters":{{{filters}}},
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """);

        var (tableEncoding, tableMap, tablePrefix) = OpcodeTableLayout.TableOf(definition);

        Assert.Equal(expectedLabel, OpcodeTableLayout.TableLabel(tableEncoding, tableMap, tablePrefix));
    }

    [Fact]
    public void IgnorePrefixIsRejectedForVex()
    {
        var definition = Make("a", 0x10, ("mandatory_prefix", "ignore")) with
        {
            Encoding = InstructionEncoding.VEX,
            OpcodeMap = OpcodeMap.M0F
        };

        Assert.Throws<NotSupportedException>(() => OpcodeTableLayout.TableOf(definition));
    }

    [Fact]
    public void PrimaryZeroFRowLeadsToTheZeroFTable()
    {
        var roots = BuildRoots(Make("a", 0xFC) with { OpcodeMap = OpcodeMap.M0F38 });

        var primary = Assert.Single(roots);
        Assert.Equal("PRIMARY", primary.Label);
        Assert.Equal(ViewNodeKind.Table, Find(primary, "0F/0F").Kind);
        Assert.Equal(ViewNodeKind.Table, Find(primary, "0F/0F/38/0F38").Kind);
        Assert.Equal("a", DefinitionAt(primary, "0F/0F/38/0F38/FC").Definition!.Mnemonic);
    }

    [Fact]
    public void VexFormsHangUnderTheC4Switch()
    {
        var roots = BuildRoots(Vex("vaddpd", 0xFC, OpcodeMap.M0F, "66"));

        var primary = Assert.Single(roots);
        Assert.Equal("PRIMARY", primary.Label);
        Assert.Equal(ViewNodeKind.Switch, Find(primary, "C4/switch_table_vex").Kind);
        var table = Assert.Single(Find(primary, "C4/switch_table_vex/66_0f").Children);
        Assert.Equal(ViewNodeKind.Table, table.Kind);
        Assert.Equal("VEX_66_0F", table.Label);
        Assert.Equal("vaddpd", DefinitionAt(table, "FC").Definition!.Mnemonic);

        var reference = Assert.Single(Find(primary, "C5/switch_table_vex/66_0f").Children);
        Assert.Equal(ViewNodeKind.Reference, reference.Kind);
        Assert.Equal("VEX_66_0F", reference.Label);
        Assert.Equal("PRIMARY/C4/switch_table_vex/66_0f", reference.Detail);
        Assert.Empty(reference.Children);
    }

    [Fact]
    public void Rex2RowsReferencePrimaryAndZeroF()
    {
        var primary = Build(
            Make("a", 0x10, ("rex_2", "rex2")),
            Make("b", 0x10) with { OpcodeMap = OpcodeMap.M0F });

        var switchRow = Find(primary, "D5/switch_table_rex2");
        Assert.Equal(["rex2", "rex2_0f"], switchRow.Children.Select(x => x.Label));

        var toPrimary = Assert.Single(Find(switchRow, "rex2").Children);
        Assert.Equal(ViewNodeKind.Reference, toPrimary.Kind);
        Assert.Equal("PRIMARY", toPrimary.Label);
        Assert.Equal("PRIMARY", toPrimary.Detail);

        var toZeroF = Assert.Single(Find(switchRow, "rex2_0f").Children);
        Assert.Equal(ViewNodeKind.Reference, toZeroF.Kind);
        Assert.Equal("0F", toZeroF.Label);
        Assert.Equal("PRIMARY/0F", toZeroF.Detail);
    }

    [Fact]
    public void LegacyFormAtC4StaysInTheDefaultSlot()
    {
        var primary = Build(
            Make("les", 0xC4, ("mode", "!64")),
            Vex("vaddpd", 0xFC, OpcodeMap.M0F, "66"));

        var switchRow = Find(primary, "C4/switch_table_vex");
        Assert.Equal(["default", "66_0f"], switchRow.Children.Select(x => x.Label));
        var mode = Assert.Single(Find(switchRow, "default").Children);
        Assert.Equal("mode_compact", mode.Filter);
        Assert.Equal("les", DefinitionAt(mode, "01 (!64)").Definition!.Mnemonic);
    }

    [Fact]
    public void EveryFormIsCountedOnce()
    {
        var primary = Build(
            Make("a", 0x10, ("rex_2", "rex2")),
            Make("b", 0x10) with { OpcodeMap = OpcodeMap.M0F },
            Make("les", 0xC4, ("mode", "!64")),
            Vex("vaddpd", 0xFC, OpcodeMap.M0F, "66"));

        Assert.Equal(4, primary.FormCount);
    }

    [Fact]
    public async Task RealDatabaseBuildsWithoutConflicts()
    {
        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(LocateDatabase()))
        {
            definitions.Add(definition);
        }

        var builder = new ViewTreeBuilder();
        for (var i = 0; i < definitions.Count; i++)
        {
            builder.Insert(definitions[i], i);
        }

        var roots = builder.Build();

        var primary = Assert.Single(roots);
        Assert.Equal("PRIMARY", primary.Label);
        Assert.Equal(ViewNodeKind.Table, Find(primary, "62/switch_table_emvex/evex_np_map4/EVEX_NP_MAP4").Kind);
        Assert.Equal(ViewNodeKind.Table, Find(primary, "0F/0F/0F/3DNW_NP_0F0F").Kind);
        Assert.Equal(definitions.Count, primary.FormCount);

        var nodes = Descendants(primary).ToList();
        var tables = nodes.Where(x => x.Kind is ViewNodeKind.Table).ToList();
        var repeated = tables.GroupBy(x => x.Label).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
        Assert.True(repeated.Count == 0, $"Tables shown more than once: {string.Join(", ", repeated)}.");

        var conflicts = nodes.Where(x => x.Kind is ViewNodeKind.Conflict).Select(x => x.Path).ToList();
        Assert.True(conflicts.Count == 0,
            $"{conflicts.Count} conflict rows:{Environment.NewLine}{string.Join(Environment.NewLine, conflicts)}");
        Assert.False(primary.HasConflict);

        output.WriteLine($"definitions: {definitions.Count}, nodes: {nodes.Count}, tables: {tables.Count}, " +
            $"references: {nodes.Count(x => x.Kind is ViewNodeKind.Reference)}");
    }

    private static InstructionDefinition Vex(string mnemonic, byte opcode, OpcodeMap map, string prefix)
    {
        return Make(mnemonic, opcode, ("mandatory_prefix", prefix)) with
        {
            Encoding = InstructionEncoding.VEX,
            OpcodeMap = map
        };
    }

    private static async Task<InstructionDefinition> ParseAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(stream).ConfigureAwait(false))
        {
            definitions.Add(definition);
        }

        return Assert.Single(definitions);
    }

    private static IEnumerable<ViewNode> Descendants(ViewNode node)
    {
        yield return node;

        foreach (var descendant in node.Children.SelectMany(Descendants))
        {
            yield return descendant;
        }
    }

    private static string LocateDatabase()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "datafiles")))
            {
                return Path.Combine(directory.FullName, "datafiles", "instructions.json");
            }
        }

        throw new DirectoryNotFoundException($"No 'datafiles' folder above '{AppContext.BaseDirectory}'.");
    }
}

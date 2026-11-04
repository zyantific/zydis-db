using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Corpus.Tests;

public class DecoderTreePathTests
{
    [Fact]
    public async Task Enumerate_InternedElseEntry_YieldsOnePathPerParentSlot()
    {
        // B is the interned else entry of both the root rex_w node and the inner rex_b node, so it is reachable
        // through two different parent slots and must yield one path for each.
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await ParseDefinitionAsync(WithMnemonic("A", """{"rex_w":"1","rex_b":"1"}""")));
        builder.InsertDefinition(await ParseDefinitionAsync(WithMnemonic("B", """{}""")));
        builder.Build();
        builder.InsertOpcodeTableSwitchNodes();

        var paths = DecoderTreePathEnumerator.Enumerate(builder.OpcodeTables).ToList();

        Assert.Equal(3, paths.Count);
        var a = Assert.Single(paths, p => p.Definition.Mnemonic == "A");
        Assert.Equal(2, a.Steps.Count);
        Assert.All(a.Steps, step => Assert.False(step.IsElse));
        var b = paths.Where(p => p.Definition.Mnemonic == "B").OrderBy(p => p.Steps.Count).ToList();
        Assert.Equal(2, b.Count);
        Assert.Equal(1, b[0].Steps.Count);
        Assert.Equal(2, b[1].Steps.Count);
        Assert.All(b, p => Assert.True(p.Steps[^1].IsElse));
        Assert.NotEqual(b[0].PathString, b[1].PathString);
    }

    private static string WithMnemonic(string mnemonic, string filtersJson) =>
        $$$"""{"mnemonic":"{{{mnemonic}}}","opcode":"00","filters":{{{filtersJson}}},"meta_info":{}}""";

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

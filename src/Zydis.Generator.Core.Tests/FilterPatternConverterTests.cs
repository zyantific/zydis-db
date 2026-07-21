using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Tests;

public class FilterPatternConverterTests
{
    [Fact]
    public async Task Read_ArrayForm_PreservesEntryOrder()
    {
        var definition = await ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":[{"filter":"rex_w","value":"1"},{"filter":"modrm_mod","value":"3"}],"meta_info":{}}""");

        Assert.Equal(["rex_w", "modrm_mod"], definition.Pattern!.Select(x => x.Filter));
        Assert.Equal(["1", "3"], definition.Pattern!.Select(x => x.Value));
    }

    [Fact]
    public async Task Read_ArrayForm_DuplicateFilter_Throws()
    {
        await Assert.ThrowsAsync<JsonException>(() => ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":[{"filter":"rex_w","value":"1"},{"filter":"rex_w","value":"0"}],"meta_info":{}}"""));
    }

    [Fact]
    public async Task Read_ArrayForm_UnknownEntryProperty_Throws()
    {
        await Assert.ThrowsAsync<JsonException>(() => ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":[{"filter":"rex_w","value":"1","bogus":"x"}],"meta_info":{}}"""));
    }

    [Fact]
    public async Task Read_ArrayForm_NonStringValue_Throws()
    {
        await Assert.ThrowsAsync<JsonException>(() => ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":[{"filter":"modrm_mod","value":3}],"meta_info":{}}"""));
    }

    [Fact]
    public async Task Read_LegacyObjectForm_PreservesKeyOrderAsEntryOrder()
    {
        var definition = await ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":{"rex_w":"1","modrm_mod":"3"},"meta_info":{}}""");

        Assert.Equal(["rex_w", "modrm_mod"], definition.Pattern!.Select(x => x.Filter));
    }

    [Fact]
    public async Task Read_LegacyObjectForm_LiftsBoolFlagsToProperties()
    {
        var definition = await ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":{"modrm_mod":"3","force_modrm_reg":true},"meta_info":{}}""");

        Assert.True(definition.ForceModrmReg);
        Assert.False(definition.ForceModrmRm);
        Assert.Equal(["modrm_mod"], definition.Pattern!.Select(x => x.Filter));
    }

    [Fact]
    public async Task Read_TopLevelFlagProperty_Binds()
    {
        var definition = await ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":[],"force_modrm_rm":true,"meta_info":{}}""");

        Assert.True(definition.ForceModrmRm);
    }

    [Fact]
    public async Task Write_EmitsSingleLineEntriesAndTopLevelFlags()
    {
        var definition = await ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":{"modrm_mod":"3","rex_w":"1","force_modrm_reg":true},"meta_info":{}}""");

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await DefinitionWriter.WriteAsync(path, [definition]);
            var text = await File.ReadAllTextAsync(path);

            Assert.Contains(
                "\"filters\": [\n      { \"filter\": \"modrm_mod\", \"value\": \"3\" },\n      { \"filter\": \"rex_w\", \"value\": \"1\" }\n    ]",
                text, StringComparison.Ordinal);
            Assert.Contains("\"force_modrm_reg\": true", text, StringComparison.Ordinal);
            Assert.DoesNotContain("force_modrm_reg\": \"", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteThenRead_RoundTripsLosslessly()
    {
        var definition = await ParseAsync(
            """{"mnemonic":"test","opcode":"00","filters":{"modrm_mod":"3","rex_w":"1","force_modrm_rm":true},"meta_info":{}}""");

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await DefinitionWriter.WriteAsync(path, [definition]);

            await foreach (var reread in DefinitionReader.ReadAsync<InstructionDefinition>(path))
            {
                Assert.Equal(definition.Pattern!.ToList(), reread.Pattern!.ToList());
                Assert.Equal(definition.ForceModrmRm, reread.ForceModrmRm);
                return;
            }

            throw new InvalidOperationException("No definition was read back.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<InstructionDefinition> ParseAsync(string definitionJson)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, $"[{definitionJson}]");

            await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(path))
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

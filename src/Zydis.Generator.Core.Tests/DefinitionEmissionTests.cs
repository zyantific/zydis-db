using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Definitions.Emitters;

namespace Zydis.Generator.Core.Tests;

public class DefinitionEmissionTests
{
    [Theory]
    [InlineData(null, 0)] // MOVBE-like: no mandatory_prefix filter at all.
    [InlineData("none", 0)]
    [InlineData("ignore", 0)]
    [InlineData("!f3", 0)] // Negated filters decode for every other prefix and consume none of their own.
    [InlineData("66", 1)]
    [InlineData("f3", 2)]
    [InlineData("f2", 3)]
    public void GetMandatoryPrefixEncoding_MapsFilterValueToConsumedPrefixCode(string? mandatoryPrefix, int expected)
    {
        var filters = mandatoryPrefix is null ? null : new Dictionary<string, string> { ["mandatory_prefix"] = mandatoryPrefix };
        var definition = CreateDefinition(filters);

        Assert.Equal(expected, DefinitionEmitter.GetMandatoryPrefixEncoding(definition));
    }

    [Fact]
    public void HasApxScc_DefinitionWithEvexSccFilter_ReturnsTrue()
    {
        var definition = CreateDefinition(new Dictionary<string, string> { ["evex_scc"] = "2" });

        Assert.True(DefinitionEmitter.HasApxScc(definition));
    }

    [Fact]
    public void HasApxScc_DefinitionWithoutEvexSccFilter_ReturnsFalse()
    {
        var definition = CreateDefinition(null);

        Assert.False(DefinitionEmitter.HasApxScc(definition));
    }

    [Fact]
    public void HasApxNfCheck_DefinitionWithEvexNfFilter_ReturnsTrue()
    {
        // HasNf is deliberately false to prove the derivation relies on the filter, not the evex "nf" property.
        var definition = CreateDefinition(new Dictionary<string, string> { ["evex_nf"] = "0" }) with
        {
            Evex = new InstructionEvexInfo { HasNf = false }
        };

        Assert.True(DefinitionEmitter.HasApxNfCheck(definition));
    }

    [Fact]
    public void HasApxNfCheck_DefinitionWithoutEvexNfFilter_ReturnsFalse()
    {
        var definition = CreateDefinition(null);

        Assert.False(DefinitionEmitter.HasApxNfCheck(definition));
    }

    private static InstructionDefinition CreateDefinition(IReadOnlyDictionary<string, string>? filters)
    {
        return new InstructionDefinition
        {
            Mnemonic = "test",
            Opcode = 0x00,
            MetaInfo = new InstructionMetaInfo(),
            Pattern = filters?.ToDictionary(x => x.Key, x => ParseJsonStringValue(x.Value))
        };
    }

    // Avoids JsonSerializer.SerializeToElement, which requires reflection-based metadata that this
    // solution's source-generated JSON context does not provide.
    private static JsonElement ParseJsonStringValue(string value)
    {
        using var document = JsonDocument.Parse($"\"{value}\"");
        return document.RootElement.Clone();
    }
}

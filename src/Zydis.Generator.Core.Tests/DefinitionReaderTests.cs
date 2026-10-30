using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Tests;

public class DefinitionReaderTests
{
    [Fact]
    public async Task StreamOverload_LiftsLegacyForceModrmRegFlag()
    {
        const string json = """
            [{"mnemonic":"x","opcode":"10","filters":{"modrm_mod":"3","force_modrm_reg":true},
              "meta_info":{"category":"BINARY","extension":"BASE","isa_set":"I86"}}]
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var definitions = new List<InstructionDefinition>();
        await foreach (var item in DefinitionReader.ReadAsync<InstructionDefinition>(stream))
        {
            definitions.Add(item);
        }
        var definition = Assert.Single(definitions);
        Assert.True(definition.ForceModrmReg);
        Assert.Equal(["modrm_mod"], definition.Pattern!.Select(x => x.Filter));
    }
}

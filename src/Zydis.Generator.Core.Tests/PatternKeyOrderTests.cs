using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.Tests;

public class PatternKeyOrderTests
{
    [Fact]
    public async Task Changed_UpdatedPatternHasSameKeyOrder_ReturnsFalse()
    {
        // Mirrors ReorderPattern's real shape: a fresh Dictionary instance whose key order happens to match the
        // original's, which is exactly the case record equality/reference equality can't distinguish from a change.
        var original = await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"3","rex_w":"1"}""");
        var updated = original with { Pattern = new Dictionary<string, JsonElement>(original.Pattern!) };

        Assert.False(PatternKeyOrder.Changed(original, updated));
    }

    [Fact]
    public async Task Changed_UpdatedPatternHasDifferentKeyOrder_ReturnsTrue()
    {
        var original = await TestHelpers.ParseDefinitionAsync("bsf", """{"rex_w":"1","modrm_mod":"3"}""");
        var updated = await TestHelpers.ParseDefinitionAsync("bsf", """{"modrm_mod":"3","rex_w":"1"}""");

        Assert.True(PatternKeyOrder.Changed(original, updated));
    }

    [Fact]
    public async Task Changed_BothPatternsNull_ReturnsFalse()
    {
        var original = await TestHelpers.ParseDefinitionAsync("nop", "{}") with { Pattern = null };
        var updated = original;

        Assert.False(PatternKeyOrder.Changed(original, updated));
    }
}

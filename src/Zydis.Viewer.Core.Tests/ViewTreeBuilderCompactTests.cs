using System.Linq;

using Xunit;

using static Zydis.Viewer.Core.Tests.TestDefinitions;

namespace Zydis.Viewer.Core.Tests;

public class ViewTreeBuilderCompactTests
{
    [Fact]
    public void ModeWithOnlySlot64Compacts()
    {
        var table = Build(
            Make("a", 0x10, ("mode", "64")),
            Make("b", 0x10, ("mode", "!64")));

        var compact = Assert.Single(Find(table, "10").Children);
        Assert.Equal("mode_compact", compact.Label);
        Assert.Equal("mode_compact", compact.Filter);
        Assert.Equal(["00 (64)", "01 (!64)"], compact.Children.Select(x => x.Label));
        Assert.Equal("a", DefinitionAt(compact, "00 (64)").Definition!.Mnemonic);
        Assert.Equal("b", DefinitionAt(compact, "01 (!64)").Definition!.Mnemonic);
    }

    [Fact]
    public void ModeWithSlot32StaysUncompacted()
    {
        var table = Build(
            Make("a", 0x10, ("mode", "64")),
            Make("b", 0x10, ("mode", "32")));

        var mode = Assert.Single(Find(table, "10").Children);
        Assert.Equal("mode", mode.Label);
        Assert.Equal(["01 (32)", "02 (64)"], mode.Children.Select(x => x.Label));
    }

    [Fact]
    public void ModeWithOnlyNegated16StaysUncompacted()
    {
        var table = Build(Make("a", 0x10, ("mode", "!16")));

        var mode = Assert.Single(Find(table, "10").Children);
        Assert.Equal("mode", mode.Label);
        Assert.Equal("a", DefinitionAt(mode, "!00 (not 16)").Definition!.Mnemonic);
    }

    [Fact]
    public void ModrmModWithOnlySlot3AndElseCompacts()
    {
        var table = Build(
            Make("a", 0x10, ("modrm_mod", "3")),
            Make("b", 0x10, ("rex_w", "0")));

        var compact = Assert.Single(Find(table, "10").Children);
        Assert.Equal("modrm_mod_compact", compact.Filter);
        Assert.Equal("a", DefinitionAt(compact, "00 (reg)").Definition!.Mnemonic);
        var rexW = Assert.Single(Find(compact, "01 (mem)").Children);
        Assert.Equal("rex_w", rexW.Filter);
        Assert.Equal("b", DefinitionAt(rexW, "00 (0)").Definition!.Mnemonic);
    }
}

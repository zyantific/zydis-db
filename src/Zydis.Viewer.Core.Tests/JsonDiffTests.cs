using System.Text.Json;

using Xunit;

using Zydis.Viewer.Core.Diff;

namespace Zydis.Viewer.Core.Tests;

public class JsonDiffTests
{
    [Fact]
    public void ChangedPathsReportsNestedArrayElement()
    {
        using var a = JsonDocument.Parse(
            """
            {"operands":[{"operand_type":"gpr8","action":"read"},{"operand_type":"gpr8","action":"read"}]}
            """);
        using var b = JsonDocument.Parse(
            """
            {"operands":[{"operand_type":"gpr8","action":"read"},{"operand_type":"gpr8","action":"write"},
                         {"operand_type":"imm"}]}
            """);

        Assert.Equal(["operands[1].action", "operands[2]"], JsonDiff.ChangedPaths(a.RootElement, b.RootElement));
    }

    [Fact]
    public void ChangedPathsReportsKeyPresentOnOneSide()
    {
        using var a = JsonDocument.Parse(
            """
            {"cpl":3,"evex":{"tuple_type":"fv"}}
            """);
        using var b = JsonDocument.Parse(
            """
            {"evex":{"tuple_type":"fv","mask_mode":"merge"},"vex":{"vector_length":"128"}}
            """);

        Assert.Equal(["cpl", "evex.mask_mode", "vex"], JsonDiff.ChangedPaths(a.RootElement, b.RootElement));
    }
}

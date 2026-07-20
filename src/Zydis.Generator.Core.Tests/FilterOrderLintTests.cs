using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree.Builder;

namespace Zydis.Generator.Core.Tests;

public class FilterOrderLintTests
{
    [Fact]
    public async Task Run_RecordedOrderMatchesCurrent_NoFindings()
    {
        var builder = new VariablePositionTreeBuilder();
        builder.InsertDefinition(await TestHelpers.ParseDefinitionAsync("a", """{"modrm_mod":"3"}"""));
        builder.InsertDefinition(await TestHelpers.ParseDefinitionAsync("a", """{"modrm_mod":"!3"}"""));
        var groups = builder.BuildGroups().ToList(); // single filter each -> no order ambiguity, trivially matches

        var findings = FilterOrderLint.Run(groups);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Run_RecordedOrderDiffersFromCurrent_ReportsFinding()
    {
        // TestHelpers.BuildKnownSuboptimalGroupAsync() (shared with TreeConstructorTests/FilterOrderExtractorTests)
        // is a group where the true optimum splits on `mode` first, then `rex_w` - but this fixture's own filters
        // JSON declares `rex_w` before `mode`, so FilterOrderLint must catch that the checked-in order disagrees
        // with what FilterOrderExtractor derives from the actual constructed tree.
        var group = await TestHelpers.BuildKnownSuboptimalGroupAsync();

        var findings = FilterOrderLint.Run([group]);

        var finding = Assert.Single(findings);
        Assert.NotEqual(finding.RecordedOrder, finding.CurrentOrder);
        Assert.Equal(new FilterKey("mode"), finding.CurrentOrder[0]); // the true optimum's first test
    }
}

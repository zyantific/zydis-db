using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree.Builder;

namespace Zydis.Generator.Core.Tests;

public class GroupValidatorTests
{
    [Fact]
    public async Task Validate_IncomparableOverlap_ReportsBothMnemonics()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"rex_w":"0"}"""),
            await MemberAsync("BBB", """{"mode":"64"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Single(errors);
        Assert.Contains("AAA", errors[0]);
        Assert.Contains("BBB", errors[0]);
    }

    [Fact]
    public async Task Validate_NestedPair_NoErrors()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"rex_w":"1","mode":"64"}"""),
            await MemberAsync("BBB", """{"rex_w":"1"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Validate_DisjointPair_NoErrors()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"rex_w":"0"}"""),
            await MemberAsync("BBB", """{"rex_w":"1"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Validate_EqualRegions_Reported()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"mode":"64"}"""),
            await MemberAsync("BBB", """{"mode":"64"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Single(errors);
        Assert.Contains("AAA", errors[0]);
        Assert.Contains("BBB", errors[0]);
    }

    [Fact]
    public async Task Validate_EqualRegions_ErrorMessageInvariantToMemberOrder()
    {
        var a = await MemberAsync("AAA", """{"mode":"64"}""");
        var b = await MemberAsync("BBB", """{"mode":"64"}""");

        var errorsAB = GroupValidator.Validate("group", new[] { a, b });
        var errorsBA = GroupValidator.Validate("group", new[] { b, a });

        Assert.Equal(errorsAB, errorsBA);
    }

    [Fact]
    public async Task Validate_Mandatory66WithOperandSize_Reported()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"mandatory_prefix":"66","operand_size":"16"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Single(errors);
        Assert.Contains("AAA", errors[0]);
    }

    [Fact]
    public async Task Validate_MandatoryPrefixOtherThan66WithOperandSize_NotReported()
    {
        // The decoder can express other mandatory prefixes alongside an operand_size constraint;
        // only the 66 case moves prefix consumption to definition time.
        var members = new[]
        {
            await MemberAsync("AAA", """{"mandatory_prefix":"f3","operand_size":"16"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Validate_NegatedMandatory66WithOperandSize_NotReported()
    {
        // "!66" means "not 66", which does not collide with an operand_size constraint the way "66" does.
        var members = new[]
        {
            await MemberAsync("AAA", """{"mandatory_prefix":"!66","operand_size":"16"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task Validate_GroupNameAppearsInErrors()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"mode":"64"}"""),
            await MemberAsync("BBB", """{"mode":"64"}""")
        };

        var errors = GroupValidator.Validate("my_group[0x12]", members);

        Assert.Single(errors);
        Assert.Contains("my_group[0x12]", errors[0]);
    }

    [Fact]
    public async Task Validate_MultipleErrors_ReturnedSorted()
    {
        var members = new[]
        {
            await MemberAsync("AAA", """{"mode":"64"}"""),
            await MemberAsync("BBB", """{"mode":"64"}"""),
            await MemberAsync("CCC", """{"rex_w":"0"}"""),
            await MemberAsync("DDD", """{"mode":"32"}""")
        };

        var errors = GroupValidator.Validate("group", members);

        Assert.True(errors.Count >= 2);

        var sorted = new List<string>(errors);
        sorted.Sort(StringComparer.Ordinal);
        Assert.Equal(sorted, errors);
    }

    private static Task<GroupMember> MemberAsync(string mnemonic, string filtersJson) =>
        TestHelpers.MemberAsync(mnemonic, filtersJson);
}

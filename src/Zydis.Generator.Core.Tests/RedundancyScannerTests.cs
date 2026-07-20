using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.DecoderTree.Builder;

namespace Zydis.Generator.Core.Tests;

public class RedundancyScannerTests
{
    [Fact]
    public async Task FindRedundant_SupersetSiblingCoversNarrowerDuplicate_FlagsNarrowerMember()
    {
        var broad = await TestHelpers.MemberAsync("bsf", """{"modrm_mod":"3"}""");
        var narrow = await TestHelpers.MemberAsync("bsf", """{"modrm_mod":"3","mandatory_prefix":"none"}""");
        var unrelated = await TestHelpers.MemberAsync("bsf", """{"modrm_mod":"!3"}""");

        var redundant = RedundancyScanner.FindRedundant([broad, narrow, unrelated]);

        var flagged = Assert.Single(redundant);
        Assert.Same(narrow.Definition, flagged);
    }

    [Fact]
    public async Task FindRedundant_SupersetSiblingWithDifferentOperands_DoesNotFlag()
    {
        // Same region relationship as above, but the narrower definition's operands differ from the
        // broader one's, so it is a real, distinct instruction form and must not be flagged.
        var broad = await TestHelpers.MemberAsync("bsf", """{"modrm_mod":"3"}""");
        var narrowDifferentOperands = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3","mandatory_prefix":"none"}""", operandCountOverride: 3);

        var redundant = RedundancyScanner.FindRedundant([broad, narrowDifferentOperands]);

        Assert.Empty(redundant);
    }

    [Fact]
    public async Task FindRedundant_SupersetSiblingWithEqualButDistinctOperandInstances_FlagsNarrowerMember()
    {
        // Operands is a reference-typed collection, so InstructionDefinition's record-synthesized `==` compares it
        // by reference: two independently parsed definitions with content-identical operand lists are never
        // `==`-equal even when every operand in them compares equal, which is exactly the case for real duplicate
        // rows in datafiles/instructions.json (each row is parsed from its own JSON object). Verifies operands are
        // compared by content, not list identity.
        var broad = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3"}""", operandCountOverride: 2);
        var narrow = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3","mandatory_prefix":"none"}""", operandCountOverride: 2);

        var redundant = RedundancyScanner.FindRedundant([broad, narrow]);

        var flagged = Assert.Single(redundant);
        Assert.Same(narrow.Definition, flagged);
    }

    [Fact]
    public async Task FindRedundant_SupersetSiblingWithEqualButDistinctAffectedFlagsInstances_FlagsNarrowerMember()
    {
        // AffectedFlags.Flags is also a reference-typed dictionary nested inside a record, so the same identity-
        // vs-content gap as Operands applies one level deeper: InstructionFlags' record-synthesized `==` compares
        // it by reference too. This mirrors the real bsf/bsr duplicates, whose affected_flags are independently
        // parsed but content-identical.
        const string affectedFlags = """{"access":"must_write","cf":"u","pf":"u","af":"u","zf":"m","sf":"u","of":"u"}""";
        var broad = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3"}""", affectedFlagsJson: affectedFlags);
        var narrow = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3","mandatory_prefix":"none"}""", affectedFlagsJson: affectedFlags);

        var redundant = RedundancyScanner.FindRedundant([broad, narrow]);

        var flagged = Assert.Single(redundant);
        Assert.Same(narrow.Definition, flagged);
    }

    [Fact]
    public async Task FindRedundant_SupersetSiblingWithDifferentComment_StillFlags()
    {
        // Comment carries free-form provenance text copied from the original data import; it has no bearing on
        // decoding and legitimately differs between two rows describing the same outcome (this is exactly the
        // case for the real bsf/bsr duplicates in datafiles/instructions.json), so it must not block the match.
        var broad = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3"}""", comment: "replaced in the HSW builds");
        var narrow = await TestHelpers.MemberAsync(
            "bsf", """{"modrm_mod":"3","mandatory_prefix":"none"}""", comment: "AMD reused 0FBC for TZCNT");

        var redundant = RedundancyScanner.FindRedundant([broad, narrow]);

        var flagged = Assert.Single(redundant);
        Assert.Same(narrow.Definition, flagged);
    }

    [Fact]
    public async Task FindRedundant_SupersetSiblingWithDifferentIsaSet_DoesNotFlag()
    {
        // Unlike Comment, MetaInfo.IsaSet is [Emittable] and reaches the generated table, so it is observable
        // through the public API. Two rows that disagree on it are not interchangeable, even though their
        // regions nest and everything else about them matches (mirrors nop/0x1E's `ignore`/FAT_NOP vs.
        // `none`/PPRO pair in datafiles/instructions.json, where the isa_set difference is real and must be
        // preserved).
        var broad = await TestHelpers.MemberAsync(
            "nop", """{"modrm_mod":"3"}""", metaInfoJson: """{"isa_set":"FAT_NOP"}""");
        var narrow = await TestHelpers.MemberAsync(
            "nop", """{"modrm_mod":"3","mandatory_prefix":"none"}""", metaInfoJson: """{"isa_set":"PPRO"}""");

        var redundant = RedundancyScanner.FindRedundant([broad, narrow]);

        Assert.Empty(redundant);
    }
}

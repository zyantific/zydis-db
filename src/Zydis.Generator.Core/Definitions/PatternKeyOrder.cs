using System.Linq;

namespace Zydis.Generator.Core.Definitions;

/// <summary>
/// Compares the filter order between two revisions of the same definition - e.g. before and after a
/// migrate-order rewrite. <see cref="InstructionDefinition"/>'s record-synthesized <c>==</c> can't answer this:
/// <c>with</c> always allocates a fresh <c>Pattern</c> list, so a rewrite whose result happens to already
/// match the original order is still a distinct instance and needs the entry sequence compared directly.
/// </summary>
internal static class PatternKeyOrder
{
    public static bool Changed(InstructionDefinition original, InstructionDefinition updated)
    {
        var originalKeys = (original.Pattern ?? []).Select(x => x.Filter);
        var updatedKeys = (updated.Pattern ?? []).Select(x => x.Filter);

        return !originalKeys.SequenceEqual(updatedKeys);
    }
}

using System.Linq;

namespace Zydis.Generator.Core.Definitions;

/// <summary>
/// Compares the "filters" key order between two revisions of the same definition - e.g. before and after a
/// migrate-order rewrite. <see cref="InstructionDefinition"/>'s record-synthesized <c>==</c> can't answer this:
/// <c>with</c> always allocates a fresh <c>Pattern</c> dictionary, so a rewrite whose result happens to already
/// match the original key order is still a distinct instance and needs the key sequence compared directly.
/// </summary>
internal static class PatternKeyOrder
{
    public static bool Changed(InstructionDefinition original, InstructionDefinition updated)
    {
        var originalKeys = original.Pattern?.Keys ?? [];
        var updatedKeys = updated.Pattern?.Keys ?? [];

        return !originalKeys.SequenceEqual(updatedKeys);
    }
}

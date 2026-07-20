using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Tests;

/// <summary>
/// Shared fixture helpers for building <see cref="GroupMember"/>s from JSON filter snippets, round-tripped through
/// the real <see cref="DefinitionReader"/> so tests exercise the same parsing path as production data.
/// </summary>
internal static class TestHelpers
{
    public static async Task<GroupMember> MemberAsync(
        string mnemonic, string filtersJson, int? operandCountOverride = null, string? comment = null,
        string metaInfoJson = "{}", string? affectedFlagsJson = null)
    {
        var definition = await ParseDefinitionAsync(
            WithMnemonic(mnemonic, filtersJson, operandCountOverride, comment, metaInfoJson, affectedFlagsJson)).ConfigureAwait(false);

        return new GroupMember(definition, ConstraintSet.Parse(definition));
    }

    private static string WithMnemonic(
        string mnemonic, string filtersJson, int? operandCountOverride, string? comment, string metaInfoJson,
        string? affectedFlagsJson)
    {
        var operandsField = operandCountOverride is { } count
            ? "," + "\"operands\":[" + string.Join(',', Enumerable.Repeat("""{"operand_type":"gpr16_32_64"}""", count)) + "]"
            : string.Empty;

        var commentField = comment is null ? string.Empty : "," + "\"comment\":\"" + comment + "\"";

        var affectedFlagsField = affectedFlagsJson is null ? string.Empty : "," + "\"affected_flags\":" + affectedFlagsJson;

        return $$$"""{"mnemonic":"{{{mnemonic}}}","opcode":"00","filters":{{{filtersJson}}}{{{operandsField}}}{{{commentField}}}{{{affectedFlagsField}}},"meta_info":{{{metaInfoJson}}}}""";
    }

    private static async Task<InstructionDefinition> ParseDefinitionAsync(string definitionJson)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, $"[{definitionJson}]").ConfigureAwait(false);

            await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(path).ConfigureAwait(false))
            {
                return definition;
            }

            throw new InvalidOperationException("No definition was parsed from the test fixture.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

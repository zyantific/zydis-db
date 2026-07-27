using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Tests;

public class DefinitionWriterTests
{
    [Fact]
    public async Task WriteAsync_ThenRead_RoundTripsToEquivalentDefinitions()
    {
        var original = await TestHelpers.ParseDefinitionAsync("bsf", """{"rex_w":"1","modrm_mod":"3"}""");
        var path = TempPath();

        try
        {
            await DefinitionWriter.WriteAsync(path, [original]);
            var roundTripped = await ReadSingleAsync(path);

            // IReadOnlyList<FilterEntry> carries reference, not value, equality through the record-generated
            // Equals, so Pattern is compared by content separately from the rest of the definition.
            Assert.Equal(original with { Pattern = null }, roundTripped with { Pattern = null });
            Assert.Equal(original.Pattern, roundTripped.Pattern);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteAsync_PreservesGivenPatternOrder()
    {
        var definition = await TestHelpers.ParseDefinitionAsync("bsf", """{"rex_w":"1","modrm_mod":"3"}""");
        var reordered = definition with
        {
            Pattern = new List<FilterEntry>
            {
                new("modrm_mod", definition.GetFilterValue("modrm_mod")!),
                new("rex_w", definition.GetFilterValue("rex_w")!),
            }
        };
        var path = TempPath();

        try
        {
            await DefinitionWriter.WriteAsync(path, [reordered]);
            var writtenJson = await File.ReadAllTextAsync(path);

            Assert.True(writtenJson.IndexOf("modrm_mod", StringComparison.Ordinal) <
                        writtenJson.IndexOf("rex_w", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteAsync_EmptyPattern_RoundTrips()
    {
        var original = await TestHelpers.ParseDefinitionAsync("insb", "{}");
        var path = TempPath();

        try
        {
            await DefinitionWriter.WriteAsync(path, [original]);
            var writtenJson = await File.ReadAllTextAsync(path);
            var roundTripped = await ReadSingleAsync(path);

            // An empty filter set collapses to the compact "[]" rather than being spread across lines.
            Assert.Contains("\"filters\": []", writtenJson, StringComparison.Ordinal);
            Assert.Empty(roundTripped.Pattern!);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteAsync_AffectedFlagsAndPrefixFlags_RoundTrip()
    {
        // 'access' is deliberately non-default (must_write) and there is more than one prefix flag, so this
        // exercises both InstructionFlagsConverter.Write and StringFlagsConverter<T>.Write, neither of which
        // had a working Write implementation before DefinitionWriter needed one.
        var original = await TestHelpers.ParseDefinitionAsync(
            "adc", """{"rex_w":"1"}""",
            affectedFlagsJson: """{"access":"must_write","cf":"m","of":"u"}""");
        var withPrefixFlags = original with { PrefixFlags = PrefixFlags.AcceptsLOCK | PrefixFlags.AcceptsXRELEASE };
        var path = TempPath();

        try
        {
            await DefinitionWriter.WriteAsync(path, [withPrefixFlags]);
            var roundTripped = await ReadSingleAsync(path);

            Assert.Equal(withPrefixFlags.PrefixFlags, roundTripped.PrefixFlags);
            Assert.Equal(withPrefixFlags.AffectedFlags!.Access, roundTripped.AffectedFlags!.Access);
            Assert.Equal(
                withPrefixFlags.AffectedFlags.Flags.OrderBy(kv => kv.Key),
                roundTripped.AffectedFlags.Flags.OrderBy(kv => kv.Key));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<InstructionDefinition> ReadSingleAsync(string path)
    {
        await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(path).ConfigureAwait(false))
        {
            return definition;
        }

        throw new InvalidOperationException("No definition was read back from the written file.");
    }

    private static string TempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
    }
}

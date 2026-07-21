using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.Serialization;

/// <summary>
/// Reads and writes <see cref="InstructionDefinition.Pattern"/> ("filters"). The canonical form is an ordered
/// array of <c>{ "filter": ..., "value": ... }</c> entries; the legacy object form (member order carrying the
/// filter order) is still accepted on read so pre-migration data files and branches keep parsing. Writes always
/// emit the array form, one single-line entry per element.
/// </summary>
internal sealed class FilterPatternConverter :
    JsonConverter<IReadOnlyList<FilterEntry>>
{
    // Bool-valued members of the legacy object form; every other member must be a string-valued filter.
    // NormalizeLegacyPatternFlags lifts the resulting entries onto InstructionDefinition's flag properties.
    private static readonly string[] LegacyFlagKeys = ["force_modrm_reg", "force_modrm_rm"];

    public override IReadOnlyList<FilterEntry>? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => ReadArrayForm(ref reader),
            JsonTokenType.StartObject => ReadLegacyObjectForm(ref reader, options),
            _ => throw new JsonException("\"filters\" must be an array of filter entries (or a legacy object).")
        };
    }

    private static List<FilterEntry> ReadArrayForm(ref Utf8JsonReader reader)
    {
        var entries = new List<FilterEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
            {
                throw new JsonException("Each \"filters\" element must be an object.");
            }

            string? filter = null;
            string? value = null;

            while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "filter":
                        filter = reader.GetString();
                        break;
                    case "value":
                        value = reader.GetString();
                        break;
                    default:
                        throw new JsonException(
                            $"Unexpected property '{propertyName}' in filter entry; only \"filter\" and \"value\" are allowed.");
                }
            }

            if (filter is null || value is null)
            {
                throw new JsonException("A filter entry must have both \"filter\" and \"value\".");
            }

            if (!seen.Add(filter))
            {
                throw new JsonException($"Duplicate filter '{filter}'.");
            }

            entries.Add(new FilterEntry(filter, value));
        }

        return entries;
    }

    private static List<FilterEntry> ReadLegacyObjectForm(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var members = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options)!;

        var entries = new List<FilterEntry>(members.Count);
        foreach (var (key, value) in members)
        {
            entries.Add(value.ValueKind switch
            {
                JsonValueKind.String => new FilterEntry(key, value.GetString()!),
                JsonValueKind.True or JsonValueKind.False when Array.IndexOf(LegacyFlagKeys, key) >= 0 =>
                    new FilterEntry(key, value.ValueKind is JsonValueKind.True ? "true" : "false"),
                _ => throw new JsonException($"Filter '{key}' must have a string value.")
            });
        }

        return entries;
    }

    public override void Write(
        Utf8JsonWriter writer, IReadOnlyList<FilterEntry> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        // WriteRawValue inserts the comma between array elements automatically but not the surrounding
        // indentation, so the newline + indent has to be prepended by hand to keep one entry per line
        // (matching the density of the legacy object form).
        var indent = writer.Options.Indented
            ? writer.Options.NewLine + new string(writer.Options.IndentCharacter, writer.CurrentDepth * writer.Options.IndentSize)
            : string.Empty;

        foreach (var entry in value)
        {
            // JsonEncodedText escapes the raw string content without going through
            // JsonSerializer.Serialize<string>(), which needs reflection-based metadata this AOT-compatible
            // project has disabled. A default JsonSerializer.Serialize(record) call would also spread each
            // entry over four lines instead of the single line built here.
            var filter = JsonEncodedText.Encode(entry.Filter, options.Encoder);
            var filterValue = JsonEncodedText.Encode(entry.Value, options.Encoder);
            writer.WriteRawValue($$"""{{indent}}{ "filter": "{{filter}}", "value": "{{filterValue}}" }""", skipInputValidation: true);
        }

        writer.WriteEndArray();
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Serialization;

/// <summary>
/// Reads and writes <see cref="Definitions.InstructionDefinition.Pattern"/> ("filters"), preserving the key order it
/// is given on write - the migration tool depends on this to record its computed filter-test order.
/// </summary>
internal sealed class FilterPatternConverter :
    JsonConverter<IReadOnlyDictionary<string, JsonElement>>
{
    public override IReadOnlyDictionary<string, JsonElement>? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options);
    }

    public override void Write(
        Utf8JsonWriter writer, IReadOnlyDictionary<string, JsonElement> value, JsonSerializerOptions options)
    {
        // An empty object is still formatted across two lines rather than the compact "{}" the default
        // dictionary writer would otherwise produce, matching the reference file's convention for a
        // definition with no filters at all (e.g. "insb").
        if (value.Count is 0 && writer.Options.Indented)
        {
            var depth = writer.CurrentDepth;
            var indent = new string(writer.Options.IndentCharacter, depth * writer.Options.IndentSize);
            writer.WriteRawValue($"{{{writer.Options.NewLine}{indent}}}", skipInputValidation: true);
            return;
        }

        writer.WriteStartObject();
        foreach (var (key, element) in value)
        {
            writer.WritePropertyName(key);
            element.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

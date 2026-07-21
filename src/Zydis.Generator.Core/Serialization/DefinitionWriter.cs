using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.Serialization;

/// <summary>
/// Writes <see cref="InstructionDefinition"/>s back to the same JSON array format <see cref="DefinitionReader"/>
/// consumes, reusing its exact serializer configuration so a write-then-read round trip is lossless.
/// </summary>
internal static class DefinitionWriter
{
    public static async Task WriteAsync(
        string path, IReadOnlyList<InstructionDefinition> definitions, CancellationToken ct = default)
    {
        await using var stream = File.Create(path);

        // A caller-supplied Utf8JsonWriter does not inherit JsonSerializerOptions.Encoder; it must be
        // repeated here to keep escaping consistent with DefinitionReader.Options.
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = DefinitionReader.Options.Encoder
        });

        writer.WriteStartArray();
        foreach (var definition in definitions)
        {
            JsonSerializer.Serialize(writer, definition, DefinitionReader.Options);
        }
        writer.WriteEndArray();

        await writer.FlushAsync(ct);

        // The reference instructions.json ends with a trailing newline after the closing ']'.
        await stream.WriteAsync("\n"u8.ToArray(), ct);
    }
}

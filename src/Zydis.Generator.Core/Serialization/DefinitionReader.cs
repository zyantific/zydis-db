using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Extensions;

namespace Zydis.Generator.Core.Serialization;

public static partial class DefinitionReader
{
    /// <summary>
    /// The exact reader configuration, shared with <see cref="DefinitionWriter"/> so writes agree with reads on
    /// property naming, converters and unmapped-member handling.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = SerializerContext.Default,
        PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,

        // The reference file leaves characters like '>' unescaped in comment text; the default encoder would
        // HTML-escape them (e.g. to ">"), producing a spurious diff on every write.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Reads the JSON array of <typeparamref name="T"/> in <paramref name="filename"/>, with the element handling of
    /// <see cref="ReadAsync{T}(Stream, CancellationToken)"/>.
    /// </summary>
    public static async IAsyncEnumerable<T> ReadAsync<T>(string filename, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);

        await using var _ = File.OpenRead(filename)
            .AsAsyncDisposable(out var fs)
            .ConfigureAwait(false);

        await foreach (var item in ReadAsync<T>(fs, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Reads a JSON array of <typeparamref name="T"/> from <paramref name="stream"/> and throws
    /// <see cref="DataException"/> on a <c>null</c> element. Legacy <c>force_modrm_*</c> flag members in the
    /// object-form filters of an <see cref="InstructionDefinition"/> are lifted onto its flag properties. The stream
    /// stays open for the caller to dispose.
    /// </summary>
    public static async IAsyncEnumerable<T> ReadAsync<T>(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var items = JsonSerializer
            .DeserializeAsyncEnumerable<T>(stream, Options, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var item in items)
        {
            if (item is null)
            {
                throw new DataException("Element must not be 'null'.");
            }

            if (item is InstructionDefinition definition)
            {
                // Transitional: legacy object-form "filters" may still carry force_modrm_* flag members;
                // lift them here so every consumer sees pure filter lists and real flag properties.
                yield return (T)(object)definition.NormalizeLegacyPatternFlags();
                continue;
            }

            yield return item;
        }
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(InstructionDefinition))]
    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    [JsonSerializable(typeof(InstructionFlagsAccess))]
    [JsonSerializable(typeof(InstructionFlag))]
    [JsonSerializable(typeof(InstructionFlagOperation))]
    [JsonSerializable(typeof(FormatterStringDefinition))]
    private sealed partial class SerializerContext :
        JsonSerializerContext
    {
    }
}

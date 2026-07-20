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

    public static async IAsyncEnumerable<T> ReadAsync<T>(string filename, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);

        await using var _ = File.OpenRead(filename)
            .AsAsyncDisposable(out var fs)
            .ConfigureAwait(false);

        var items = JsonSerializer
            .DeserializeAsyncEnumerable<T>(fs, Options, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var item in items)
        {
            if (item is null)
            {
                throw new DataException("Element must not be 'null'.");
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

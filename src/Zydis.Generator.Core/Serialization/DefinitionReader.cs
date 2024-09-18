using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Extensions;

namespace Zydis.Generator.Core.Serialization;

public static partial class DefinitionReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = SerializerContext.Default,
        PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static async IAsyncEnumerable<InstructionDefinition> ReadAsync(string filename, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);

        await using var _ = File.OpenRead(filename)
            .AsAsyncDisposable(out var fs)
            .ConfigureAwait(false);

        var items = JsonSerializer
            .DeserializeAsyncEnumerable<InstructionDefinition>(fs, SerializerOptions, cancellationToken)
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
    [JsonSerializable(typeof(InstructionFlagsAccess))]
    [JsonSerializable(typeof(InstructionFlag))]
    [JsonSerializable(typeof(InstructionFlagOperation))]
    private sealed partial class SerializerContext :
        JsonSerializerContext
    {
    }
}

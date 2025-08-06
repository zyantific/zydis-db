using System.Text.Json.Serialization;

using Zydis.Generator.SourceGenerator.Helpers;

namespace Zydis.Generator.Enums.SourceGenerator;

internal sealed record SharedEnumDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("members")]
    public required ImmutableEquatableArray<SharedEnumMember> Members { get; init; }

    [JsonPropertyName("is_flags_enum")]
    public bool IsFlagsEnum { get; init; }

    [JsonPropertyName("is_serializable")]
    public bool IsSerializable { get; init; }
}

internal sealed record SharedEnumMember
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

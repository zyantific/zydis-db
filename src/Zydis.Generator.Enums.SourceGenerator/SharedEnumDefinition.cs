using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Zydis.SourceGeneration.Helpers;

namespace Zydis.Generator.Enums.SourceGenerator;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json during deserialization.")]
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

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json during deserialization.")]
internal sealed record SharedEnumMember
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

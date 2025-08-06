using System.Text.Json.Serialization;

using Zydis.Generator.SourceGenerator.Helpers;

namespace Zydis.Generator.SourceGenerator;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TerminalNodeDefinition), typeDiscriminator: "terminal")]
[JsonDerivedType(typeof(DecisionNodeDefinition), typeDiscriminator: "decision")]
internal abstract record DecoderTreeNodeDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

#pragma warning disable CA1812 // Avoid uninstantiated internal classes.

internal sealed record TerminalNodeDefinition :
    DecoderTreeNodeDefinition
{
}

internal sealed record DecisionNodeDefinition :
    DecoderTreeNodeDefinition
{
    [JsonPropertyName("number_of_slots")]
    public int? NumberOfSlots { get; init; }

    [JsonPropertyName("named_slots")]
    public ImmutableEquatableArray<string>? NamedSlots { get; init; }
}

#pragma warning restore CA1812

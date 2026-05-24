using System;
using System.Linq;
using System.Text.Json.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

public sealed record FormatterStringToken
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("type")]
    public required TokenType Type { get; init; }
}

public sealed record FormatterStringDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("tokens")]
    public FormatterStringToken[]? Tokens { get; init; }

    [JsonPropertyName("string")]
    public string? StringLiteral { get; init; }

    public string? FullString
    {
        get
        {
            if (Tokens != null)
            {
                return String.Join("", Tokens.Select(x => x.Value));
            }
            return StringLiteral;
        }
    }
}

using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1036

public sealed record InstructionMetaInfo :
    IComparable<InstructionMetaInfo>,
    IComparable

#pragma warning restore CA1036
{
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("extension")]
    public string IsaExtension { get; set; } = string.Empty;

    public string IsaSet { get; set; } = string.Empty;

    public int CompareTo(InstructionMetaInfo? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Category),
            x => x.Compare(x => x.IsaExtension),
            x => x.Compare(x => x.IsaSet)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as InstructionMetaInfo);
    }
}

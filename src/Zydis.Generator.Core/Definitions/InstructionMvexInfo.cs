using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Helpers;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1036

public sealed record InstructionMvexInfo :
    IComparable<InstructionMvexInfo>,
    IComparable

#pragma warning restore CA1036
{
    public MvexFunctionality Functionality { get; init; }

    public MaskMode MaskMode { get; init; }

    [JsonPropertyName("element_granularity")]
    public bool HasElementGranularity { get; init; }

    public StaticBroadcast StaticBroadcast { get; init; }

    public int CompareTo(InstructionMvexInfo? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Functionality),
            x => x.Compare(x => x.MaskMode),
            x => x.Compare(x => x.HasElementGranularity),
            x => x.Compare(x => x.StaticBroadcast)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as InstructionMvexInfo);
    }
}

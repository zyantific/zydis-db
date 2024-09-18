using System;

using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1036

public sealed record InstructionVexInfo :
    IComparable<InstructionVexInfo>,
    IComparable

#pragma warning restore CA1036
{
    public StaticBroadcast StaticBroadcast { get; init; }

    public int CompareTo(InstructionVexInfo? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.StaticBroadcast)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as InstructionVexInfo);
    }
}

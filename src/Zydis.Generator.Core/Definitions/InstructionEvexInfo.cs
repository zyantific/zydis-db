using System;

using Zydis.Generator.Core.Helpers;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

// TODO: Model proper enums.

#pragma warning disable CA1036

public sealed record InstructionEvexInfo :
    IComparable<InstructionEvexInfo>,
    IComparable

#pragma warning restore CA1036
{
    public VectorLength VectorLength { get; init; }
    public EvexFunctionality Functionality { get; init; }
    public MaskMode MaskMode { get; init; }
    public EvexMaskFlags? MaskFlags { get; init; } // Nullable to allow setting "AcceptsZeroMask" as the default value.
    public EvexTupleType TupleType { get; init; }
    public EvexElementSize ElementSize { get; init; }
    public StaticBroadcast StaticBroadcast { get; init; }
    public bool IsEevex { get; init; }
    public bool HasNf { get; init; }
    public bool HasDfv { get; init; }
    public bool HasZu { get; init; }
    public bool HasPpx { get; init; }

    public int CompareTo(InstructionEvexInfo? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.VectorLength),
            x => x.Compare(x => x.Functionality),
            x => x.Compare(x => x.MaskMode),
            x => x.Compare(x => x.MaskFlags),
            x => x.Compare(x => x.TupleType),
            x => x.Compare(x => x.ElementSize),
            x => x.Compare(x => x.StaticBroadcast),
            x => x.Compare(x => x.IsEevex),
            x => x.Compare(x => x.HasNf),
            x => x.Compare(x => x.HasDfv),
            x => x.Compare(x => x.HasZu),
            x => x.Compare(x => x.HasPpx)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as InstructionEvexInfo);
    }
}

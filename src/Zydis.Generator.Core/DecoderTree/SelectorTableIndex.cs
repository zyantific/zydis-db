using System.Globalization;

namespace Zydis.Generator.Core.DecoderTree;

public readonly record struct SelectorTableIndex
{
    public int Index { get; init; }
    public bool IsNegated { get; init; }

    public static SelectorTableIndex Create(int index, bool isNegated)
    {
        return new SelectorTableIndex
        {
            Index = index,
            IsNegated = isNegated
        };
    }

    public static SelectorTableIndex ForIndex(int index)
    {
        return new SelectorTableIndex
        {
            Index = index,
            IsNegated = false
        };
    }

    public static SelectorTableIndex ForNegatedIndex(int index)
    {
        return new SelectorTableIndex
        {
            Index = index,
            IsNegated = true
        };
    }

#pragma warning disable CA2225

    public static implicit operator SelectorTableIndex(int index)
    {
        return ForIndex(index);
    }

#pragma warning restore CA2225

    public override string ToString()
    {
        return IsNegated ? $"!{Index}" : Index.ToString(CultureInfo.InvariantCulture);
    }
}

using System;
using System.Globalization;

namespace Zydis.Generator.Core.DecoderTree;

public readonly record struct DecisionNodeIndex
{
    public required int Index { get; init; }
    public bool IsNegated { get; init; }

    public static DecisionNodeIndex Create(int index, bool isNegated)
    {
        return new DecisionNodeIndex
        {
            Index = index,
            IsNegated = isNegated
        };
    }

    public static DecisionNodeIndex ForIndex(int index)
    {
        return new DecisionNodeIndex
        {
            Index = index,
            IsNegated = false
        };
    }

    public static DecisionNodeIndex ForNegatedIndex(int index)
    {
        return new DecisionNodeIndex
        {
            Index = index,
            IsNegated = true
        };
    }

    public static DecisionNodeIndex<TIndexEnum> Create<TIndexEnum>(TIndexEnum index, bool isNegated)
        where TIndexEnum : struct, Enum
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index,
            IsNegated = isNegated
        };
    }

    public static DecisionNodeIndex<TIndexEnum> ForIndex<TIndexEnum>(TIndexEnum index)
        where TIndexEnum : struct, Enum
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index,
            IsNegated = false
        };
    }

    public static DecisionNodeIndex<TIndexEnum> ForNegatedIndex<TIndexEnum>(TIndexEnum index)
        where TIndexEnum : struct, Enum
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index,
            IsNegated = true
        };
    }

#pragma warning disable CA2225

    public static DecisionNodeIndex operator !(DecisionNodeIndex index)
    {
        return new DecisionNodeIndex
        {
            Index = index.Index,
            IsNegated = !index.IsNegated
        };
    }

    public static implicit operator DecisionNodeIndex(int index)
    {
        return ForIndex(index);
    }

#pragma warning restore CA2225

    public override string ToString()
    {
        return IsNegated ? $"!{Index}" : Index.ToString(CultureInfo.InvariantCulture);
    }
}

public readonly record struct DecisionNodeIndex<TIndexEnum>
    where TIndexEnum : struct, Enum
{
    public required TIndexEnum Index { get; init; }
    public bool IsNegated { get; init; }

    public static DecisionNodeIndex<TIndexEnum> Create(TIndexEnum index, bool isNegated)
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index,
            IsNegated = isNegated
        };
    }

    public static DecisionNodeIndex<TIndexEnum> ForIndex(TIndexEnum index)
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index,
            IsNegated = false
        };
    }

    public static DecisionNodeIndex<TIndexEnum> ForNegatedIndex(TIndexEnum index)
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index,
            IsNegated = true
        };
    }

#pragma warning disable CA2225

    public static DecisionNodeIndex<TIndexEnum> operator !(DecisionNodeIndex<TIndexEnum> index)
    {
        return new DecisionNodeIndex<TIndexEnum>
        {
            Index = index.Index,
            IsNegated = !index.IsNegated
        };
    }

    public static implicit operator DecisionNodeIndex<TIndexEnum>(TIndexEnum index)
    {
        return ForIndex(index);
    }

#pragma warning restore CA2225

    public override string ToString()
    {
        return IsNegated ? $"!{Index}" : Index.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Zydis.Generator.Core.Helpers;

internal static class FluentComparer
{
    public static int Compare<T>(T? x, T? y, params Func<FluentComparerContext<T>, int>[] comparisons)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (ReferenceEquals(null, y))
        {
            return 1;
        }

        if (ReferenceEquals(null, x))
        {
            return -1;
        }

        var context = new FluentComparerContext<T>(x, y);

        return comparisons.Select(comparison => comparison(context)).FirstOrDefault(value => value is not 0);
    }
}

internal readonly struct FluentComparerContext<T>(T x, T y)
{
    public int Compare<TProperty>(Func<T, TProperty> selector, Func<TProperty, TProperty, int> comparison)
    {
        return comparison(selector(x), selector(y));
    }

    public int Compare<TProperty>(Func<T, TProperty?> selector)
        where TProperty : IComparable
    {
        return Comparer<TProperty>.Default.Compare(selector(x), selector(y));
    }

    public int Compare<TProperty>(Func<T, TProperty?> selector)
        where TProperty : struct, IComparable
    {
        return Comparer<TProperty?>.Default.Compare(selector(x), selector(y));
    }

    public int Compare(Func<T, string?> selector)
    {
        return string.Compare(selector(x), selector(y), StringComparison.OrdinalIgnoreCase);
    }

    public int CompareSequence<TProperty>(Func<T, IEnumerable<TProperty>?> selector, Func<TProperty, TProperty, int> comparison)
    {
        return CompareSequence(selector(x), selector(y), comparison);
    }

    public int CompareSequence(Func<T, IEnumerable<string?>?> selector)
    {
        return CompareSequence(selector(x), selector(y), (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
    }

    public int CompareSequence<TProperty>(Func<T, IEnumerable<TProperty>?> selector)
        where TProperty : IComparable
    {
        return CompareSequence(selector(x), selector(y), (a, b) => a.CompareTo(b));
    }

    public int CompareDictionary<TKey, TValue>(Func<T, IEnumerable<KeyValuePair<TKey, TValue>>?> selector, Func<TValue, TValue, int> comparison)
        where TKey : IComparable
    {
        return (selector(x), selector(y)) switch
        {
            (null, null) => 0,
            (null, not null) => -1,
            (not null, null) => 1,

            (IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y) when (x.Count < y.Count) => -1,
            (IReadOnlyDictionary<TKey, TValue> x, IReadOnlyDictionary<TKey, TValue> y) when (x.Count > y.Count) => 1,

            (IDictionary<TKey, TValue> x, IDictionary<TKey, TValue> y) when (x.Count < y.Count) => -1,
            (IDictionary<TKey, TValue> x, IDictionary<TKey, TValue> y) when (x.Count > y.Count) => 1,

            (SortedDictionary<TKey, TValue> x, SortedDictionary<TKey, TValue> y) => CompareSequence(x.Values.AsEnumerable(), y.Values.AsEnumerable(), comparison),

            ({ } x, { } y) => CompareSequence(x.OrderBy(x => x.Key).Select(x => x.Value), y.OrderBy(x => x.Key).Select(x => x.Value), comparison)
        };
    }

    public int CompareDictionary<TKey, TValue>(Func<T, IEnumerable<KeyValuePair<TKey, TValue>>?> selector)
        where TKey : IComparable
        where TValue : IComparable
    {
        return CompareDictionary(selector, (a, b) => a.CompareTo(b));
    }

    private static int CompareSequence<TProperty>(IEnumerable<TProperty>? a, IEnumerable<TProperty>? b, Func<TProperty, TProperty, int> comparison)
    {
        return (a, b) switch
        {
            (null, null) => 0,
            (null, not null) => -1,
            (not null, null) => 1,

            (IReadOnlyCollection<TProperty> x, IReadOnlyCollection<TProperty> y) when (x.Count < y.Count) => -1,
            (IReadOnlyCollection<TProperty> x, IReadOnlyCollection<TProperty> y) when (x.Count > y.Count) => 1,

            (ICollection<TProperty> x, ICollection<TProperty> y) when (x.Count < y.Count) => -1,
            (ICollection<TProperty> x, ICollection<TProperty> y) when (x.Count > y.Count) => 1,

            ({ } x, { } y) when (x.Count() < y.Count()) => -1,
            ({ } x, { } y) when (x.Count() > y.Count()) => 1,

            (IReadOnlyCollection<TProperty> x, IReadOnlyCollection<TProperty> y) => x.Zip(y).Select(x => comparison(x.First, x.Second)).FirstOrDefault(x => x is not 0),

            (ICollection<TProperty> x, ICollection<TProperty> y) => x.Zip(y).Select(x => comparison(x.First, x.Second)).FirstOrDefault(x => x is not 0),

            ({ } x, { } y) => x.Zip(y).Select(x => comparison(x.First, x.Second)).FirstOrDefault(x => x is not 0),
        };
    }
}

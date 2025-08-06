using System.Collections.Generic;
using System.Linq;

namespace Zydis.Generator.Core.Extensions;

internal static class DictionaryExtensions
{
    public static bool Compare<TKey, TValue>(this Dictionary<TKey, TValue> dict, Dictionary<TKey, TValue> other)
    {
        return dict.Count == other.Count && !dict.Except(other).Any();
    }

    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
    where TValue : new()
    {
        if (!dict.TryGetValue(key, out var value))
        {
            value = new TValue();
            dict.Add(key, value);
        }
        return value;
    }

    public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
    {
        if (!dict.TryGetValue(key, out var value))
        {
            value = defaultValue;
            dict.Add(key, value);
        }
        return value;
    }
}

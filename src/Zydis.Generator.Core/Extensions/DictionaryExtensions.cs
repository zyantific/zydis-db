using System.Collections.Generic;
using System.Linq;

namespace Zydis.Generator.Core.Extensions;

internal static class DictionaryExtensions
{
    public static bool Compare<TKey, TValue>(this Dictionary<TKey, TValue> dict, Dictionary<TKey, TValue> other)
    {
        return dict.Count == other.Count && !dict.Except(other).Any();
    }
}

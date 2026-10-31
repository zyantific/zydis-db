using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Zydis.Viewer.Core.Diff;

/// <summary>
/// Lists the values that differ between two JSON values.
/// </summary>
public static class JsonDiff
{
    /// <summary>
    /// Returns the paths of the values that differ between <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    /// <remarks>
    /// Objects are compared member by member, by name, and arrays element by element. A path joins member names with
    /// <c>.</c> and appends <c>[i]</c> for an array element, for example <c>operands[1].action</c> or
    /// <c>evex.mask_mode</c>. A member that only one side has is reported once, with its own path, and so is an array
    /// element past the end of the shorter array. Values of different kinds and unequal scalars are reported at their
    /// path. When the two values differ at the top and are not both objects or both arrays, the one path is the empty
    /// string.
    /// </remarks>
    /// <param name="a">The value to compare from.</param>
    /// <param name="b">The value to compare to.</param>
    /// <returns>
    /// The paths in the member order of <paramref name="a"/>, followed by the members only <paramref name="b"/> has,
    /// or an empty list when the values are equal under <see cref="JsonElement.DeepEquals"/>.
    /// </returns>
    public static IReadOnlyList<string> ChangedPaths(JsonElement a, JsonElement b)
    {
        var paths = new List<string>();
        Walk(a, b, string.Empty, paths);

        return paths;
    }

    private static void Walk(JsonElement a, JsonElement b, string path, List<string> paths)
    {
        switch (a.ValueKind)
        {
            case JsonValueKind.Object when b.ValueKind is JsonValueKind.Object:
                WalkObjects(a, b, path, paths);
                break;

            case JsonValueKind.Array when b.ValueKind is JsonValueKind.Array:
                WalkArrays(a, b, path, paths);
                break;

            default:
                if (!JsonElement.DeepEquals(a, b))
                {
                    paths.Add(path);
                }
                break;
        }
    }

    private static void WalkObjects(JsonElement a, JsonElement b, string path, List<string> paths)
    {
        foreach (var member in a.EnumerateObject())
        {
            var memberPath = MemberPath(path, member.Name);

            if (b.TryGetProperty(member.Name, out var other))
            {
                Walk(member.Value, other, memberPath, paths);
            }
            else
            {
                paths.Add(memberPath);
            }
        }

        foreach (var member in b.EnumerateObject())
        {
            if (!a.TryGetProperty(member.Name, out _))
            {
                paths.Add(MemberPath(path, member.Name));
            }
        }
    }

    private static void WalkArrays(JsonElement a, JsonElement b, string path, List<string> paths)
    {
        var lengthA = a.GetArrayLength();
        var lengthB = b.GetArrayLength();

        for (var i = 0; i < Math.Max(lengthA, lengthB); i++)
        {
            var elementPath = string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]");

            if ((i < lengthA) && (i < lengthB))
            {
                Walk(a[i], b[i], elementPath, paths);
            }
            else
            {
                paths.Add(elementPath);
            }
        }
    }

    private static string MemberPath(string path, string name)
    {
        return (path.Length == 0) ? name : $"{path}.{name}";
    }
}

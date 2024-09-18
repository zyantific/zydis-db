using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Zydis.Generator.Core.Helpers;

public delegate TResult TreeNodeFunc<in T, out TResult>(T? node, T? parent, int level, int index);

public static class GenericTreeVisitor
{
    public static IEnumerable<(T? Node, T? Parent, int Level, int Index)> VisitDepthFirst<T>(T? node,
        Func<T?, IEnumerable<T?>> enumerateChildren)
    {
        ArgumentNullException.ThrowIfNull(enumerateChildren);

        return Visit(node, default, 0, 0);

        IEnumerable<(T? Node, T? Parent, int Level, int Index)> Visit(T? current, T? parent, int level, int index)
        {
            Contract.Assert(level >= 0);

            yield return (current, parent, level, index);

            if (current is null)
            {
                yield break;
            }

            var i = 0;
            foreach (var child in enumerateChildren(current))
            {
                foreach (var inner in Visit(child, current, level + 1, i++))
                {
                    yield return inner;
                }
            }
        }
    }

    public static IEnumerable<(T? Node, T? Parent, int Level, int Index)> VisitBreadthFirst<T>(T? node,
        Func<T?, IEnumerable<T?>> enumerateChildren)
    {
        ArgumentNullException.ThrowIfNull(enumerateChildren);

        var queue = new Queue<(T? Node, T? Parent, int Level, int Index)>();
        queue.Enqueue((node, default, 0, 0));

        while (queue.Count > 0)
        {
            var (current, parent, level, index) = queue.Dequeue();
            yield return (current, parent, level, index);

            if (current is null)
            {
                continue;
            }

            var i = 0;
            foreach (var child in enumerateChildren(current))
            {
                queue.Enqueue((child, current, level + 1, i++));
            }
        }
    }

    public static void DebugPrint<T>(T? node, Func<T?, IEnumerable<T?>> enumerateChildren, Action<string> printLineDelegate)
    {
        ArgumentNullException.ThrowIfNull(enumerateChildren);
        ArgumentNullException.ThrowIfNull(printLineDelegate);

        DebugPrint(node, enumerateChildren, (n, _, _, _) => n?.ToString() ?? string.Empty, printLineDelegate);
    }

    public static void DebugPrint<T>(T? node, Func<T?, IEnumerable<T?>> enumerateChildren, TreeNodeFunc<T, string?> toStringDelegate, Action<string> printLineDelegate)
    {
        ArgumentNullException.ThrowIfNull(toStringDelegate);
        ArgumentNullException.ThrowIfNull(printLineDelegate);

        var text = toStringDelegate(node, default, 0, 0);
        if (text is not null)
        {
            printLineDelegate(text);
        }

        if (node is null)
        {
            return;
        }

        var builder = new StringBuilder();
        var lastLevel = 0;

        foreach (var (n, parent, level, index) in VisitDepthFirst(node, enumerateChildren).Skip(1))
        {
            text = toStringDelegate(n, parent, level, index);
            if (text is null)
            {
                continue;
            }

            if (lastLevel < level)
            {
                if (lastLevel == 0)
                {
                    builder.Append(" :.. ");
                }
                else
                {
                    builder.Length = (lastLevel - 1) * 5;
                    builder.Append(" :    :.. ");
                }

                lastLevel = level;
            }
            else if (lastLevel > level)
            {
                builder.Length = (level - 1) * 5;
                builder.Append(" :.. ");
                lastLevel = level;
            }
            else
            {
                builder.Length = lastLevel * 5;
            }

            builder.Append(text);

            printLineDelegate(builder.ToString());
        }
    }
}

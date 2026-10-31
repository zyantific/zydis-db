using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;

using Zydis.Generator.Core.Definitions;
using Zydis.Viewer.Core.Model;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Diff;

/// <summary>
/// The comparison of two databases: the pairs of their forms and the merged view tree.
/// </summary>
public sealed class DatabaseDiff
{
    private readonly DiffModel _model;

    private DatabaseDiff(DiffModel model)
    {
        _model = model;
    }

    /// <summary>
    /// Pairs the forms of <paramref name="baseDb"/> with the forms of <paramref name="headDb"/> and merges the two
    /// view trees.
    /// </summary>
    /// <remarks>
    /// Forms with equal JSON pair first, in file order. The forms left over pair by form id, unless either side has a
    /// <see cref="ViewerDatabase.FormIdError"/>. The forms still left over pair by region key, the encoding, the opcode
    /// map, the opcode, the mnemonic and the filter entries as a set, and forms with equal region keys pair in file
    /// order. The rest are added or removed.
    /// <para>
    /// The merged tree holds the rows of the head tree other than its definition rows, followed under each row by the
    /// rows only the base tree has. Each form of the head has a definition row at its head path. A removed form and
    /// the base side of a moved form have a definition row at their base path, so a removed form stays visible when
    /// another form takes its path.
    /// </para>
    /// </remarks>
    /// <param name="baseDb">The database to compare from. The caller keeps ownership.</param>
    /// <param name="headDb">The database to compare to. The caller keeps ownership.</param>
    /// <returns>The comparison of the two databases.</returns>
    public static DatabaseDiff Compare(ViewerDatabase baseDb, ViewerDatabase headDb)
    {
        ArgumentNullException.ThrowIfNull(baseDb);
        ArgumentNullException.ThrowIfNull(headDb);

        var (pairs, pairOfBase) = CreatePairs(baseDb, headDb);

        var tree = new MergedTree(pairs, pairOfBase);
        foreach (var root in headDb.Roots)
        {
            tree.AddHeadNode(root, tree.Roots);
        }
        foreach (var root in baseDb.Roots)
        {
            tree.AddBaseNode(root, tree.Roots);
        }

        var nodes = new List<DiffNodeDto>();
        var rootIds = new List<int>(tree.Roots.Count);
        foreach (var root in tree.Roots)
        {
            Summarize(root);
            rootIds.Add(AddNode(root, null, nodes));
        }

        var stats = new DiffStats(
            pairs.Count(x => x.Status is DiffStatus.Added),
            pairs.Count(x => x.Status is DiffStatus.Removed),
            pairs.Count(x => x.Status is DiffStatus.Changed),
            pairs.Count(x => x.Status is DiffStatus.Moved),
            pairs.Count(x => x.Status is DiffStatus.Unchanged));

        return new DatabaseDiff(
            new DiffModel(stats, headDb.FormIdError ?? baseDb.FormIdError, rootIds, nodes, pairs));
    }

    /// <summary>
    /// Returns the <see cref="DiffModel"/> of this comparison as JSON.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(_model, ViewerSerializerContext.Default.DiffModel);
    }

    /// <returns>
    /// The pairs in head file order, followed by the removed forms in base file order, and the position in that list
    /// of the pair of each base form.
    /// </returns>
    private static (List<DiffPairDto> Pairs, int[] PairOfBase) CreatePairs(ViewerDatabase baseDb,
        ViewerDatabase headDb)
    {
        var baseCount = baseDb.Definitions.Count;
        var headCount = headDb.Definitions.Count;

        var headOfBase = new int[baseCount];
        var baseOfHead = new int[headCount];
        Array.Fill(headOfBase, -1);
        Array.Fill(baseOfHead, -1);

        // Indexing the root array walks it from the start, so the forms are read once in order.
        var baseRaw = baseDb.Document.RootElement.EnumerateArray().ToArray();
        var headRaw = headDb.Document.RootElement.EnumerateArray().ToArray();

        // A form id depends on the sibling forms, so a deletion can hand a survivor the id of the deleted form. Pairing
        // equal forms first keeps the survivor out of the id pass. The pass reads no ids, so with positional ids it
        // keeps the region pass from pairing an unchanged form with a changed one.
        var headByContent = new Dictionary<string, Queue<int>>(headCount, StringComparer.Ordinal);
        for (var h = 0; h < headCount; h++)
        {
            var key = CompactJson(headRaw[h]);
            if (!headByContent.TryGetValue(key, out var queue))
            {
                queue = new Queue<int>();
                headByContent.Add(key, queue);
            }

            queue.Enqueue(h);
        }

        for (var b = 0; b < baseCount; b++)
        {
            if (headByContent.TryGetValue(CompactJson(baseRaw[b]), out var queue) && queue.TryDequeue(out var h))
            {
                headOfBase[b] = h;
                baseOfHead[h] = b;
            }
        }

        // The fallback ids hold the position of the form, so after a reorder equal ids name different forms.
        if ((baseDb.FormIdError is null) && (headDb.FormIdError is null))
        {
            var headById = new Dictionary<string, int>(headCount, StringComparer.Ordinal);
            for (var h = 0; h < headCount; h++)
            {
                if (baseOfHead[h] < 0)
                {
                    headById.Add(headDb.FormIds[h], h);
                }
            }

            for (var b = 0; b < baseCount; b++)
            {
                if ((headOfBase[b] < 0) && headById.TryGetValue(baseDb.FormIds[b], out var h))
                {
                    headOfBase[b] = h;
                    baseOfHead[h] = b;
                }
            }
        }

        var headByRegion = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (var h = 0; h < headCount; h++)
        {
            if (baseOfHead[h] >= 0)
            {
                continue;
            }

            var key = RegionKey(headDb.Definitions[h]);
            if (!headByRegion.TryGetValue(key, out var queue))
            {
                queue = new Queue<int>();
                headByRegion.Add(key, queue);
            }

            queue.Enqueue(h);
        }

        for (var b = 0; b < baseCount; b++)
        {
            if ((headOfBase[b] < 0) &&
                headByRegion.TryGetValue(RegionKey(baseDb.Definitions[b]), out var queue) &&
                queue.TryDequeue(out var h))
            {
                headOfBase[b] = h;
                baseOfHead[h] = b;
            }
        }

        var pairs = new List<DiffPairDto>(headCount + baseCount);
        var pairOfBase = new int[baseCount];

        for (var h = 0; h < headCount; h++)
        {
            var b = baseOfHead[h];
            if (b < 0)
            {
                pairs.Add(new DiffPairDto(null, h, DiffStatus.Added, null, headDb.Paths[h], []));
                continue;
            }

            var changed = !JsonElement.DeepEquals(baseRaw[b], headRaw[h]);
            var moved = !string.Equals(baseDb.Paths[b], headDb.Paths[h], StringComparison.Ordinal);
            var status = moved ? DiffStatus.Moved : (changed ? DiffStatus.Changed : DiffStatus.Unchanged);

            pairOfBase[b] = pairs.Count;
            pairs.Add(new DiffPairDto(b, h, status, baseDb.Paths[b], headDb.Paths[h],
                changed ? JsonDiff.ChangedPaths(baseRaw[b], headRaw[h]) : []));
        }

        for (var b = 0; b < baseCount; b++)
        {
            if (headOfBase[b] < 0)
            {
                pairOfBase[b] = pairs.Count;
                pairs.Add(new DiffPairDto(b, null, DiffStatus.Removed, baseDb.Paths[b], null, []));
            }
        }

        return (pairs, pairOfBase);
    }

    private static string CompactJson(JsonElement element)
    {
        return JsonSerializer.Serialize(element, ViewerSerializerContext.Default.JsonElement);
    }

    /// <summary>
    /// Returns the encoding, the opcode map, the opcode, the mnemonic and the sorted filter entries of
    /// <paramref name="definition"/>, which together select its region of the decoder tree.
    /// </summary>
    private static string RegionKey(InstructionDefinition definition)
    {
        var filters = (definition.Pattern ?? [])
            .Select(x => $"{x.Filter}={x.Value}")
            .Order(StringComparer.Ordinal);

        return string.Create(CultureInfo.InvariantCulture,
            $"{definition.Encoding}|{definition.OpcodeMap}|{definition.Opcode:X2}|{definition.Mnemonic}|" +
            $"{string.Join(",", filters)}");
    }

    private static void Summarize(MergedNode node)
    {
        if (node.Kind is ViewNodeKind.Definition)
        {
            node.FormCount = 1;
            node.Added = (node.Status is DiffStatus.Added) ? 1 : 0;
            node.Removed = (node.Status is DiffStatus.Removed) ? 1 : 0;
            node.Changed = (node.Status is DiffStatus.Changed) ? 1 : 0;
            // The row a moved form leaves at its base path counts nowhere, so the counts of a root equal the stats.
            node.Moved = (node.Status is DiffStatus.Moved) && (node.HeadDefinitionIndex is not null) ? 1 : 0;
            return;
        }

        foreach (var child in node.Children)
        {
            Summarize(child);
            node.FormCount += child.FormCount;
            node.Added += child.Added;
            node.Removed += child.Removed;
            node.Changed += child.Changed;
            node.Moved += child.Moved;
            node.Conflict |= child.Conflict;
        }

        node.Conflict |= node.Kind is ViewNodeKind.Conflict;
    }

    private static int AddNode(MergedNode node, int? parentId, List<DiffNodeDto> nodes)
    {
        var id = nodes.Count;
        var childIds = new List<int>(node.Children.Count);

        // Added before the children, so the ids follow the preorder.
        nodes.Add(new DiffNodeDto(
            id,
            parentId,
            node.Kind,
            node.Label,
            node.Detail,
            node.Filter,
            childIds,
            node.HeadDefinitionIndex ?? node.BaseDefinitionIndex,
            node.Conflict,
            node.FormCount,
            node.Status,
            new DiffCounts(node.Added, node.Removed, node.Changed, node.Moved),
            node.BaseDefinitionIndex,
            node.HeadDefinitionIndex));

        foreach (var child in node.Children)
        {
            childIds.Add(AddNode(child, id, nodes));
        }

        return id;
    }

    /// <summary>
    /// The merged tree under construction. The rows of the head tree come first, so the rows the base tree adds
    /// follow the head rows under each parent in base order.
    /// </summary>
    private sealed class MergedTree(List<DiffPairDto> pairs, int[] pairOfBase)
    {
        private readonly Dictionary<(ViewNodeKind Kind, string Path), MergedNode> _rows = [];

        public List<MergedNode> Roots { get; } = [];

        public void AddHeadNode(ViewNode node, List<MergedNode> siblings)
        {
            // Stays added unless the base tree has the same row.
            var row = new MergedNode(node.Kind, node.Label, node.Detail, node.Filter, DiffStatus.Added);
            AddRow(node, row);
            siblings.Add(row);

            foreach (var child in node.Children)
            {
                if (child.Kind is not ViewNodeKind.Definition)
                {
                    AddHeadNode(child, row.Children);
                    continue;
                }

                // The pairs start with the head forms in file order.
                var pair = pairs[child.DefinitionIndex];
                row.Children.Add(new MergedNode(child.Kind, child.Label, child.Detail, null, pair.Status)
                {
                    BaseDefinitionIndex = pair.BaseIndex,
                    HeadDefinitionIndex = child.DefinitionIndex
                });
            }
        }

        public void AddBaseNode(ViewNode node, List<MergedNode> siblings)
        {
            if (_rows.TryGetValue((node.Kind, node.Path), out var row))
            {
                row.Status = DiffStatus.Unchanged;
            }
            else
            {
                row = new MergedNode(node.Kind, node.Label, node.Detail, node.Filter, DiffStatus.Removed);
                AddRow(node, row);
                siblings.Add(row);
            }

            foreach (var child in node.Children)
            {
                if (child.Kind is not ViewNodeKind.Definition)
                {
                    AddBaseNode(child, row.Children);
                    continue;
                }

                var status = pairs[pairOfBase[child.DefinitionIndex]].Status;
                if (status is not (DiffStatus.Removed or DiffStatus.Moved))
                {
                    continue;
                }

                row.Children.Add(new MergedNode(child.Kind, child.Label, child.Detail, null, status)
                {
                    BaseDefinitionIndex = child.DefinitionIndex
                });
            }
        }

        /// <remarks>
        /// The tree builder gives sibling rows distinct labels, so a second row with the same key is a builder bug. The
        /// exception names the row, and its type differs from the <see cref="ArgumentException"/> a caller gets for a
        /// bad argument. The base walk adds a row only after a failed lookup, so a duplicate in a tree shows when that
        /// tree is the head of a comparison.
        /// </remarks>
        private void AddRow(ViewNode node, MergedNode row)
        {
            if (!_rows.TryAdd((node.Kind, node.Path), row))
            {
                throw new UnreachableException($"Two {node.Kind} rows have the path '{node.Path}'.");
            }
        }
    }

    private sealed class MergedNode(ViewNodeKind kind, string label, string? detail, string? filter,
        DiffStatus status)
    {
        public ViewNodeKind Kind { get; } = kind;

        public string Label { get; } = label;

        public string? Detail { get; } = detail;

        public string? Filter { get; } = filter;

        public DiffStatus Status { get; set; } = status;

        public int? BaseDefinitionIndex { get; init; }

        public int? HeadDefinitionIndex { get; init; }

        public List<MergedNode> Children { get; } = [];

        public int FormCount { get; set; }

        public int Added { get; set; }

        public int Removed { get; set; }

        public int Changed { get; set; }

        public int Moved { get; set; }

        public bool Conflict { get; set; }
    }
}

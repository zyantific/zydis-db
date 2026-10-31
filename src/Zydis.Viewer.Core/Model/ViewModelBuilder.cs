using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Model;

/// <summary>
/// Creates the <see cref="ViewModel"/> of a <see cref="ViewerDatabase"/> and the texts its rows show.
/// </summary>
internal static class ViewModelBuilder
{
    /// <summary>
    /// Creates the view model of <paramref name="database"/>.
    /// </summary>
    public static ViewModel Build(ViewerDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var nodes = new List<ViewNodeDto>();
        var filters = new SortedSet<string>(StringComparer.Ordinal);
        var rootIds = database.Roots.Select(x => AddNode(x, null, nodes, filters)).ToList();

        var definitions = new List<ViewDefinitionDto>(database.Definitions.Count);
        foreach (var raw in database.Document.RootElement.EnumerateArray())
        {
            var index = definitions.Count;
            var definition = database.Definitions[index];

            definitions.Add(new ViewDefinitionDto(
                index,
                database.FormIds[index],
                definition.Mnemonic,
                OperandSummary(definition),
                CommentText(definition),
                definition.NumberOfVisibleOperands,
                database.Paths[index],
                raw));
        }

        var filterSlots = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var filter in filters)
        {
            var (nodeDefinition, _) = DecisionNodes.ParseDecisionNodeType(filter);
            var slots = Enumerable.Range(0, nodeDefinition.NumberOfSlots).Select(nodeDefinition.GetSlotName);
            filterSlots.Add(filter, [.. slots]);
        }

        var stats = new ViewStats(
            definitions.Count,
            nodes.Count,
            nodes.Count(x => x.Kind is ViewNodeKind.Conflict));

        return new ViewModel(database.Schema, stats, database.FormIdError, rootIds, nodes, definitions, filterSlots);
    }

    /// <summary>
    /// Returns the operand summary of <paramref name="definition"/>: the JSON names of the operand types of the
    /// visible operands, joined by <c>", "</c>, with the register name for an implicit register operand.
    /// </summary>
    public static string OperandSummary(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var names = (definition.Operands ?? [])
            .Where(x => x.Visibility is not OperandVisibility.Hidden)
            .Select(x => (x.Type is OperandType.ImplicitReg) ? JsonName(x.Register) : JsonName(x.Type));

        return string.Join(", ", names);
    }

    /// <summary>
    /// Returns the <c>comment</c> of <paramref name="definition"/> decoded from base64 to UTF-8, or
    /// <see langword="null"/> when the form has no comment or the comment is not base64.
    /// </summary>
    public static string? CommentText(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Comment is not { } comment)
        {
            return null;
        }

        // Base64 encodes three bytes in four characters, so this bounds the decoded length.
        var buffer = new byte[comment.Length * 3 / 4];

        return Convert.TryFromBase64String(comment, buffer, out var length)
            ? Encoding.UTF8.GetString(buffer, 0, length)
            : null;
    }

    /// <summary>
    /// Returns the detail text of a table row: the JSON names of the encoding, the opcode map and, when the table has
    /// one, the refining prefix, joined by <c>", "</c>.
    /// </summary>
    public static string TableDetail(TableInfo table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var detail = $"{JsonName(table.Encoding)}, {JsonName(table.Map)}";

        return (table.Prefix is { } prefix) ? $"{detail}, {PrefixName(prefix)}" : detail;
    }

    private static int AddNode(ViewNode node, int? parentId, List<ViewNodeDto> nodes, SortedSet<string> filters)
    {
        var id = nodes.Count;
        var childIds = new List<int>(node.Children.Count);

        // Added before the children, so the ids follow the preorder.
        nodes.Add(new ViewNodeDto(
            id,
            parentId,
            node.Kind,
            node.Label,
            node.Detail,
            node.Filter,
            childIds,
            (node.DefinitionIndex >= 0) ? node.DefinitionIndex : null,
            node.HasConflict,
            node.FormCount));

        if (node.Filter is not null)
        {
            filters.Add(node.Filter);
        }

        foreach (var child in node.Children)
        {
            childIds.Add(AddNode(child, id, nodes, filters));
        }

        return id;
    }

    /// <summary>
    /// Returns the name the data file uses for the enum <paramref name="value"/>.
    /// </summary>
    private static string JsonName<T>(T value)
        where T : struct, Enum
    {
        var typeInfo = (JsonTypeInfo<T>)DefinitionReader.Options.GetTypeInfo(typeof(T));

        return JsonSerializer.SerializeToElement(value, typeInfo).GetString() ??
            throw new UnreachableException($"The enum value '{value}' has no JSON name.");
    }

    /// <remarks>
    /// The data file names a refining prefix by the <c>mandatory_prefix</c> value that selects it.
    /// </remarks>
    private static string PrefixName(RefiningPrefix prefix)
    {
        var slot = prefix switch
        {
            RefiningPrefix.PNP => MandatoryPrefixNode.Slot.PNP,
            RefiningPrefix.P66 => MandatoryPrefixNode.Slot.P66,
            RefiningPrefix.PF3 => MandatoryPrefixNode.Slot.PF3,
            RefiningPrefix.PF2 => MandatoryPrefixNode.Slot.PF2,
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null)
        };

        var nodeDefinition = MandatoryPrefixNode.NodeDefinition.Instance;

        return nodeDefinition.GetSlotName(nodeDefinition.GetSlotIndex(slot));
    }
}

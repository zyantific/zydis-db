using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Viewer.Core.Tree;

/// <summary>
/// Builds the view tree of a database by inserting each form along its recorded filter order.
/// </summary>
/// <remarks>
/// Each form appears once in the tree. The generator copies a form that does not test the filter of a node into every
/// claimed slot of that node as well as into its <c>else</c> slot, and the view keeps only the <c>else</c> copy.
/// </remarks>
public sealed class ViewTreeBuilder
{
    private const string ElseLabel = "else";
    private const string ConflictLabel = "conflict";

    private readonly Dictionary<byte, Cell> _primaryTable = [];

    /// <summary>
    /// Inserts <paramref name="definition"/> at the slot its filter entries lead to, in their recorded order.
    /// </summary>
    /// <param name="definition">The form to insert.</param>
    /// <param name="definitionIndex">The position of the form in the database.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown if the form uses a filter that has no decision node definition, or a value that names no slot of its
    /// filter.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the form does not belong to the primary opcode table.
    /// </exception>
    public void Insert(InstructionDefinition definition, int definitionIndex)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Encoding is not InstructionEncoding.Default || definition.OpcodeMap is not OpcodeMap.MAP0)
        {
            throw new NotSupportedException(
                $"Form '{definition.Mnemonic}' does not belong to the primary opcode table.");
        }

        // Parsed before the tree changes, so a form with a bad entry leaves no partial path behind.
        var pending = ParseEntries(definition);

        if (!_primaryTable.TryGetValue(definition.Opcode, out var cell))
        {
            cell = new Cell();
            _primaryTable.Add(definition.Opcode, cell);
        }

        while (pending.Count > 0)
        {
            if (cell.Content is DecisionContent decision)
            {
                // A match after the first pending entry means the recorded order is stale. The node decides here
                // anyway, so that entry is used now.
                var position = pending.FindIndex(x => x.Filter == decision.Filter);
                if (position < 0)
                {
                    cell = decision.Else;
                    continue;
                }

                cell = decision.GetSlot(pending[position].Slot);
                pending.RemoveAt(position);
                continue;
            }

            // A form or conflict found here has no entries left, so it does not test this filter and belongs in
            // the else slot of the new node.
            var entry = pending[0];
            var created = new DecisionContent(entry.Filter, entry.NodeDefinition);
            created.Else.Content = cell.Content;
            cell.Content = created;
            cell = created.GetSlot(entry.Slot);
            pending.RemoveAt(0);
        }

        while (cell.Content is DecisionContent decision)
        {
            cell = decision.Else;
        }

        var form = new FormContent(definition, definitionIndex);

        switch (cell.Content)
        {
            case null:
                cell.Content = form;
                break;
            case FormContent existing:
                cell.Content = new ConflictContent(existing, form);
                break;
            case ConflictContent conflict:
                conflict.Forms.Add(form);
                break;
        }
    }

    /// <summary>
    /// Creates the view tree of the inserted forms.
    /// </summary>
    /// <returns>The root rows in display order.</returns>
    public IReadOnlyList<ViewNode> Build()
    {
        var table = ViewNode.CreateRoot(ViewNodeKind.Table,
            OpcodeTables.FormatTableString(InstructionEncoding.Default, OpcodeMap.MAP0, null));

        foreach (var (opcode, cell) in _primaryTable.OrderBy(x => x.Key))
        {
            AddSlotRow(table, ViewNodeKind.Opcode, opcode.ToString("X2", CultureInfo.InvariantCulture), cell);
        }

        Summarize(table);

        return [table];
    }

    private static List<PendingEntry> ParseEntries(InstructionDefinition definition)
    {
        var entries = new List<PendingEntry>();

        foreach (var entry in definition.Pattern ?? [])
        {
            DecisionNodeDefinition nodeDefinition;
            try
            {
                (nodeDefinition, _) = DecisionNodes.ParseDecisionNodeType(entry.Filter);
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidDataException(
                    $"Form '{definition.Mnemonic}' uses the unknown filter '{entry.Filter}'.", exception);
            }

            DecisionNodeIndex slot;
            try
            {
                slot = nodeDefinition.ParseSlotIndex(entry.Value);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Form '{definition.Mnemonic}' uses the unknown value '{entry.Value}' for the filter " +
                    $"'{entry.Filter}'.", exception);
            }

            entries.Add(new PendingEntry(entry.Filter, nodeDefinition, slot));
        }

        return entries;
    }

    private static void AddSlotRow(ViewNode parent, ViewNodeKind kind, string label, Cell cell)
    {
        // An else slot stays empty when no form reaches it.
        if (cell.Content is null)
        {
            return;
        }

        AddContent(parent.AddChild(kind, label), cell.Content);
    }

    private static void AddContent(ViewNode parent, Content content)
    {
        switch (content)
        {
            case FormContent form:
                parent.AddDefinition(form.Definition, form.DefinitionIndex);
                break;

            case ConflictContent conflict:
                var conflictRow = parent.AddChild(ViewNodeKind.Conflict, ConflictLabel);
                foreach (var member in conflict.Forms)
                {
                    conflictRow.AddDefinition(member.Definition, member.DefinitionIndex);
                }
                break;

            case DecisionContent decision:
                var decisionRow = parent.AddChild(ViewNodeKind.Decision, decision.Filter, decision.Filter);
                var slots = decision.Slots
                    .OrderBy(x => x.Key.IsNegated)
                    .ThenBy(x => x.Key.Index);
                foreach (var (index, cell) in slots)
                {
                    AddSlotRow(decisionRow, ViewNodeKind.Slot, SlotLabel(decision.NodeDefinition, index), cell);
                }
                AddSlotRow(decisionRow, ViewNodeKind.Slot, ElseLabel, decision.Else);
                break;
        }
    }

    private static string SlotLabel(DecisionNodeDefinition nodeDefinition, DecisionNodeIndex index)
    {
        var name = nodeDefinition.GetSlotName(index.Index);

        return index.IsNegated
            ? string.Create(CultureInfo.InvariantCulture, $"!{index.Index:X2} (not {name})")
            : string.Create(CultureInfo.InvariantCulture, $"{index.Index:X2} ({name})");
    }

    private static void Summarize(ViewNode node)
    {
        if (node.Kind is ViewNodeKind.Definition)
        {
            node.FormCount = 1;
            return;
        }

        foreach (var child in node.Children)
        {
            Summarize(child);
            node.FormCount += child.FormCount;
            node.HasConflict |= child.HasConflict;
        }

        node.HasConflict |= node.Kind is ViewNodeKind.Conflict;
    }

    private readonly record struct PendingEntry(
        string Filter, DecisionNodeDefinition NodeDefinition, DecisionNodeIndex Slot);

    /// <summary>
    /// A slot of the tree under construction. Moving a form into the else slot of a new node replaces the content of
    /// the slot, so the builder holds slots by reference.
    /// </summary>
    private sealed class Cell
    {
        public Content? Content { get; set; }
    }

    private abstract class Content;

    private sealed class DecisionContent(string filter, DecisionNodeDefinition nodeDefinition) :
        Content
    {
        public string Filter { get; } = filter;

        public DecisionNodeDefinition NodeDefinition { get; } = nodeDefinition;

        public Dictionary<DecisionNodeIndex, Cell> Slots { get; } = [];

        public Cell Else { get; } = new();

        public Cell GetSlot(DecisionNodeIndex index)
        {
            if (!Slots.TryGetValue(index, out var cell))
            {
                cell = new Cell();
                Slots.Add(index, cell);
            }

            return cell;
        }
    }

    private sealed class FormContent(InstructionDefinition definition, int definitionIndex) :
        Content
    {
        public InstructionDefinition Definition { get; } = definition;

        public int DefinitionIndex { get; } = definitionIndex;
    }

    private sealed class ConflictContent(FormContent first, FormContent second) :
        Content
    {
        public List<FormContent> Forms { get; } = [first, second];
    }
}

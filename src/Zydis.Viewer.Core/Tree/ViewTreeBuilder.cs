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
/// <para>
/// The generator wires its opcode tables into a graph, in which the <c>C4</c> and <c>C5</c> rows share the VEX tables
/// and the <c>D5</c> row leads back to <c>PRIMARY</c> and <c>0F</c>. The view gives each table one owner, the first row
/// that leads to it, and every later row that leads to it becomes a <see cref="ViewNodeKind.Reference"/> row.
/// </para>
/// </remarks>
public sealed class ViewTreeBuilder
{
    private const string ElseLabel = "else";
    private const string ConflictLabel = "conflict";

    private readonly Dictionary<int, Dictionary<byte, Cell>> _tables = [];

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
    /// Thrown if the <c>mandatory_prefix</c> filter of a form with an encoding other than the default one is
    /// <c>ignore</c> or negated.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the form belongs to no opcode table of the generator, for example a vector form without a
    /// <c>mandatory_prefix</c> filter.
    /// </exception>
    public void Insert(InstructionDefinition definition, int definitionIndex)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Parsed before the tree changes, so a form with a bad entry leaves no partial path behind.
        var pending = ParseEntries(definition);
        var tableId = OpcodeTableLayout.TableIdOf(definition);

        // The mandatory prefix of a vector form selects its table, and the generator drops the entry from the tests
        // inside that table.
        if (definition.Encoding is not (InstructionEncoding.Default or InstructionEncoding.AMD3DNOW))
        {
            pending.RemoveAll(x => ReferenceEquals(x.NodeDefinition, MandatoryPrefixNode.NodeDefinition.Instance));
        }

        if (!_tables.TryGetValue(tableId, out var table))
        {
            table = [];
            _tables.Add(tableId, table);
        }

        if (!table.TryGetValue(definition.Opcode, out var cell))
        {
            cell = new Cell();
            table.Add(definition.Opcode, cell);
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
    /// <returns>The root rows in display order: the primary opcode table, which leads to every other table.</returns>
    public IReadOnlyList<ViewNode> Build()
    {
        var primary = ViewNode.CreateRoot(ViewNodeKind.Table, OpcodeTableLayout.Primary.Label);
        primary.Table = OpcodeTableLayout.Primary.Table;
        var owners = new Dictionary<int, ViewNode> { [OpcodeTableLayout.Primary.TableId] = primary };

        AddOpcodeRows(primary, OpcodeTableLayout.Primary.TableId, owners);
        Summarize(primary);

        return [primary];
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

    /// <remarks>
    /// The rows are added depth first in opcode order. That order reaches <c>PRIMARY/0F</c> before the <c>D5</c> row
    /// and <c>C4</c> before <c>C5</c>, so the first row that leads to a table owns it.
    /// </remarks>
    private void AddOpcodeRows(ViewNode tableRow, int tableId, Dictionary<int, ViewNode> owners)
    {
        var cells = _tables.GetValueOrDefault(tableId) ?? [];
        var links = OpcodeTableLayout.LinksOf(tableId);

        foreach (var opcode in cells.Keys.Union(links.Keys).Order())
        {
            var label = opcode.ToString("X2", CultureInfo.InvariantCulture);
            var cell = cells.GetValueOrDefault(opcode);

            switch (links.GetValueOrDefault(opcode))
            {
                case EscapeLink escape when HoldsForm(escape.Target.TableId):
                    var escapeRow = tableRow.AddChild(ViewNodeKind.Opcode, label);
                    // The decoder never reaches a form at an escape opcode. Showing it next to the table keeps it
                    // in the view instead of dropping it.
                    if (cell?.Content is { } content)
                    {
                        AddContent(escapeRow, content);
                    }
                    AddTableRow(escapeRow, escape.Target, owners);
                    break;

                case SwitchLink switchLink:
                    AddSwitchRow(tableRow, label, switchLink, cell, owners);
                    break;

                default:
                    if (cell is not null)
                    {
                        AddSlotRow(tableRow, ViewNodeKind.Opcode, label, cell);
                    }
                    break;
            }
        }
    }

    private void AddSwitchRow(ViewNode tableRow, string label, SwitchLink link, Cell? cell,
        Dictionary<int, ViewNode> owners)
    {
        var slots = link.Slots.Where(x => HoldsForm(x.Target.TableId)).ToList();
        if ((cell?.Content is null) && (slots.Count == 0))
        {
            return;
        }

        var switchRow = tableRow.AddChild(ViewNodeKind.Opcode, label).AddChild(ViewNodeKind.Switch, link.Name);

        if (cell is not null)
        {
            AddSlotRow(switchRow, ViewNodeKind.Slot, link.DefaultSlot, cell);
        }

        foreach (var slot in slots)
        {
            AddTableRow(switchRow.AddChild(ViewNodeKind.Slot, slot.Name), slot.Target, owners);
        }
    }

    private void AddTableRow(ViewNode parent, TableTarget target, Dictionary<int, ViewNode> owners)
    {
        if (owners.TryGetValue(target.TableId, out var owner))
        {
            var reference = parent.AddChild(ViewNodeKind.Reference, owner.Label);
            // The root table has no row above it, so a reference to it names the table row itself.
            reference.Detail = (owner.Parent ?? owner).Path;
            return;
        }

        var tableRow = parent.AddChild(ViewNodeKind.Table, target.Label);
        tableRow.Table = target.Table;
        owners.Add(target.TableId, tableRow);
        AddOpcodeRows(tableRow, target.TableId, owners);
    }

    /// <remarks>
    /// A form behind an escape opcode counts, so <c>PRIMARY/0F</c> shows when only the <c>0F38</c> table holds forms.
    /// A form behind a switch does not, so the <c>D5</c> rows lead back only to tables that hold legacy forms.
    /// </remarks>
    private bool HoldsForm(int tableId)
    {
        return (_tables.TryGetValue(tableId, out var cells) && (cells.Count > 0)) ||
            OpcodeTableLayout.LinksOf(tableId).Values.OfType<EscapeLink>().Any(x => HoldsForm(x.Target.TableId));
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
                var shown = Compact(decision);
                var decisionRow = parent.AddChild(ViewNodeKind.Decision, shown.Filter, shown.Filter);
                var slots = shown.Slots
                    .OrderBy(x => x.Key.IsNegated)
                    .ThenBy(x => x.Key.Index);
                foreach (var (index, cell) in slots)
                {
                    AddSlotRow(decisionRow, ViewNodeKind.Slot, SlotLabel(shown.NodeDefinition, index), cell);
                }
                AddSlotRow(decisionRow, ViewNodeKind.Slot, ElseLabel, shown.Else);
                break;
        }
    }

    /// <summary>
    /// Applies the compact rules of the generator: a <c>mode</c> node that only tells <c>64</c> from the rest and a
    /// <c>modrm_mod</c> node that only tells <c>3</c> from the rest become two-slot nodes.
    /// </summary>
    /// <returns>
    /// A new compact node that shares the slots of <paramref name="decision"/>, or <paramref name="decision"/> itself
    /// when no rule applies. The node is left as it is, so a later insertion still finds its filter.
    /// </returns>
    private static DecisionContent Compact(DecisionContent decision)
    {
        return decision.NodeDefinition switch
        {
            ModeNode.NodeDefinition => Compact(decision,
                ModeNode.NodeDefinition.Instance.GetSlotIndex(ModeNode.Slot.M64),
                ModeCompactNode.NodeDefinition.Instance,
                ModeCompactNode.NodeDefinition.Instance.GetSlotIndex(ModeCompactNode.Slot.M64),
                ModeCompactNode.NodeDefinition.Instance.GetSlotIndex(ModeCompactNode.Slot.MNot64)),

            ModrmModNode.NodeDefinition => Compact(decision,
                ModrmModNode.NodeDefinition.Instance.GetSlotIndex(ModrmModNode.Slot.M3),
                ModrmModCompactNode.NodeDefinition.Instance,
                ModrmModCompactNode.NodeDefinition.Instance.GetSlotIndex(ModrmModCompactNode.Slot.Register),
                ModrmModCompactNode.NodeDefinition.Instance.GetSlotIndex(ModrmModCompactNode.Slot.Memory)),

            _ => decision
        };
    }

    private static DecisionContent Compact(DecisionContent decision, int slot,
        DecisionNodeDefinition compactDefinition, int compactSlot, int compactRest)
    {
        Cell? match = null;
        Cell? negated = null;

        foreach (var (index, cell) in decision.Slots)
        {
            if (cell.Content is null)
            {
                continue;
            }

            if (index.Index != slot)
            {
                return decision;
            }

            if (index.IsNegated)
            {
                negated = cell;
            }
            else
            {
                match = cell;
            }
        }

        // The negated slot and else both stand for the rest, and a single compact slot cannot hold both of them.
        if ((negated is not null) && (decision.Else.Content is not null))
        {
            return decision;
        }

        var compact = new DecisionContent(compactDefinition.Name, compactDefinition);

        if (match is not null)
        {
            compact.Slots.Add(DecisionNodeIndex.ForIndex(compactSlot), match);
        }

        compact.Slots.Add(DecisionNodeIndex.ForIndex(compactRest), negated ?? decision.Else);

        return compact;
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

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Viewer.Core.Tree;

/// <summary>
/// Places forms in the opcode tables of the generator and describes the rows that lead from one opcode table to
/// another.
/// </summary>
public static class OpcodeTableLayout
{
    private static readonly Dictionary<int, Dictionary<byte, TableLink>> Links = ReadGeneratorWiring();

    /// <summary>
    /// The primary opcode table, the root of the view tree.
    /// </summary>
    internal static TableTarget Primary { get; } =
        CreateTarget(InstructionEncoding.Default, OpcodeMap.MAP0, null);

    /// <summary>
    /// Returns the opcode table of <paramref name="definition"/>, picked the way the generator buckets it.
    /// </summary>
    /// <param name="definition">The form to place.</param>
    /// <returns>
    /// The encoding, the opcode map and the refining prefix that select the table. The prefix is
    /// <see langword="null"/> for the <see cref="InstructionEncoding.Default"/> encoding.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the <c>mandatory_prefix</c> filter of a form with another encoding is <c>ignore</c> or negated.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the form has no opcode table, for example a vector form without a <c>mandatory_prefix</c> filter, or
    /// if its <c>mandatory_prefix</c> value names no slot of that filter.
    /// </exception>
    public static (InstructionEncoding Encoding, OpcodeMap Map, RefiningPrefix? Prefix) TableOf(
        InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var prefix = OpcodeTableRouting.GetRefiningPrefix(definition);

        // Throws for the combinations the generator cannot bucket, so a form never lands in a table no row leads to.
        _ = OpcodeTables.GetTableId(definition.Encoding, definition.OpcodeMap, prefix);

        return (definition.Encoding, definition.OpcodeMap, prefix);
    }

    /// <summary>
    /// Returns the label of an opcode table, as the generator names it.
    /// </summary>
    /// <param name="encoding">The instruction encoding of the table.</param>
    /// <param name="map">The opcode map of the table.</param>
    /// <param name="prefix">
    /// The refining prefix of the table. Must be <see langword="null"/> for <see cref="InstructionEncoding.Default"/>
    /// and set for every other encoding.
    /// </param>
    /// <returns>The table label, for example <c>PRIMARY</c>, <c>0F38</c> or <c>VEX_66_0F</c>.</returns>
    public static string TableLabel(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
    {
        return OpcodeTables.FormatTableString(encoding, map, prefix);
    }

    /// <summary>
    /// Returns the id of the opcode table of <paramref name="definition"/>, the key the generator buckets it by.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown in the cases <see cref="TableOf"/> names.</exception>
    /// <exception cref="ArgumentException">Thrown in the cases <see cref="TableOf"/> names.</exception>
    internal static int TableIdOf(InstructionDefinition definition)
    {
        var (encoding, map, prefix) = TableOf(definition);

        return OpcodeTables.GetTableId(encoding, map, prefix);
    }

    /// <summary>
    /// Returns the rows of the table with the id <paramref name="tableId"/> that lead to other tables, by opcode.
    /// </summary>
    internal static IReadOnlyDictionary<byte, TableLink> LinksOf(int tableId)
    {
        return Links.TryGetValue(tableId, out var links) ? links : [];
    }

    private static Dictionary<int, Dictionary<byte, TableLink>> ReadGeneratorWiring()
    {
        // The generator wires a row only when its target table holds a node. A marker in every slot of every table
        // makes it wire every row, and the marker it moves into a switch shows which slot is the default slot.
        var tables = new OpcodeTables();
        var marker = new Marker();

        foreach (var table in tables.Tables)
        {
            for (var opcode = 0; opcode < table.Definition.NumberOfSlots; opcode++)
            {
                table[opcode] = marker;
            }
        }

        OpcodeTableRouting.WireSwitchNodes(tables);

        var links = new Dictionary<int, Dictionary<byte, TableLink>>();

        for (var tableId = 0; tableId < tables.Tables.Count; tableId++)
        {
            var table = tables.Tables[tableId];

            for (var opcode = 0; opcode < table.Definition.NumberOfSlots; opcode++)
            {
                TableLink? link = table[opcode] switch
                {
                    OpcodeTableSwitchNode escape => new EscapeLink(CreateTarget(escape)),
                    DecisionNode switchNode => ReadSwitch(switchNode),
                    _ => null
                };

                if (link is null)
                {
                    continue;
                }

                if (!links.TryGetValue(tableId, out var tableLinks))
                {
                    tableLinks = [];
                    links.Add(tableId, tableLinks);
                }

                tableLinks.Add((byte)opcode, link);
            }
        }

        return links;
    }

    private static SwitchLink ReadSwitch(DecisionNode switchNode)
    {
        string? defaultSlot = null;
        var slots = new List<SwitchSlot>();

        for (var index = 0; index < switchNode.Definition.NumberOfSlots; index++)
        {
            var name = switchNode.Definition.GetSlotName(index);

            switch (switchNode[index])
            {
                case Marker:
                    defaultSlot = name;
                    break;

                case OpcodeTableSwitchNode target:
                    slots.Add(new SwitchSlot(name, CreateTarget(target)));
                    break;
            }
        }

        return new SwitchLink(switchNode.Definition.Name, defaultSlot ?? throw new UnreachableException(), slots);
    }

    private static TableTarget CreateTarget(OpcodeTableSwitchNode node)
    {
        return CreateTarget(node.Encoding, node.Map, node.Prefix);
    }

    private static TableTarget CreateTarget(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
    {
        return new TableTarget(OpcodeTables.GetTableId(encoding, map, prefix), TableLabel(encoding, map, prefix));
    }

    /// <summary>
    /// Fills the tables of the wiring probe. It never reaches the view tree.
    /// </summary>
    private sealed class Marker() :
        TerminalNode(MarkerDefinition.Instance);

    private sealed class MarkerDefinition :
        TerminalNodeDefinition
    {
        public static MarkerDefinition Instance { get; } = new();

        public override string Name => "marker";

        public override int EncodedSize => 0;
    }
}

/// <summary>
/// An opcode table a row leads to.
/// </summary>
/// <param name="TableId">The generator's id of the table.</param>
/// <param name="Label">The label of the table.</param>
internal sealed record TableTarget(int TableId, string Label);

/// <summary>
/// A row of an opcode table that leads to other tables.
/// </summary>
internal abstract record TableLink;

/// <summary>
/// An escape opcode that leads to one table, for example <c>0F</c> in the primary table.
/// </summary>
/// <param name="Target">The table the opcode leads to.</param>
internal sealed record EscapeLink(TableTarget Target) :
    TableLink;

/// <summary>
/// An opcode that holds a switch node, for example <c>C4</c> in the primary table.
/// </summary>
/// <param name="Name">The name of the switch node definition.</param>
/// <param name="DefaultSlot">The name of the slot that keeps the forms of the host table at the opcode.</param>
/// <param name="Slots">The slots that lead to tables, in slot order.</param>
internal sealed record SwitchLink(string Name, string DefaultSlot, IReadOnlyList<SwitchSlot> Slots) :
    TableLink;

/// <summary>
/// A slot of a switch node that leads to a table.
/// </summary>
/// <param name="Name">The slot name from the node definition.</param>
/// <param name="Target">The table the slot leads to.</param>
internal sealed record SwitchSlot(string Name, TableTarget Target);

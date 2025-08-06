using System;
using System.Collections.Generic;
using System.Linq;

namespace Zydis.Generator.Core.DecoderTree.Emitters;

/// <summary>
/// Provides an abstract base class for emitting opcode tables.
/// </summary>
/// <remarks>
/// This class defines the core functionality for traversing and processing a <see cref="OpcodeTableNode"/> and all
/// child nodes, handling of inverted values, and managing address calculations during the emission process. Derived
/// classes must implement the abstract methods to handle specific node types and their encoding logic.
/// </remarks>
public abstract class OpcodeTableEmitter
{
    private readonly Queue<DecoderTreeNode> _queue = new();
    private readonly DecoderTableEmitterStatistics? _statistics;

    protected int CurrentAddress { get; private set; }
    protected int TargetAddress { get; private set; }

    protected OpcodeTableEmitter(DecoderTableEmitterStatistics? statistics = null)
    {
        _statistics = statistics;
    }

    /// <summary>
    /// Emits the specified <see cref="OpcodeTableNode"/> and calculates the total size (number of items) of the data.
    /// </summary>
    /// <param name="node">The <see cref="OpcodeTableNode"/> to emit.</param>
    /// <param name="startAddress">The starting address for encoding. Defaults to 0 if not specified.</param>
    /// <returns>The total size (number of items) of the encoded table.</returns>
    public int Emit(OpcodeTableNode node, int startAddress = 0)
    {
        ArgumentNullException.ThrowIfNull(node);

        CurrentAddress = startAddress;
        TargetAddress = startAddress + node.Definition.EncodedSize;

        _queue.Enqueue(node);

        while (_queue.Count > 0)
        {
            var current = _queue.Dequeue();
            VisitNode(current);
        }

        return TargetAddress - startAddress;
    }

    protected abstract void EmitDecisionNode(DecisionNode node, IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> targets);

    protected abstract void EmitDefinitionNode(DefinitionNode node);

    protected abstract void EmitOpcodeTableSwitchNode(OpcodeTableSwitchNode node);

    private void VisitNode(DecoderTreeNode? node)
    {
        switch (node)
        {
            case NonTerminalNode n:
                VisitNonTerminalNode(n);
                break;

            case TerminalNode n:
                VisitTerminalNode(n);
                break;

            default:
                throw new NotSupportedException($"Unsupported node type '{node?.GetType().Name ?? "null"}'.");
        }
    }

    private void VisitNonTerminalNode(NonTerminalNode node)
    {
        var effectiveEntries = node.EnumerateSlots().ToArray();

        // Determine target offsets.

        var nextTargetOffset = 0;
        var targetOffsets = new Dictionary<DecoderTreeNode, int>();

        foreach (var entry in effectiveEntries)
        {
            if (entry is null)
            {
                continue;
            }

            if (!targetOffsets.TryAdd(entry, nextTargetOffset))
            {
                continue;
            }

            nextTargetOffset += entry.Definition.EncodedSize;
        }

        // Emit.

        switch (node)
        {
            case DecisionNode n:
                EmitDecisionNode(n, EnumerateEntries());
                _statistics?.DecisionNodeEmitted(n);
                break;

            default:
                throw new NotSupportedException($"Unsupported node type '{node.GetType().Name}'.");
        }

        CurrentAddress += node.Definition.EncodedSize;
        TargetAddress += nextTargetOffset;

        // Recursively child nodes.

        var visited = new HashSet<DecoderTreeNode>();

        foreach (var entry in effectiveEntries)
        {
            if ((entry is null) || (entry.Definition.EncodedSize is 0))
            {
                // Do not visit leaf nodes.
                continue;
            }

            if (!visited.Add(entry))
            {
                continue;
            }

            _queue.Enqueue(entry);
        }

        return;

        IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> EnumerateEntries()
        {
            var i = 0;
            foreach (var entry in effectiveEntries)
            {
                if (entry is null)
                {
                    yield return (i++, entry, -1, -1);
                    continue;
                }

                var targetAddress = TargetAddress + targetOffsets[entry];
                var offset = targetAddress - CurrentAddress;

                _statistics?.UpdateHighestOffset(offset);

                yield return (i++, entry, targetAddress, offset);
            }
        }
    }

    private void VisitTerminalNode(TerminalNode node)
    {
        switch (node)
        {
            case DefinitionNode n:
                EmitDefinitionNode(n);
                break;

            case OpcodeTableSwitchNode n:
                EmitOpcodeTableSwitchNode(n);
                break;

            default:
                throw new NotSupportedException($"Terminal node of type '{node.GetType().Name}' is not supported.");
        }

        CurrentAddress += node.Definition.EncodedSize;
    }
}

public sealed record DecoderTableEmitterStatistics
{
    private readonly Dictionary<(DecisionNodeDefinition Definition, string Arguments), int> _selectorTables = [];

    public IDictionary<(DecisionNodeDefinition Definition, string Arguments), int> SelectorTables => _selectorTables;
    public int HighestOffset { get; private set; }

    internal void DecisionNodeEmitted(DecisionNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var key = (node.Definition, /* TODO: string.Join(',', node.Arguments.Select(x => x.Data)) */ "");
        var count = _selectorTables.GetValueOrDefault(key, 0);

        _selectorTables[key] = count + 1;
    }

    internal void UpdateHighestOffset(int offset)
    {
        if (offset > HighestOffset)
        {
            HighestOffset = offset;
        }
    }
}

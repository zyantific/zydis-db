using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Zydis.Generator.Core.DecoderTree.Emitters;

/// <summary>
/// Provides an abstract base class for emitting opcode tables.
/// </summary>
/// <remarks>
/// This class defines the core functionality for traversing and processing a <see cref="OpcodeTableNode"/> and all
/// child nodes, handling of inverted values, and managing address calculations during the emission process. Derived
/// classes must implement the abstract methods to handle specific node types and their encoding logic.
/// <para>
///     Emission runs in two passes. The first pass assigns each distinct node object a single address via a
///     deterministic breadth-first traversal, so a subtree shared between multiple parents is laid out (and later
///     emitted) exactly once. The second pass emits the nodes in address order and resolves every edge against that
///     shared address map.
/// </para>
/// </remarks>
public abstract class OpcodeTableEmitter
{
    private const int DefaultMaximumOffset = 0xFFFF;

    private readonly DecoderTableEmitterStatistics? _statistics;
    private readonly int _maximumOffset;

    // Working slots per node, keyed by reference identity. Cloning edits these arrays instead of the source graph so
    // that a subtree shared across tables is never mutated for one table's emission.
    private readonly Dictionary<NonTerminalNode, DecoderTreeNode?[]> _slots = new(ReferenceEqualityComparer.Instance);

    protected int CurrentAddress { get; private set; }
    protected int TargetAddress { get; private set; }

    /// <summary>
    /// The number of subtree clones the range guard produced during the most recent <see cref="Emit(OpcodeTableNode, int)"/>.
    /// </summary>
    internal int CloneCount { get; private set; }

    protected OpcodeTableEmitter(DecoderTableEmitterStatistics? statistics = null) :
        this(DefaultMaximumOffset, statistics)
    {
    }

    internal OpcodeTableEmitter(int maximumOffset, DecoderTableEmitterStatistics? statistics = null)
    {
        _maximumOffset = maximumOffset;
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

        return Run(node, startAddress);
    }

    internal int Emit(DecisionNode node, int startAddress = 0)
    {
        ArgumentNullException.ThrowIfNull(node);

        return Run(node, startAddress);
    }

    protected abstract void EmitDecisionNode(DecisionNode node, IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> targets);

    protected abstract void EmitDefinitionNode(DefinitionNode node);

    protected abstract void EmitOpcodeTableSwitchNode(OpcodeTableSwitchNode node);

    private int Run(NonTerminalNode root, int startAddress)
    {
        _slots.Clear();
        CloneCount = 0;

        // Pass 1: assign addresses, cloning shared subtrees until every edge is encodable.
        Layout layout;
        while (true)
        {
            layout = ComputeLayout(root, startAddress);

            if (!TryFindCloneTarget(layout, out var parent, out var child))
            {
                break;
            }

            ReplaceChild(parent, child, DeepClone(child));
            ++CloneCount;
        }

        // Pass 2: emit nodes in address order, resolving edges against the shared address map.
        TargetAddress = layout.EndAddress;

        foreach (var node in layout.Order)
        {
            CurrentAddress = layout.Address[node];
            EmitNode(node, layout);
        }

        return layout.EndAddress - startAddress;
    }

    private Layout ComputeLayout(NonTerminalNode root, int startAddress)
    {
        var order = new List<DecoderTreeNode>();
        var address = new Dictionary<DecoderTreeNode, int>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<DecoderTreeNode>();

        var next = startAddress;

        address[root] = next;
        next += root.Definition.EncodedSize;
        order.Add(root);
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            if (queue.Dequeue() is not NonTerminalNode current)
            {
                continue;
            }

            foreach (var entry in GetWorkingSlots(current))
            {
                // Zero-size nodes are synthetic and can not be encoded, so they are treated as leaves and never laid
                // out; this mirrors the traversal decisions the single-pass emitter made.
                if ((entry is null) || (entry.Definition.EncodedSize is 0))
                {
                    continue;
                }

                // First visit of a node object reserves its address; later references reuse it. Because addresses are
                // handed out in breadth-first discovery order, a child is always placed after its first parent.
                if (!address.TryAdd(entry, next))
                {
                    continue;
                }

                next += entry.Definition.EncodedSize;
                order.Add(entry);
                queue.Enqueue(entry);
            }
        }

        return new Layout(order, address, next);
    }

    private bool TryFindCloneTarget(Layout layout, out NonTerminalNode parent, out DecoderTreeNode child)
    {
        var parentCount = CountParents(layout);

        foreach (var node in layout.Order)
        {
            if (node is not NonTerminalNode nonTerminal)
            {
                continue;
            }

            var nodeAddress = layout.Address[node];
            var seen = new HashSet<DecoderTreeNode>(ReferenceEqualityComparer.Instance);

            foreach (var entry in GetWorkingSlots(nonTerminal))
            {
                if ((entry is null) || (entry.Definition.EncodedSize is 0) || !seen.Add(entry))
                {
                    continue;
                }

                var offset = layout.Address[entry] - nodeAddress;
                if ((offset >= 1) && (offset <= _maximumOffset))
                {
                    continue;
                }

                // Only sharing can be resolved by cloning: giving one parent a private copy reduces the reference
                // count and eventually lays the copy out forward and in range. A single-parent edge that can not be
                // encoded is a genuine, non-recoverable overflow.
                if (parentCount.GetValueOrDefault(entry) <= 1)
                {
                    throw new InvalidOperationException(
                        $"Un-encodable edge offset {offset} from '{nonTerminal}' to '{entry}' can not be resolved " +
                        $"by cloning (maximum offset is {_maximumOffset}).");
                }

                parent = nonTerminal;
                child = entry;
                return true;
            }
        }

        parent = null!;
        child = null!;
        return false;
    }

    private Dictionary<DecoderTreeNode, int> CountParents(Layout layout)
    {
        var parentCount = new Dictionary<DecoderTreeNode, int>(ReferenceEqualityComparer.Instance);

        foreach (var node in layout.Order)
        {
            if (node is not NonTerminalNode nonTerminal)
            {
                continue;
            }

            var distinct = new HashSet<DecoderTreeNode>(ReferenceEqualityComparer.Instance);

            foreach (var entry in GetWorkingSlots(nonTerminal))
            {
                if ((entry is null) || (entry.Definition.EncodedSize is 0) || !distinct.Add(entry))
                {
                    continue;
                }

                parentCount[entry] = parentCount.GetValueOrDefault(entry) + 1;
            }
        }

        return parentCount;
    }

    private void EmitNode(DecoderTreeNode node, Layout layout)
    {
        switch (node)
        {
            case DecisionNode n:
                EmitDecisionNode(n, EnumerateEntries(n, layout));
                _statistics?.DecisionNodeEmitted(n);
                break;

            case DefinitionNode n:
                EmitDefinitionNode(n);
                break;

            case OpcodeTableSwitchNode n:
                EmitOpcodeTableSwitchNode(n);
                break;

            default:
                throw new NotSupportedException($"Unsupported node type '{node.GetType().Name}'.");
        }
    }

    private IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> EnumerateEntries(
        DecisionNode node, Layout layout)
    {
        var nodeAddress = layout.Address[node];

        var i = 0;
        foreach (var entry in GetWorkingSlots(node))
        {
            if ((entry is null) || !layout.Address.TryGetValue(entry, out var targetAddress))
            {
                yield return (i++, null, -1, -1);
                continue;
            }

            var offset = targetAddress - nodeAddress;

            // Guaranteed by the range guard in pass 1: every retained edge points forward and is encodable.
            Debug.Assert(offset >= 1, "child address must exceed parent address");

            _statistics?.UpdateHighestOffset(offset);

            yield return (i++, entry, targetAddress, offset);
        }
    }

    private DecoderTreeNode?[] GetWorkingSlots(NonTerminalNode node)
    {
        if (!_slots.TryGetValue(node, out var slots))
        {
            slots = node.EnumerateSlots().ToArray();
            _slots[node] = slots;
        }

        return slots;
    }

    private void ReplaceChild(NonTerminalNode parent, DecoderTreeNode oldChild, DecoderTreeNode newChild)
    {
        var slots = GetWorkingSlots(parent);

        for (var i = 0; i < slots.Length; ++i)
        {
            if (ReferenceEquals(slots[i], oldChild))
            {
                slots[i] = newChild;
            }
        }
    }

    private static DecoderTreeNode DeepClone(DecoderTreeNode node)
    {
        switch (node)
        {
            case DefinitionNode n:
                return new DefinitionNode(n.InstructionDefinition);

            case OpcodeTableSwitchNode n:
                return new OpcodeTableSwitchNode(n.Encoding, n.Map, n.Prefix);

            case DecisionNode n:
            {
                var clone = n.Definition.Create();

                foreach (var (index, child) in n.EnumerateVirtualSlots())
                {
                    if (child is not null)
                    {
                        clone[index] = DeepClone(child);
                    }
                }

                if (n.ElseEntry is not null)
                {
                    clone.ElseEntry = DeepClone(n.ElseEntry);
                }

                return clone;
            }

            default:
                throw new NotSupportedException($"Node of type '{node.GetType().Name}' can not be cloned.");
        }
    }

    private sealed record Layout(
        IReadOnlyList<DecoderTreeNode> Order,
        Dictionary<DecoderTreeNode, int> Address,
        int EndAddress);
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Hash-conses <see cref="DecoderTreeNode"/> instances so that structurally identical subtrees anywhere in a table
/// become a single canonical object, and assigns each canonical node a stable id, dense from <c>0</c> in intern
/// order.
/// </summary>
/// <remarks>
/// A node's identity is its kind (<see cref="DecoderTreeNode.Definition"/>, plus arguments where a node type stores
/// any) together with the identities of its children: for a <see cref="DecisionNode"/>, the ids of every virtual
/// slot (<see cref="DecisionNode.EnumerateVirtualSlots"/>) plus <see cref="DecisionNode.ElseEntry"/>, in that order;
/// for a <see cref="DefinitionNode"/>, its <see cref="DefinitionNode.InstructionDefinition"/> reference.
/// <para>
///     None of the currently generated <see cref="DecisionNodeDefinition"/> node types retain their constructor
///     arguments on the instance (<c>Create(string[]? arguments)</c> discards them), so there is nothing to key on
///     beyond the definition singleton for those node types today.
/// </para>
/// <para>
///     Interning requires bottom-up construction: every child of a node must already be interned, and set to the
///     canonical instance <see cref="Intern"/> returned for it, before the node itself is interned. A node must
///     never be mutated once it has been interned; doing so silently invalidates the identity of every parent that
///     already references it, since that identity was derived from the child's state at intern time.
/// </para>
/// </remarks>
internal sealed class NodeInterner
{
    private readonly Dictionary<NodeKey, DecoderTreeNode> _canonicalByKey = [];
    private readonly Dictionary<DecoderTreeNode, Entry> _entriesByNode = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The number of distinct canonical nodes interned so far.
    /// </summary>
    public int Count => _entriesByNode.Count;

    /// <summary>
    /// Interns <paramref name="node"/> and returns the canonical instance for its structural identity.
    /// </summary>
    /// <remarks>
    /// On first sight of a given identity, <paramref name="node"/> becomes canonical and is returned as-is. On a
    /// later sighting of a structurally identical node, the original canonical instance is returned instead and
    /// <paramref name="node"/> is discarded (it is never assigned an id of its own).
    /// </remarks>
    public DecoderTreeNode Intern(DecoderTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var key = BuildKey(node);

        if (_entriesByNode.TryGetValue(node, out var existingEntry))
        {
            // `node` is already canonical for some earlier key; recomputing it now must still match, otherwise the
            // node was mutated after interning, which would silently corrupt every parent that already references it.
            Debug.Assert(existingEntry.Key.Equals(key), "A node was mutated after being interned.");

            return node;
        }

        if (_canonicalByKey.TryGetValue(key, out var canonical))
        {
            return canonical;
        }

        _canonicalByKey[key] = node;
        _entriesByNode[node] = new Entry(_entriesByNode.Count, key);

        return node;
    }

    /// <summary>
    /// Gets the stable id assigned to <paramref name="node"/> when it was interned.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="node"/> was never interned.</exception>
    public int GetId(DecoderTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!_entriesByNode.TryGetValue(node, out var entry))
        {
            throw new InvalidOperationException($"Node '{node}' has not been interned.");
        }

        return entry.Id;
    }

    private NodeKey BuildKey(DecoderTreeNode node)
    {
        return node switch
        {
            DefinitionNode definitionNode => NodeKey.ForDefinition(definitionNode.Definition, definitionNode.InstructionDefinition),
            DecisionNode decisionNode => NodeKey.ForDecision(decisionNode.Definition, BuildChildIds(decisionNode)),
            _ => throw new NotSupportedException($"Node interning does not support node type '{node.GetType()}'.")
        };
    }

    private int[] BuildChildIds(DecisionNode node)
    {
        // EnumerateVirtualSlots() order (regular slots, then negated slots, per the node's definition) is fixed and
        // deterministic; the else entry is appended last. Every referenced child must already be interned (GetId
        // throws otherwise), which is what enforces the bottom-up construction discipline.
        var children = node.EnumerateVirtualSlots().Select(x => x.Value).Append(node.ElseEntry);

        return [.. children.Select(child => child is null ? -1 : GetId(child))];
    }

    private readonly record struct Entry(int Id, NodeKey Key);

    private readonly record struct NodeKey
    {
        private readonly DecoderTreeNodeDefinition _definition;
        private readonly InstructionDefinition? _instructionDefinition;
        private readonly int[]? _childIds;

        private NodeKey(DecoderTreeNodeDefinition definition, InstructionDefinition? instructionDefinition, int[]? childIds)
        {
            _definition = definition;
            _instructionDefinition = instructionDefinition;
            _childIds = childIds;
        }

        public static NodeKey ForDefinition(DecoderTreeNodeDefinition definition, InstructionDefinition instructionDefinition)
        {
            return new NodeKey(definition, instructionDefinition, null);
        }

        public static NodeKey ForDecision(DecoderTreeNodeDefinition definition, int[] childIds)
        {
            return new NodeKey(definition, null, childIds);
        }

        // `_definition` is a per-node-type singleton, compared by reference. `_instructionDefinition` is a record;
        // its auto-generated structural equality is deliberately bypassed here in favor of reference equality, since
        // two distinct instructions that happen to share every field must not be treated as the same identity.
        public bool Equals(NodeKey other)
        {
            if (!ReferenceEquals(_definition, other._definition) || !ReferenceEquals(_instructionDefinition, other._instructionDefinition))
            {
                return false;
            }

            if (_childIds is null || other._childIds is null)
            {
                return (_childIds is null) && (other._childIds is null);
            }

            return _childIds.AsSpan().SequenceEqual(other._childIds);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(RuntimeHelpers.GetHashCode(_definition));
            hash.Add(_instructionDefinition is null ? 0 : RuntimeHelpers.GetHashCode(_instructionDefinition));

            if (_childIds is not null)
            {
                foreach (var id in _childIds)
                {
                    hash.Add(id);
                }
            }

            return hash.ToHashCode();
        }
    }
}

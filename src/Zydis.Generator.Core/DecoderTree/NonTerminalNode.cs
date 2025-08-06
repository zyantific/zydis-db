using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Zydis.Generator.Core.DecoderTree;

public abstract class NonTerminalNodeDefinition :
    DecoderTreeNodeDefinition
{
    /// <summary>
    /// The total number of slots for this non-terminal node type.
    /// <para>
    ///     The minimum number of slots is <c>1</c>.
    /// </para>
    /// </summary>
    public abstract int NumberOfSlots { get; }
}

/// <summary>
/// Represents a non-terminal node in the decoder table tree.
/// </summary>
/// <remarks>
/// Non-terminal nodes are used to represent branches in the tree that can lead to other nodes.
/// <para>
///     Non-terminal nodes have a fixed amount of slots, which may contain child nodes or be empty
///     (<see langword="null"/>).
/// </para>
/// </remarks>
public abstract class NonTerminalNode :
    DecoderTreeNode
{
    /// <inheritdoc cref="DecoderTreeNode.Definition"/>
    public new NonTerminalNodeDefinition Definition => (NonTerminalNodeDefinition)base.Definition;

    /// <inheritdoc/>
    protected NonTerminalNode(NonTerminalNodeDefinition definition) :
        base(definition)
    {
    }

    /// <summary>
    /// Enumerates all slots of the current node.
    /// </summary>
    /// <returns>
    /// An enumeration of <see cref="DecoderTreeNode"/> objects, where each item may be <see langword="null"/> to
    /// represent an empty slot.
    /// </returns>
    [Pure]
    public abstract IEnumerable<DecoderTreeNode?> EnumerateSlots();
}

using System;
using System.Collections.Generic;

namespace Zydis.Generator.Core.DecoderTree;

/// <summary>
/// Represents a decoder table tree node that can not be encoded as a deterministic pattern.
/// <para>
///     This kind of node is only used when building the decoder table from the flat list of instruction definitions.
/// </para>
/// <para>
///     The presence of a <see cref="OverflowNode"/> in the decoder table always indicates an error.
/// </para>
/// </summary>
public sealed class OverflowNode :
    DecoderTreeNode
{
#pragma warning disable CA1034

    public sealed class NodeDefinition :

        DecoderTreeNodeDefinition
    {
        public static NodeDefinition Instance { get; } = new();

        /// <inheritdoc/>
        public override string Name => "overflow";

        /// <inheritdoc/>
        public override int EncodedSize => 0;
    }

#pragma warning restore CA1034

    private readonly List<DecoderTreeNode> _children;

    public IReadOnlyList<DecoderTreeNode> Children => _children;

    public OverflowNode(params IEnumerable<DecoderTreeNode> children) :
        base(NodeDefinition.Instance)
    {
        ArgumentNullException.ThrowIfNull(children);

        _children = [.. children];
    }

    public void Add(DecoderTreeNode node)
    {
        _children.Add(node);
    }

    public void Remove(DecoderTreeNode node)
    {
        _children.Remove(node);
    }

    #region Debugging

    public override string ToString()
    {
        return "???";
    }

    #endregion Debugging
}

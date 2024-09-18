using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

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
    private readonly List<DecoderTreeNode> _children;

    public override int EncodedSize => 0;

    public IReadOnlyList<DecoderTreeNode> Children => _children;

    public OverflowNode(params IEnumerable<DecoderTreeNode> children)
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

    /// <inheritdoc/>
    [Pure]
    public override int CalcEncodedSizeRecursive()
    {
        return 0;
    }

    #region Debugging

    public override string ToString()
    {
        return "???";
    }

    #endregion Debugging
}

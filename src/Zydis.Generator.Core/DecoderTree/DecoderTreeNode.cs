using System;

namespace Zydis.Generator.Core.DecoderTree;

public abstract class DecoderTreeNodeDefinition
{
    /// <summary>
    /// The name of the decoder tree node.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The number of <c>ZydisDecoderTreeNode</c> items that are required to represent this node in its encoded form.
    /// <para>
    ///     The minimum encoded size is <c>1</c> (to be able to encode the node header).
    /// </para>
    /// <para>
    ///     A <see cref="EncodedSize"/> value of <c>0</c> indicates a synthetic, generator specific, node that is
    ///     impossible to encode.
    /// </para>
    /// </summary>
    public abstract int EncodedSize { get; }
}

/// <summary>
/// Represents a node in the decoder table tree.
/// </summary>
public abstract class DecoderTreeNode
{
    /// <summary>
    /// The definition for this node type.
    /// </summary>
    public DecoderTreeNodeDefinition Definition { get; }

    /// <summary>
    /// Constructs a new <see cref="DecoderTreeNode"/>.
    /// </summary>
    protected DecoderTreeNode(DecoderTreeNodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
    }

    // TODO: Implement visitor pattern.
    ///// <summary>
    ///// Calculates the total encoded size of the current node and all its descendants.
    ///// </summary>
    ///// <remarks>
    ///// This method traverses the node hierarchy recursively, summing the encoded sizes of the current node and all its
    ///// child nodes. It is useful for determining the complete size of a tree structure in its encoded form.
    ///// </remarks>
    ///// <returns>The total encoded size of the current node and its descendants.</returns>
    //[Pure]
    //public abstract int CalcEncodedSizeRecursive();

    #region Debugging

    public override string ToString()
    {
        return Definition.Name;
    }

    #endregion Debugging
}

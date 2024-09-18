using System.Diagnostics.Contracts;

namespace Zydis.Generator.Core.DecoderTree;

/// <summary>
/// Represents a node in the decoder table tree.
/// </summary>
/// <remarks>
/// This tree representation is purely used for the code generation process and does not reflect the exact structure
/// of the resulting C decoder tables.
/// </remarks>
public abstract class DecoderTreeNode
{
    /// <summary>
    /// The size (number of items) of the encoded table
    /// <para>
    ///     <c>0</c> indicates a leaf node (instruction definition) or a synthetic node that is impossible to encode.
    /// </para>
    /// </summary>
    public abstract int EncodedSize { get; }

    /// <summary>
    /// Constructs a new <see cref="DecoderTreeNode"/>.
    /// </summary>
    protected DecoderTreeNode()
    {
    }

    /// <summary>
    /// Calculates the total encoded size of the current node and all its descendants.
    /// </summary>
    /// <remarks>
    /// This method traverses the node hierarchy recursively, summing the encoded sizes of the current node and all its
    /// child nodes. It is useful for determining the complete size of a tree structure in its encoded form.
    /// </remarks>
    /// <returns>The total encoded size of the current node and its descendants.</returns>
    [Pure]
    public abstract int CalcEncodedSizeRecursive();
}

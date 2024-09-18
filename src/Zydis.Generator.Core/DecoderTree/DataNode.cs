using System;
using System.Diagnostics.Contracts;

namespace Zydis.Generator.Core.DecoderTree;

/// <summary>
/// Represents a decoder table tree node that contains arbitrary data like e.g. a filter/function argument.
/// </summary>
public sealed class DataNode :
    TerminalNode
{
    public override int EncodedSize => 0;

    /// <summary>
    /// The node data.
    /// </summary>
    public string Data { get; }

    /// <summary>
    /// Constructs a new <see cref="DataNode"/> that wraps the given <paramref name="data"/> string.
    /// </summary>
    /// <param name="data">The data to be wrapped by the node.</param>
    public DataNode(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = data;
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
        return Data;
    }

    #endregion Debugging
}

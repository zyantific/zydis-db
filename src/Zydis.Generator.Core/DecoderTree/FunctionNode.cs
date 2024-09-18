using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Zydis.Generator.Core.DecoderTree;

/// <summary>
/// Represents a decoder table tree node that refers to a runtime function.
/// <para>
///     A runtime function node is used whenever a specific action must be executed before the next node can be
///     evaluated.
///     A simple function could, for example, force the operand-size to a specific value.
/// </para>
/// </summary>
public sealed class FunctionNode :
    NonTerminalNode
{
    // 0 = [15..8] = ARG0, [7..0] = TYPE
    // 1 = ARG1
    // 2 = ARG2
    // ...
    // A = NEXT
    public override int EncodedSize => 1 + (Arguments.Count > 1 ? Arguments.Count - 1 : 0) + ((Next is null) ? 0 : 1);

    /// <summary>
    /// The name of the function.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// A list of arguments to be passed to the function.
    /// </summary>
    public IReadOnlyList<DataNode> Arguments { get; }

    /// <summary>
    /// The <see cref="DecoderTreeNode"/> that is evaluated after executing the function.
    /// </summary>
    public DecoderTreeNode? Next { get; }

    /// <summary>
    /// Constructs a new <see cref="FunctionNode"/>.
    /// </summary>
    /// <param name="functionName">The name of the function</param>
    /// <param name="next">The <see cref="DecoderTreeNode"/> that is evaluated after executing the function.</param>
    /// <param name="arguments">A list of arguments to be passed to the function.</param>
    public FunctionNode(string functionName, IEnumerable<string>? arguments, DecoderTreeNode? next)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionName);

        FunctionName = functionName;
        Next = next;
        Arguments = arguments?.Select(x => new DataNode(x)).ToArray() ?? [];
    }

    /// <inheritdoc/>
    [Pure]
    public override int CalcEncodedSizeRecursive()
    {
        return EncodedSize + Next?.CalcEncodedSizeRecursive() ?? 0;
    }

    #region Debugging

    public override string ToString()
    {
        return $"{FunctionName}({string.Join(", ", Arguments)})";
    }

    #endregion Debugging
}

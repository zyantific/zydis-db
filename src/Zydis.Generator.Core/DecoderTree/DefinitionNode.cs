using System;
using System.Diagnostics.Contracts;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree;

/// <summary>
/// Represents a decoder table tree node that contains an instruction definition.
/// </summary>
public sealed class DefinitionNode :
    TerminalNode
{
    // 0 = [15..8] = PHYSICAL_ENCODING_ID, [7..0] = NODE_TYPE
    // 1 = INSTRUCTION_ID

    /// <inheritdoc/>
    public override int EncodedSize => 2;

    /// <summary>
    /// The <see cref="InstructionDefinition"/> that is represented by this node.
    /// </summary>
    public InstructionDefinition Definition { get; }

    /// <summary>
    /// Constructs a new <see cref="DefinitionNode"/> that wraps the given <paramref name="definition"/>.
    /// </summary>
    /// <param name="definition">The <see cref="InstructionDefinition"/> to be wrapped by the new node.</param>
    public DefinitionNode(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
    }

    public static implicit operator DefinitionNode(InstructionDefinition definition) => FromInstructionDefinition(definition);

    public static DefinitionNode FromInstructionDefinition(InstructionDefinition definition)
    {
        return new(definition);
    }

    /// <inheritdoc/>
    [Pure]
    public override int CalcEncodedSizeRecursive()
    {
        return EncodedSize;
    }

    #region Debugging

    public override string ToString()
    {
        return Definition.Mnemonic;
    }

    #endregion Debugging
}

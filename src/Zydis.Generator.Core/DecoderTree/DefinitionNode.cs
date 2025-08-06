using System;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree;

public sealed partial class DefinitionNode
{
#pragma warning disable CA1034

    public sealed partial class NodeDefinition
    {
        // 0 = [15..8] = PHYSICAL_ENCODING_ID, [7..0] = NODE_TYPE
        // 1 = INSTRUCTION_ID

        /// <inheritdoc/>
        public override int EncodedSize => 2;
    }

#pragma warning restore CA1034

    /// <summary>
    /// The <see cref="InstructionDefinition"/> that is represented by this node.
    /// </summary>
    public InstructionDefinition InstructionDefinition { get; }

    /// <summary>
    /// Constructs a new <see cref="DefinitionNode"/> that wraps the given <paramref name="definition"/>.
    /// </summary>
    /// <param name="definition">The <see cref="InstructionDefinition"/> to be wrapped by the new node.</param>
    public DefinitionNode(InstructionDefinition definition) :
        base(NodeDefinition.Instance)
    {
        ArgumentNullException.ThrowIfNull(definition);

        InstructionDefinition = definition;
    }

    public static implicit operator DefinitionNode(InstructionDefinition definition) => FromInstructionDefinition(definition);

    public static DefinitionNode FromInstructionDefinition(InstructionDefinition definition)
    {
        return new(definition);
    }

    #region Debugging

    public override string ToString()
    {
        return InstructionDefinition.Mnemonic;
    }

    #endregion Debugging
}

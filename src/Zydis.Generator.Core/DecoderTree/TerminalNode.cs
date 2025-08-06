namespace Zydis.Generator.Core.DecoderTree;

public abstract class TerminalNodeDefinition :
    DecoderTreeNodeDefinition
{
}

/// <summary>
/// Represents a terminal node in the decoder table tree.
/// </summary>
/// <remarks>
/// A terminal node is a leaf node in the decoder tree, meaning it does not have any child nodes. This class serves as
/// a base for specific types of terminal nodes.
/// </remarks>
public abstract class TerminalNode :
    DecoderTreeNode
{
    public new TerminalNodeDefinition Definition => (TerminalNodeDefinition)base.Definition;

    protected TerminalNode(TerminalNodeDefinition definition) :
        base(definition)
    {
    }
}

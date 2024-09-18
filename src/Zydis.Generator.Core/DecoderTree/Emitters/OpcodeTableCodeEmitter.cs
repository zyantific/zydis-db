using System;
using System.Collections.Generic;
using System.Diagnostics;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.DecoderTree.Emitters;

internal sealed class DecoderTableCodeEmitter :
    OpcodeTableEmitter
{
    //private const string NodeEntryInvalid = "ZYDIS_DT_INVALID";
    //private const string NodeEntryHeader = "ZYDIS_DT_HEADER";
    //private const string NodeEntryOffset = "ZYDIS_DT_OFFSET";
    //private const string NodeEntryDefinition = "ZYDIS_DT_DEFINITION";

    private const int Padding = -12;

    private readonly InitializerListWriter _writer;
    private readonly DefinitionRegistry _definitionRegistry;
    private readonly EncodingRegistry _encodingRegistry;

    public DecoderTableCodeEmitter(
        InitializerListWriter writer,
        DefinitionRegistry definitionRegistry,
        EncodingRegistry encodingRegistry,
        DecoderTableEmitterStatistics? statistics = null) :
        base(statistics)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(definitionRegistry);
        ArgumentNullException.ThrowIfNull(encodingRegistry);

        _writer = writer;
        _definitionRegistry = definitionRegistry;
        _encodingRegistry = encodingRegistry;
    }

    protected override void EmitSelectorNode(SelectorNode node, IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> targets)
    {
        // Emit header and first argument (if applicable).

        var type = node.Definition.Name.ToUpperInvariant();
        type = type.Replace("FEATURE_", "MODE_"); // TODO: Use `mode` in JSON file.

        var arg0 = (node.Arguments.Count > 0)
            ? $"/* {node.Definition.Parameters[0],Padding} */, {node.Arguments[0].Data}"
            : "0";

        _writer.WriteInlineComment($"{"HEADER",Padding}");
        _writer.WriteExpression("ZYDIS_DT_HEADER({0}, {1})", "ZYDIS_NODETYPE_" + type, arg0);

        // Emit additional arguments.

        // TYPE and ARG0 is encoded in the same position

        for (var i = 1; i < node.Arguments.Count; ++i)
        {
            var parameter = node.Definition.Parameters[i];
            var argument = node.Arguments[i];

            _writer.WriteInlineComment($"{parameter,Padding}");
            _writer.WriteExpression(argument.Data);
        }

        // Emit selector targets.

        foreach (var (index, targetNode, _, offsetToTarget) in targets)
        {
            _writer.WriteInlineComment($"{node.Definition.Slots[index],Padding}");

            if (targetNode is null)
            {
                _writer.WriteExpression("ZYDIS_DT_INVALID");
                continue;
            }

            if (targetNode is DataNode dn)
            {
                _writer.WriteExpression(dn.Data);
                continue;
            }

            //var comment = targetNode switch
            //{
            //    SelectorNode n => n.Definition.Name,
            //    FunctionNode n => n.FunctionName,
            //    DefinitionNode n => n.Definition.Mnemonic,
            //    _ => throw new UnreachableException()
            //};

            var comment = targetNode.ToString();

            _writer.WriteExpression("ZYDIS_DT_OFFSET(0x{0:X4}) /* {1} */", offsetToTarget, comment);
        }
    }

    protected override void EmitFunctionNode(FunctionNode node, DecoderTreeNode? targetNode, int targetAddress, int offsetToTarget)
    {
        throw new NotImplementedException();
    }

    protected override void EmitDefinitionNode(DefinitionNode node)
    {
        // Emit header.

        var encodingId = _encodingRegistry.GetEncodingId(node.Definition);

        _writer.WriteInlineComment($"{"HEADER",Padding}");
        _writer.WriteExpression("ZYDIS_DT_DEFINITION_HEADER(0x{0:X2})", encodingId);

        // Emit definition id.

        _writer.WriteInlineComment($"{"ID",Padding}");
        _writer.WriteExpression("ZYDIS_DT_DEFINITION(0x{0:X4}) /* {1} */",
            _definitionRegistry.GetDefinitionId(node.Definition),
            node.Definition.Mnemonic);
    }

    protected override void EmitSelectorOpcodeTableNode(SelectOpcodeTableNode node)
    {
        // Emit header.

        _writer.WriteInlineComment($"{"HEADER",Padding}");
        _writer.WriteExpression("ZYDIS_DT_SWITCH_TABLE_HEADER(0x{0:X2})", node.OpcodeTableId);
    }
}

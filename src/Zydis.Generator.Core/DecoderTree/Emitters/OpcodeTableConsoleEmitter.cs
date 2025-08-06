using System;
using System.Collections.Generic;

using Spectre.Console;

using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.DecoderTree.Emitters;

internal sealed class OpcodeTableConsoleEmitter :
    OpcodeTableEmitter
{
    private readonly DefinitionRegistry _definitionRegistry;
    private readonly EncodingRegistry _encodingRegistry;

    public bool SkipEmpty { get; set; }

    public OpcodeTableConsoleEmitter(DefinitionRegistry definitionRegistry, EncodingRegistry encodingRegistry, DecoderTableEmitterStatistics? statistics = null) :
        base(statistics)
    {
        ArgumentNullException.ThrowIfNull(definitionRegistry);
        ArgumentNullException.ThrowIfNull(encodingRegistry);

        _definitionRegistry = definitionRegistry;
        _encodingRegistry = encodingRegistry;
    }

    protected override void EmitDecisionNode(DecisionNode node, IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> targets)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(targets);

        PrintSymbol(node);

        // Print Header

        PrintOffsetAndIndex(0);
        AnsiConsole.MarkupLine($"TYPE = [{ColorConstants.ColorDecision}]{node.Definition.Name}[/] ");

        // TODO:
        //// Print Arguments

        //var startIndex = (node.Arguments.Count == 0) ? 1 : 0; // TYPE and ARG0 is encoded in the same position
        //for (var i = 0; i < node.Arguments.Count; ++i)
        //{
        //    var parameter = node.Definition.Parameters[i];
        //    var argument = node.Arguments[i];

        //    PrintOffsetAndIndex(i);
        //    AnsiConsole.MarkupLine($"[{GetNodeColor(argument)}]{parameter} = {GetNodeText(argument)}[/]");

        //    ++startIndex;
        //}

        var startIndex = 0;

        foreach (var (index, targetNode, targetAddress, offsetToTarget) in targets)
        {
            if (targetNode is null && SkipEmpty)
            {
                continue;
            }

            PrintOffsetAndIndex(startIndex + index);
            AnsiConsole.Markup($"[{ColorConstants.ColorEmpty}]/* {node.Definition.GetSlotName(index),6} */[/] ");

            if (targetNode is null)
            {
                AnsiConsole.MarkupLine($"[{GetNodeColor(targetNode)}]{GetNodeText(targetNode)}[/]");
                continue;
            }

            AnsiConsole.MarkupLine(
                $"goto [{ColorConstants.ColorOffset}][[{targetAddress:X4}]][/] " +
                $"([{ColorConstants.ColorOffsetRelative}]+{offsetToTarget:X4}[/]) " +
                $"[{ColorConstants.ColorEmpty}]// -> [{GetNodeColor(targetNode)}]{targetAddress:X4}_{GetNodeText(targetNode)}[/][/]");
        }
    }

    protected override void EmitDefinitionNode(DefinitionNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var definition = node.InstructionDefinition;

        PrintSymbol(node);

        // Print Header

        PrintOffsetAndIndex(0);
        AnsiConsole.MarkupLine($"TYPE = [{ColorConstants.ColorDefinition}]DEFINITION[/] ");

        PrintOffsetAndIndex(0);
        AnsiConsole.MarkupLine($"[{ColorConstants.ColorData}]ENCODING_ID = {_encodingRegistry.GetEncodingId(definition):X2}[/]");

        PrintOffsetAndIndex(1);
        AnsiConsole.MarkupLine($"[{ColorConstants.ColorData}]INSTRUCTION_ID = {_definitionRegistry.GetDefinitionId(definition):X4}[/] [{ColorConstants.ColorEmpty}]/* {definition.Mnemonic} */[/]");
    }

    protected override void EmitOpcodeTableSwitchNode(OpcodeTableSwitchNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        PrintSymbol(node);

        // Print Header

        PrintOffsetAndIndex(0);
        AnsiConsole.MarkupLine($"TYPE = [{ColorConstants.ColorDefinition}]SWITCH_TABLE[/] ");

        PrintOffsetAndIndex(0);
        AnsiConsole.MarkupLine($"[{ColorConstants.ColorData}]OPCODE_TABLE_ID = {node.OpcodeTableId:X2}[/] [{ColorConstants.ColorEmpty}]/* {node} */[/]");
    }

    private void PrintOffsetAndIndex(int index)
    {
        AnsiConsole.Markup($"[{ColorConstants.ColorOffset}][[{(CurrentAddress + index):X4}]][/] [[{index,3:X2}]] ");
    }

    private void PrintSymbol(DecoderTreeNode node)
    {
        AnsiConsole.MarkupLine($"[{GetNodeColor(node)}]@{CurrentAddress:X4}_{GetNodeText(node)}:[/]");
    }

    private static string GetNodeText(DecoderTreeNode? node)
    {
        if (node is null)
        {
            return "<empty>";
        }

        return node
            .ToString()!
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }

    private static string GetNodeColor(DecoderTreeNode? node)
    {
        return node switch
        {
            null => ColorConstants.ColorEmpty,
            DefinitionNode => ColorConstants.ColorDefinition,
            DecisionNode => ColorConstants.ColorDecision,
            //FunctionNode => ColorConstants.ColorFunction,
            OverflowNode => ColorConstants.ColorOverflow,
            _ => throw new NotSupportedException()
        };
    }
}

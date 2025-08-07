using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Extensions;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class EncoderDefinitionRegistry
{
    private readonly SortedDictionary<string, List<EncodableDefinition>> _instructions = new();

    private readonly Dictionary<string, int> _swappableTracker = new();

    public List<EncodableDefinition> Definitions { get; } = new();

    private static int GetModrmRegOperandPosition(EncodableDefinition definition) =>
        definition.Instruction.Operands?.ToList().FindIndex(x => x.Encoding == OperandEncoding.ModrmReg) ?? -1;

    private void MarkSwappable(EncodableDefinition definition1, EncodableDefinition definition2)
    {
        if (GetModrmRegOperandPosition(definition1) > GetModrmRegOperandPosition(definition2))
        {
            (definition1, definition2) = (definition2, definition1);
        }
        var index = _swappableTracker.GetOrCreate(definition1.Instruction.Mnemonic, 1);
        definition1.SwappableIndex = index;
        definition2.SwappableIndex = index + 1;
        _swappableTracker[definition1.Instruction.Mnemonic] += 2;
    }

    private static bool CheckSwappableOperands(IReadOnlyList<InstructionOperand>? operands1, IReadOnlyList<InstructionOperand>? operands2)
    {
        if (operands1 == null ||
            operands2 == null ||
            operands1.Count != operands2.Count)
        {
            return false;
        }
        var modrmOperandCount = 0;
        foreach (var (op1, op2) in operands1.Zip(operands2))
        {
            if (op1.Encoding is OperandEncoding.ModrmRm or OperandEncoding.ModrmReg &&
                op2.Encoding is OperandEncoding.ModrmRm or OperandEncoding.ModrmReg)
            {
                var operandPartialEquality =
                   (op1.Type == op2.Type) &&
                   (op1.Access == op2.Access) &&
                   (op1.ElementType == op2.ElementType) &&
                   (op1.ScaleFactor == op2.ScaleFactor) &&
                   (op1.Width16 == op2.Width16) &&
                   (op1.Width32 == op2.Width32) &&
                   (op1.Width64 == op2.Width64) &&
                   (op1.IsVisible == op2.IsVisible) &&
                   (op1.IsMultiSource4 == op2.IsMultiSource4) &&
                   (op1.IgnoreSegmentOverride == op2.IgnoreSegmentOverride) &&
                   (op1.Register == op2.Register) &&
                   (op1.MemorySegment == op2.MemorySegment) &&
                   (op1.MemoryBase == op2.MemoryBase);
                if (!operandPartialEquality)
                {
                    return false;
                }
                ++modrmOperandCount;
            }
            else if (!op1.Equals(op2))
            {
                return false;
            }
        }
        return modrmOperandCount == 2;
    }

    private void FindSwappable(List<EncodableDefinition> definitions, EncodableDefinition newDefinition)
    {
        if (newDefinition.Instruction.Encoding != InstructionEncoding.VEX)
        {
            return;
        }
        foreach (var definition in definitions)
        {
            var selectors1 = definition.GetSelectors(SelectorDefinitions.MandatoryPrefix.Name);
            var selectors2 = newDefinition.GetSelectors(SelectorDefinitions.MandatoryPrefix.Name);
            if (!selectors1.Compare(selectors2) ||
                definition.Instruction.OpcodeMap != newDefinition.Instruction.OpcodeMap)
            {
                continue;
            }
            if (definition.Instruction.Encoding == InstructionEncoding.VEX &&
                CheckSwappableOperands(definition.Instruction.Operands, newDefinition.Instruction.Operands))
            {
                MarkSwappable(definition, newDefinition);
                return;
            }
        }
    }

    public void InsertDefinition(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var encodableDefinition = new EncodableDefinition(definition);
        if (!encodableDefinition.IsEncodable)
        {
            return;
        }
        if (_instructions.TryGetValue(definition.Mnemonic, out var candidates))
        {
            foreach (var candidateDefinition in candidates)
            {
                if (candidateDefinition.Merge(encodableDefinition))
                {
                    return;
                }
            }
            FindSwappable(candidates, encodableDefinition);
            candidates.Add(encodableDefinition);
        }
        else
        {
            _instructions[definition.Mnemonic] = [encodableDefinition];
        }

        Definitions.Add(encodableDefinition);
    }

    private void ProcessAnnotatedSwappables()
    {
        for (var i = 0; i < Definitions.Count; ++i)
        {
            var definition = Definitions[i];
            var swappableAnnotation = definition.Instruction.GetAnnotation<AnnotationSwappable>();
            if (swappableAnnotation == null)
            {
                continue;
            }
            definition.SwappableIndex = 1;
            var nextDefinitionName = i + 1 < Definitions.Count ? Definitions[i + 1].Instruction.GetAnnotation<AnnotationName>()?.Name ?? null : null;
            if (swappableAnnotation.Next != nextDefinitionName)
            {
                throw new InvalidDataException($"Swappable definition constraint check has failed. Next definition is not: {swappableAnnotation.Next}");
            }
        }
    }

    public void Optimize()
    {
        var stableSorted = Definitions.OrderBy(x => x).ToList();
        Definitions.Clear();
        Definitions.AddRange(stableSorted);
        ProcessAnnotatedSwappables();
    }
}

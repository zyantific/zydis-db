using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class OperandsRegistry
{
    private readonly List<InstructionOperand> _operands = new();
    private readonly ConditionalWeakTable<InstructionDefinition, PhysicalInstructionEncoding> _lut = new();

    public IReadOnlyList<InstructionOperand> Operands => _operands;

    public void Initialize(IEnumerable<InstructionDefinition> definitions)
    {
        foreach (var definition in definitions.OrderByDescending(x => x.NumberOfOperands))
        {
            if (definition.NumberOfOperands is 0)
            {
                continue;
            }

            InsertOperands(definition.AllOperands.ToList());
        }
    }

    public int GetOperandsIndex(InstructionDefinition definition)
    {
        if (definition.NumberOfOperands is 0)
        {
            return (-1) & 0x7FFF;
        }

        return FindOperandsIndex(definition.AllOperands.ToList());
    }

    private void InsertOperands(IReadOnlyList<InstructionOperand> operands)
    {
        Debug.Assert(operands.Count is not 0);

        var id = FindOperandsIndex(operands);
        if (id >= 0)
        {
            // Operands already exist, no need to insert again.
            return;
        }

        _operands.AddRange(operands);
    }

    private int FindOperandsIndex(IReadOnlyList<InstructionOperand> operands)
    {
        var index = -1;
        while (true)
        {
            index = _operands.IndexOf(operands[0], index + 1);
            if (index < 0)
            {
                break;
            }

            if (operands.SequenceEqual(_operands.Skip(index).Take(operands.Count)))
            {
                var test = _operands[index];
                return index;
            }
        }

        return -1;
    }
}

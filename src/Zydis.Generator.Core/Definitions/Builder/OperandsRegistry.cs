using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Zydis.Generator.Core.Helpers;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class OperandsRegistry
{
    private readonly List<InstructionOperand> _operands = new();

    private readonly SortedSet<SizeTable> _sizes = new();

    private readonly SortedSet<OperandDetails> _details = new();

    public IReadOnlyList<InstructionOperand> Operands => _operands;

    public IReadOnlySet<SizeTable> Sizes => _sizes;

    public IReadOnlySet<OperandDetails> OperandsDetails => _details;

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
        foreach (var operand in operands)
        {
            _sizes.Add(GetSizeTable(operand));
            _details.Add(GetDetails(operand));
        }
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
                return index;
            }
        }

        return -1;
    }

    public static SizeTable GetSizeTable(InstructionOperand operand)
    {
        return new SizeTable(operand.Width16, operand.Width32, operand.Width64);
    }

    public static OperandDetails GetDetails(InstructionOperand operand)
    {
        var type = "OTHER";
        if (operand.Type is OperandType.ImplicitReg)
        {
            type = operand.Register.GetRegisterClass() switch
            {
                RegisterClass.GPROSZ => "GPR_OSZ",
                RegisterClass.GPRASZ => "GPR_ASZ",
                RegisterClass.GPRSSZ => "GPR_SSZ",
                _ => "STATIC"
            };
            type = operand.Register switch
            {
                Register.ASZIP => "IP_ASZ",
                Register.SSZIP => "IP_SSZ",
                Register.SSZFLAGS => "FLAGS_SSZ",
                _ => type
            };
        }
        else if (operand.Type is OperandType.ImplicitMem)
        {
            type = "MEMORY";
        }

        return new OperandDetails(type, operand.Encoding, operand.Register, operand.MemorySegment, operand.MemoryBase);
    }
}

#pragma warning disable CA1036

public sealed record OperandDetails(string Type, OperandEncoding Encoding, Register Register, SegmentRegister? MemorySegment = null, BaseRegister? MemoryBase = null) :
    IComparable<OperandDetails>,
    IComparable

#pragma warning restore CA1036
{
    public int CompareTo(OperandDetails? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Type),
            x => x.Compare(x => x.Encoding),
            x => x.Compare(x => x.Register),
            x => x.Compare(x => x.MemorySegment),
            x => x.Compare(x => x.MemoryBase)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as OperandDetails);
    }
}

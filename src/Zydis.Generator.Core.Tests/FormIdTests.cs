using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Tests;

public sealed class FormIdTests
{
    [Fact]
    public async Task AssignIds_RealDatabase_ProducesUniqueIdForEveryDefinition()
    {
        var path = Path.Combine(TestPaths.RepoRoot, "datafiles", "instructions.json");
        var definitions = new List<InstructionDefinition>();
        await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(path))
        {
            definitions.Add(definition);
        }

        var ids = FormId.AssignIds(definitions);

        Assert.Equal(definitions.Count, ids.Count);
        Assert.Equal(definitions.Count, ids.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(OperandTokenCases))]
    public void GetOperandToken_ReturnsExpectedToken(InstructionOperand operand, string expected)
    {
        Assert.Equal(expected, FormId.GetOperandToken(operand));
    }

    public static TheoryData<InstructionOperand, string> OperandTokenCases()
    {
        return new TheoryData<InstructionOperand, string>
        {
            { new InstructionOperand { Type = OperandType.GPR8 }, "r8" },
            { new InstructionOperand { Type = OperandType.GPR16 }, "r16" },
            { new InstructionOperand { Type = OperandType.GPR32 }, "r32" },
            { new InstructionOperand { Type = OperandType.GPR64 }, "r64" },
            { new InstructionOperand { Type = OperandType.GPR16_32_64 }, "rv" },
            { new InstructionOperand { Type = OperandType.GPR32_32_64 }, "ry" },
            { new InstructionOperand { Type = OperandType.GPR16_32_32 }, "rz" },
            { new InstructionOperand { Type = OperandType.XMM }, "xmm" },
            { new InstructionOperand { Type = OperandType.YMM }, "ymm" },
            { new InstructionOperand { Type = OperandType.ZMM }, "zmm" },
            { new InstructionOperand { Type = OperandType.MMX }, "mm" },
            { new InstructionOperand { Type = OperandType.MASK }, "k" },
            { new InstructionOperand { Type = OperandType.BND }, "bnd" },
            { new InstructionOperand { Type = OperandType.SREG }, "sreg" },
            { new InstructionOperand { Type = OperandType.CR }, "cr" },
            { new InstructionOperand { Type = OperandType.DR }, "dr" },
            { new InstructionOperand { Type = OperandType.TMM }, "tmm" },
            { new InstructionOperand { Type = OperandType.FPR }, "st" },
            { new InstructionOperand { Type = OperandType.MEM, Width16 = 32, Width32 = 32, Width64 = 32 }, "m32" },
            { new InstructionOperand { Type = OperandType.MEM, Width16 = 16, Width32 = 32, Width64 = 64 }, "mv" },
            { new InstructionOperand { Type = OperandType.MEMVSIBX }, "vsibx" },
            { new InstructionOperand { Type = OperandType.MEMVSIBY }, "vsiby" },
            { new InstructionOperand { Type = OperandType.MEMVSIBZ }, "vsibz" },
            { new InstructionOperand { Type = OperandType.IMM, Encoding = OperandEncoding.Uimm8 }, "imm8" },
            { new InstructionOperand { Type = OperandType.IMM, Encoding = OperandEncoding.Simm32 }, "imm32" },
            { new InstructionOperand { Type = OperandType.IMM, Encoding = OperandEncoding.Uimm16_32_64 }, "immv" },
            { new InstructionOperand { Type = OperandType.REL, Encoding = OperandEncoding.Jimm8 }, "rel8" },
            { new InstructionOperand { Type = OperandType.REL, Encoding = OperandEncoding.Jimm32 }, "rel32" },
            { new InstructionOperand { Type = OperandType.ABS }, "abs" },
            { new InstructionOperand { Type = OperandType.PTR }, "ptr" },
            { new InstructionOperand { Type = OperandType.AGEN }, "agen" },
            { new InstructionOperand { Type = OperandType.AGENNoRel }, "agen" },
            { new InstructionOperand { Type = OperandType.MOFFS }, "moffs" },
            { new InstructionOperand { Type = OperandType.MIB }, "mib" },
            { new InstructionOperand { Type = OperandType.DFV }, "dfv" },
            { new InstructionOperand { Type = OperandType.ImplicitReg, Register = Register.AL, IsVisible = false }, "al" },
            { new InstructionOperand { Type = OperandType.ImplicitMem, MemoryBase = BaseRegister.SSP, IsVisible = false }, "mem_ssp" },
            { new InstructionOperand { Type = OperandType.ImplicitImm1, IsVisible = false }, "implicitimm1" }
        };
    }

    [Fact]
    public void AssignIds_MandatoryPrefixAbsentVsExplicitNone_ProducesDifferentIds()
    {
        // Historically, treating explicit "none" as droppable (like a real off-value) collapsed it
        // into "absent" and silently reintroduced a collision; this pins the fix.
        var absent = Definition("test1", pattern: null);
        var explicitNone = Definition("test1", pattern: Pattern(("mandatory_prefix", "none")));

        var ids = FormId.AssignIds([absent, explicitNone]);

        Assert.NotEqual(ids[absent], ids[explicitNone]);
    }

    [Fact]
    public void AssignIds_UnknownFilterKey_Throws()
    {
        var definition = Definition("test1", pattern: Pattern(("totally_bogus_key", "1")));

        Assert.Throws<NotSupportedException>(() => FormId.AssignIds([definition]));
    }

    [Fact]
    public void AssignIds_DefinitionsDifferingOnlyInHiddenOperand_ShareBaseKeyButGetDistinctIds()
    {
        var operandsA = new[]
        {
            new InstructionOperand { Type = OperandType.ImplicitReg, Register = Register.AL, IsVisible = false },
            new InstructionOperand { Type = OperandType.ImplicitReg, Register = Register.AH, IsVisible = false }
        };
        var operandsB = new[]
        {
            new InstructionOperand { Type = OperandType.ImplicitReg, Register = Register.AL, IsVisible = false },
            new InstructionOperand { Type = OperandType.ImplicitReg, Register = Register.DL, IsVisible = false }
        };

        var definitionA = Definition("aaa", operands: operandsA, pattern: Pattern(("mode", "!64")));
        var definitionB = Definition("aaa", operands: operandsB, pattern: Pattern(("mode", "64")));

        Assert.Equal(FormId.GetBaseKey(definitionA), FormId.GetBaseKey(definitionB));

        var ids = FormId.AssignIds([definitionA, definitionB]);

        Assert.NotEqual(ids[definitionA], ids[definitionB]);
    }

    private static InstructionDefinition Definition(
        string mnemonic, byte opcode = 0, InstructionEncoding encoding = InstructionEncoding.Default,
        OpcodeMap opcodeMap = OpcodeMap.MAP0, IReadOnlyList<InstructionOperand>? operands = null,
        IReadOnlyList<FilterEntry>? pattern = null)
    {
        return new InstructionDefinition
        {
            Mnemonic = mnemonic,
            Opcode = opcode,
            Encoding = encoding,
            OpcodeMap = opcodeMap,
            Operands = operands,
            Pattern = pattern,
            MetaInfo = new InstructionMetaInfo()
        };
    }

    private static IReadOnlyList<FilterEntry> Pattern(params (string Filter, string Value)[] filters)
    {
        return filters.Select(f => new FilterEntry(f.Filter, f.Value)).ToList();
    }
}

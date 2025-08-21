using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Enums;

using static Zydis.Generator.Core.CodeGeneration.ObjectDeclaration;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class OperandsEmitter
{
    public static async Task EmitAsync(StreamWriter writer, OperandsRegistry operandsRegistry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(operandsRegistry);

        await writer.WriteLineAsync("#ifndef ZYDIS_MINIMAL_MODE").ConfigureAwait(false);

        var declarationWriter = DeclarationWriter.Create(writer);
        var operandsWriter = declarationWriter
            .BeginDeclaration("static const", "ZydisOperandDefinition", "OPERAND_DEFINITIONS[]")
            .WriteInitializerList()
            .BeginList();
        var operandDeclaration = new ObjectDeclaration<InstructionOperand>();
        var opDeclaration = new SimpleObjectDeclaration(InitializerType.Designated, "encoding", "reg", "mem");
        var regOuterDeclaration = new SimpleObjectDeclaration("type", "reg");
        var regInnerDeclaration = new SimpleObjectDeclaration(InitializerType.Designated, "reg", "id");
        var memDeclaration = new SimpleObjectDeclaration("seg", "base");

        foreach (var operand in operandsRegistry.Operands)
        {
            var operandEntry = operandsWriter.CreateObjectWriter(operandDeclaration);
            var opEntry = operandEntry.CreateObjectWriter(opDeclaration);
            if (operand.Type is OperandType.ImplicitReg)
            {
                var regOuterEntry = opEntry.CreateObjectWriter(regOuterDeclaration);
                var regInnerEntry = regOuterEntry.CreateObjectWriter(regInnerDeclaration);
                var type = operand.Register.GetRegisterClass() switch
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

                if (type == "STATIC")
                {
                    regInnerEntry.WriteExpression("reg", "ZYDIS_REGISTER_{0}", operand.Register.ToZydisString());
                    regOuterEntry.WriteExpression("type", "ZYDIS_IMPLREG_TYPE_STATIC");

                }
                else
                {
                    regInnerEntry.WriteInteger("id", operand.Register.GetRegisterId() & 0x3F, 4, true);
                    regOuterEntry.WriteExpression("type", "ZYDIS_IMPLREG_TYPE_{0}", type);
                }

                regOuterEntry.WriteObject("reg", regInnerEntry);
                opEntry.WriteObject("reg", regOuterEntry);
            }
            else if (operand.Type is OperandType.ImplicitMem)
            {
                var memEntry = opEntry.CreateObjectWriter(memDeclaration);
                memEntry
                    .WriteInteger("seg", (int)(operand.MemorySegment ?? SegmentRegister.None))
                    .WriteExpression("base", operand.MemoryBase!.Value.ToZydisString());
                opEntry.WriteObject("mem", memEntry);
            }
            else
            {
                opEntry.WriteExpression("encoding", "ZYDIS_OPERAND_ENCODING_{0}", operand.Encoding.ToZydisString());
            }
            operandEntry
                .WriteExpression("type", "ZYDIS_SEMANTIC_OPTYPE_{0}", operand.Type.ToZydisString())
                .WriteExpression("visibility", "ZYDIS_OPERAND_VISIBILITY_{0}", operand.Visibility.ToZydisString())
                .WriteExpression("actions", "ZYDIS_OPERAND_ACTION_{0}", operand.Access.ToZydisString())
                .WriteIntegerArray("size", operand.Width16, operand.Width32, operand.Width64)
                .WriteExpression("element_type", operand.ElementType.ToZydisString())
                .WriteObject("op", opEntry)
                .WriteBool("is_multisource4", operand.IsMultiSource4)
                .WriteBool("ignore_seg_override", operand.IgnoreSegmentOverride);

            operandsWriter.WriteObject(operandEntry);
        }

        operandsWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("#endif").ConfigureAwait(false);
    }
}

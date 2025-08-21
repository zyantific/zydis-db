using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class OperandsEmitter
{
    public static async Task EmitAsync(StreamWriter writer, OperandsRegistry operandsRegistry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(operandsRegistry);

        await writer.WriteLineAsync("#ifndef ZYDIS_MINIMAL_MODE").ConfigureAwait(false);

        var declarationWriter = DeclarationWriter.Create(writer)
            .BeginDeclaration("static const", "ZydisOperandDefinition", "OPERAND_DEFINITIONS[]");

        var operandsWriter = declarationWriter.WriteInitializerList()
            .BeginList();

        //var i = 0;

        foreach (var operand in operandsRegistry.Operands)
        {
            var initializerListWriter = operandsWriter
                //.WriteInlineComment("{0:X4}", i++)
                .WriteInitializerList(indent: Debugger.IsAttached)
                .BeginList();

            initializerListWriter
                .WriteFieldDesignation("type").WriteExpression("ZYDIS_SEMANTIC_OPTYPE_{0}", operand.Type.ToZydisString())
                .WriteFieldDesignation("visibility").WriteExpression("ZYDIS_OPERAND_VISIBILITY_{0}", operand.Visibility.ToZydisString())
                .WriteFieldDesignation("actions").WriteExpression("ZYDIS_OPERAND_ACTION_{0}", operand.Access.ToZydisString());

            initializerListWriter
                .WriteFieldDesignation("size").WriteInitializerList()
                .BeginList()
                .WriteInteger(operand.Width16)
                .WriteInteger(operand.Width32)
                .WriteInteger(operand.Width64)
                .EndList();

            initializerListWriter
                .WriteFieldDesignation("element_type").WriteExpression(operand.ElementType.ToZydisString());

            var op = initializerListWriter
                .WriteFieldDesignation("op").WriteInitializerList()
                .BeginList();

            if (operand.Type is OperandType.ImplicitReg)
            {
                var reg = op.WriteFieldDesignation("reg").WriteInitializerList()
                    .BeginList();

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

                if (type is "STATIC")
                {
                    var regreg = reg
                        .WriteFieldDesignation("type").WriteExpression("ZYDIS_IMPLREG_TYPE_STATIC")
                        .WriteFieldDesignation("reg").WriteInitializerList()
                        .BeginList();
                    regreg.WriteFieldDesignation("reg").WriteExpression("ZYDIS_REGISTER_{0}", operand.Register.ToZydisString());
                    regreg.EndList();
                }
                else
                {
                    var regreg = reg
                        .WriteFieldDesignation("type").WriteExpression("ZYDIS_IMPLREG_TYPE_{0}", type)
                        .WriteFieldDesignation("reg").WriteInitializerList()
                        .BeginList();
                    regreg.WriteFieldDesignation("id").WriteInteger(operand.Register.GetRegisterId() & 0x3F, 4, true);
                    regreg.EndList();
                }

                reg.EndList();
            }
            else if (operand.Type is OperandType.ImplicitMem)
            {
                op.WriteFieldDesignation("mem").WriteInitializerList()
                   .BeginList()
                   .WriteFieldDesignation("seg").WriteInteger((int)(operand.MemorySegment ?? SegmentRegister.None))
                   .WriteFieldDesignation("base").WriteExpression(operand.MemoryBase!.Value.ToZydisString())
                   .EndList();
            }
            else
            {
                op
                    .WriteFieldDesignation("encoding").WriteExpression("ZYDIS_OPERAND_ENCODING_{0}", operand.Encoding.ToZydisString());
            }

            op.EndList();

            initializerListWriter
                .WriteFieldDesignation("is_multisource4").WriteBool(operand.IsMultiSource4)
                .WriteFieldDesignation("ignore_seg_override").WriteBool(operand.IgnoreSegmentOverride);

            initializerListWriter.EndList();
        }

        operandsWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("#endif").ConfigureAwait(false);
    }
}

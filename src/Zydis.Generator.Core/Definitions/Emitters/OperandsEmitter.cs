using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        var sizes = operandsRegistry.Sizes.ToList();
        var details = operandsRegistry.OperandsDetails.ToList();
        Debug.Assert(sizes.Count < byte.MaxValue);
        Debug.Assert(details.Count < byte.MaxValue);

        foreach (var operand in operandsRegistry.Operands)
        {
            var operandEntry = operandsWriter.CreateObjectWriter(operandDeclaration)
                .WriteExpression("type", "ZYDIS_SEMANTIC_OPTYPE_{0}", operand.Type.ToZydisString())
                .WriteExpression("visibility", "ZYDIS_OPERAND_VISIBILITY_{0}", operand.Visibility.ToZydisString())
                .WriteExpression("actions", "ZYDIS_OPERAND_ACTION_{0}", operand.Access.ToZydisString())
                .WriteExpression("element_type", operand.ElementType.ToZydisString())
                .WriteBool("is_multisource4", operand.IsMultiSource4)
                .WriteBool("ignore_seg_override", operand.IgnoreSegmentOverride)
                .WriteInteger("size_reference", sizes.BinarySearch(OperandsRegistry.GetSizeTable(operand)), 2, true)
                .WriteInteger("details_reference", details.BinarySearch(OperandsRegistry.GetDetails(operand)), 2, true);

            operandsWriter.WriteObject(operandEntry);
        }

        operandsWriter.EndList();
        declarationWriter.EndDeclaration();
        declarationWriter.WriteNewline();
        declarationWriter.WriteNewline();

        var sizesWriter = declarationWriter
            .BeginDeclaration("static const", "ZyanU16", "OPERAND_SIZES[][3]")
            .WriteInitializerList()
            .BeginList();
        var sizeArrayDeclaration = new ArrayObjectDeclaration(3);

        foreach (var sizeTable in sizes)
        {
            var sizeTableEntry = new ObjectWriter(sizeArrayDeclaration, null)
                .WriteInteger(0, sizeTable.Width16)
                .WriteInteger(1, sizeTable.Width32)
                .WriteInteger(2, sizeTable.Width64);
            sizesWriter.WriteObject(sizeTableEntry);
        }

        sizesWriter.EndList();
        declarationWriter.EndDeclaration();
        declarationWriter.WriteNewline();
        declarationWriter.WriteNewline();

        var detailsWriter = declarationWriter
            .BeginDeclaration("static const", "ZydisOperandDetails", "OPERAND_DETAILS[]")
            .WriteInitializerList()
            .BeginList();
        var detailsDeclaration = new SimpleObjectDeclaration(InitializerType.Designated, "encoding", "reg", "mem");
        var regOuterDeclaration = new SimpleObjectDeclaration("type", "reg");
        var regInnerDeclaration = new SimpleObjectDeclaration(InitializerType.Designated, "reg", "id");
        var memDeclaration = new SimpleObjectDeclaration("seg", "base");

        foreach (var opDetails in details)
        {
            var detailsEntry = detailsWriter.CreateObjectWriter(detailsDeclaration);
            if (opDetails.Type == "MEMORY")
            {
                var memEntry = detailsEntry.CreateObjectWriter(memDeclaration);
                memEntry
                    .WriteInteger("seg", (int)(opDetails.MemorySegment ?? SegmentRegister.None))
                    .WriteExpression("base", opDetails.MemoryBase!.Value.ToZydisString());
                detailsEntry.WriteObject("mem", memEntry);
            }
            else if (opDetails.Type == "OTHER")
            {
                detailsEntry.WriteExpression("encoding", "ZYDIS_OPERAND_ENCODING_{0}", opDetails.Encoding.ToZydisString());
            }
            else
            {
                var regOuterEntry = detailsEntry.CreateObjectWriter(regOuterDeclaration);
                var regInnerEntry = regOuterEntry.CreateObjectWriter(regInnerDeclaration);

                if (opDetails.Type == "STATIC")
                {
                    regInnerEntry.WriteExpression("reg", "ZYDIS_REGISTER_{0}", opDetails.Register.ToZydisString());
                    regOuterEntry.WriteExpression("type", "ZYDIS_IMPLREG_TYPE_STATIC");
                }
                else
                {
                    regInnerEntry.WriteInteger("id", opDetails.Register.GetRegisterId() & 0x3F, 4, true);
                    regOuterEntry.WriteExpression("type", "ZYDIS_IMPLREG_TYPE_{0}", opDetails.Type);
                }

                regOuterEntry.WriteObject("reg", regInnerEntry);
                detailsEntry.WriteObject("reg", regOuterEntry);
            }
            detailsWriter.WriteObject(detailsEntry);
        }

        detailsWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("#endif").ConfigureAwait(false);
    }
}

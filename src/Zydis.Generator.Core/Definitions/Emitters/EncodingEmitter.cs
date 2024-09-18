using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class EncodingEmitter
{
    public static async Task EmitAsync(StreamWriter writer, EncodingRegistry encodingRegistry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(encodingRegistry);

        var declarationWriter = DeclarationWriter.Create(writer);

        var encodingsWriter = declarationWriter
            .BeginDeclaration("static const", "ZydisInstructionEncodingInfo", "INSTR_ENCODINGS[]")
            .WriteInitializerList()
            .BeginList();

        foreach (var encoding in encodingRegistry.Encodings)
        {
            var initializerListWriter = encodingsWriter.WriteInitializerList().BeginList();

            initializerListWriter
                .WriteFieldDesignation("flags").WriteExpression(GetEncodingFlags(encoding));

            var dispWriter = initializerListWriter.WriteFieldDesignation("disp").WriteInitializerList()
                .BeginList();
            dispWriter.WriteFieldDesignation("size").WriteInitializerList()
                .BeginList()
                .WriteInteger(encoding.Displacement?.Width16 ?? 0)
                .WriteInteger(encoding.Displacement?.Width32 ?? 0)
                .WriteInteger(encoding.Displacement?.Width64 ?? 0)
                .EndList();
            dispWriter.EndList();

            var immediatesWriter = initializerListWriter.WriteFieldDesignation("imm").WriteInitializerList().BeginList();

            var imm0Writer = immediatesWriter.WriteArrayDesignation(0).WriteInitializerList()
                .BeginList();
            imm0Writer.WriteFieldDesignation("size").WriteInitializerList()
                .BeginList()
                .WriteInteger(encoding.Immediate0?.Width16 ?? 0)
                .WriteInteger(encoding.Immediate0?.Width32 ?? 0)
                .WriteInteger(encoding.Immediate0?.Width64 ?? 0)
                .EndList();
            imm0Writer
                .WriteFieldDesignation("is_signed").WriteBool(encoding.Immediate0?.IsSigned ?? false)
                .WriteFieldDesignation("is_address").WriteBool(encoding.Immediate0?.IsAddress ?? false)
                .WriteFieldDesignation("is_relative").WriteBool(encoding.Immediate0?.IsRelative ?? false);
            imm0Writer.EndList();

            var imm1Writer = immediatesWriter.WriteArrayDesignation(1).WriteInitializerList()
                .BeginList();
            imm1Writer.WriteFieldDesignation("size").WriteInitializerList()
                .BeginList()
                .WriteInteger(encoding.Immediate1?.Width16 ?? 0)
                .WriteInteger(encoding.Immediate1?.Width32 ?? 0)
                .WriteInteger(encoding.Immediate1?.Width64 ?? 0)
                .EndList();
            imm1Writer
                .WriteFieldDesignation("is_signed").WriteBool(encoding.Immediate1?.IsSigned ?? false)
                .WriteFieldDesignation("is_address").WriteBool(encoding.Immediate1?.IsAddress ?? false)
                .WriteFieldDesignation("is_relative").WriteBool(encoding.Immediate1?.IsRelative ?? false);
            imm1Writer.EndList();

            immediatesWriter.EndList();

            initializerListWriter.EndList();
        }

        encodingsWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
    }

    private static string GetEncodingFlags(PhysicalInstructionEncoding encoding)
    {
        var flags = new List<string>();

        if (encoding.HasModrm)
        {
            flags.Add("ZYDIS_INSTR_ENC_FLAG_HAS_MODRM");
        }

        if (encoding.Displacement is not null)
        {
            flags.Add("ZYDIS_INSTR_ENC_FLAG_HAS_DISP");
        }

        if (encoding.Immediate0 is not null)
        {
            flags.Add("ZYDIS_INSTR_ENC_FLAG_HAS_IMM0");
        }

        if (encoding.Immediate1 is not null)
        {
            flags.Add("ZYDIS_INSTR_ENC_FLAG_HAS_IMM1");
        }

        if (encoding.ForceRegForm)
        {
            flags.Add("ZYDIS_INSTR_ENC_FLAG_FORCE_REG_FORM");
        }

        if (flags.Count is 0)
        {
            return "0";
        }

        return string.Join(" | ", flags);
    }
}

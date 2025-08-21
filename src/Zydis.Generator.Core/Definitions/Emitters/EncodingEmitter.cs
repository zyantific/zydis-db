using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        var instructionEncodingInfoDeclaration = new SimpleObjectDeclaration("flags", "disp", "imm");
        var dispDeclaration = new SimpleObjectDeclaration("size");
        var immArrayDeclaration = new ArrayObjectDeclaration(2);
        var immDeclaration = new ObjectDeclaration<PhysicalInstructionEncodingImm>();

        foreach (var encoding in encodingRegistry.Encodings)
        {
            var encodingEntry = encodingsWriter.CreateObjectWriter(instructionEncodingInfoDeclaration);
            var dispEntry = encodingEntry.CreateObjectWriter(dispDeclaration);
            dispEntry.WriteIntegerArray("size",
                encoding.Displacement?.Width16 ?? 0,
                encoding.Displacement?.Width32 ?? 0,
                encoding.Displacement?.Width64 ?? 0);
            var immArrayEntry = encodingEntry.CreateObjectWriter(immArrayDeclaration);
            PhysicalInstructionEncodingImm?[] immediates = [encoding.Immediate0, encoding.Immediate1];
            for (var i = 0; i < immediates.Length; ++i)
            {
                var immediate = immediates[i];
                var immEntry = immArrayEntry.CreateObjectWriter(immDeclaration);
                immEntry
                    .WriteIntegerArray("size",
                        immediate?.Width16 ?? 0,
                        immediate?.Width32 ?? 0,
                        immediate?.Width64 ?? 0)
                    .WriteBool("is_signed", immediate?.IsSigned ?? false)
                    .WriteBool("is_address", immediate?.IsAddress ?? false)
                    .WriteBool("is_relative", immediate?.IsRelative ?? false);
                immArrayEntry.WriteObject(i, immEntry);
            }
            encodingEntry
                .WriteExpression("flags", GetEncodingFlags(encoding))
                .WriteObject("disp", dispEntry)
                .WriteObject("imm", immArrayEntry);
            encodingsWriter.WriteObject(encodingEntry);
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

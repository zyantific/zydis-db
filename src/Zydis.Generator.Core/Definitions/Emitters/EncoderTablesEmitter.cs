using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class EncoderTablesEmitter
{
    private struct LookupEntry
    {
        public int Reference;
        public int Count;

        public LookupEntry(int reference, int count = 0)
        {
            Reference = reference;
            Count = count;
        }
    }

    public static async Task EmitAsync(StreamWriter writer, EncoderDefinitionRegistry registry, DefinitionRegistry instructions)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(registry);

        var declarationWriter = DeclarationWriter.Create(writer);
        EmitLookupTable(declarationWriter, registry);
        declarationWriter.WriteNewline();
        EmitDefinitionsTable(declarationWriter, registry, instructions);
    }

    private static IEnumerable<LookupEntry> GetLookupEntries(EncoderDefinitionRegistry registry)
    {
        var lastMnemonic = "";
        var entry = new LookupEntry();
        foreach (var (index, definition) in registry.Definitions.Index())
        {
            if (definition.Instruction.Mnemonic != lastMnemonic)
            {
                yield return entry;
                lastMnemonic = definition.Instruction.Mnemonic;
                entry = new LookupEntry(index);
            }
            ++entry.Count;
        }
        yield return entry;
    }

    private static void EmitLookupTable(DeclarationWriter writer, EncoderDefinitionRegistry registry)
    {
        var initializerListWriter = writer
            .BeginDeclaration("const", "ZydisEncoderLookupEntry", "encoder_instruction_lookup[]")
            .WriteInitializerList()
            .BeginList();

        foreach (var entry in GetLookupEntries(registry))
        {
            initializerListWriter
                .WriteInitializerList(true)
                .BeginList()
                .WriteInteger(entry.Reference, 4, true)
                .WriteInteger(entry.Count)
                .EndList();
        }

        initializerListWriter.EndList();
        writer.EndDeclaration().WriteNewline();
    }

    private static void EmitDefinitionsTable(DeclarationWriter writer, EncoderDefinitionRegistry registry, DefinitionRegistry instructions)
    {
        var initializerListWriter = writer
            .BeginDeclaration("const", "ZydisEncodableInstruction", "encoder_instructions[]")
            .WriteInitializerList()
            .BeginList();

        foreach (var definition in registry.Definitions)
        {
            initializerListWriter
                .WriteInitializerList(true)
                .BeginList()
                .WriteInteger(instructions.GetDefinitionId(definition.Instruction), 4, true)
                .WriteInteger(definition.GetOperandMask(), 4, true)
                .WriteInteger(definition.Instruction.Opcode, 2, true)
                .WriteInteger(definition.Modrm, 2, true)
                .WriteExpression(definition.Instruction.Encoding.ToZydisString())
                .WriteExpression(definition.Instruction.OpcodeMap.ToZydisString())
                .WriteExpression(definition.Modes.ToZydisString())
                .WriteExpression(definition.GetEffectiveAddressSize().ToZydisString())
                .WriteExpression(definition.OperandSizes.ToZydisString())
                .WriteExpression("ZYDIS_MANDATORY_PREFIX_{0}", definition.MandatoryPrefix.ToZydisString())
                .WriteBool(definition.RexW)
                .WriteExpression("ZYDIS_REX2_TYPE_{0}", definition.Rex2.ToZydisString())
                .WriteBool(definition.EvexNd)
                .WriteBool(definition.EvexNf)
                .WriteBool(definition.ApxOsz)
                .WriteExpression("ZYDIS_VECTOR_LENGTH_{0}", definition.VectorLength.ToZydisEncoderString())
                .WriteExpression("ZYDIS_SIZE_HINT_{0}", definition.GetSizeHint().ToZydisString())
                .WriteBool(definition.IsSwappable)
                .EndList();
        }

        initializerListWriter.EndList();
        writer.EndDeclaration().WriteNewline();
    }
}

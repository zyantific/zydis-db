using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
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
        var lookupWriter = writer
            .BeginDeclaration("const", "ZydisEncoderLookupEntry", "encoder_instruction_lookup[]")
            .WriteInitializerList()
            .BeginList();
        var lookupDeclaration = new SimpleObjectDeclaration("encoder_reference", "instruction_count");

        foreach (var entry in GetLookupEntries(registry))
        {
            var lookupEntry = lookupWriter.CreateObjectWriter(lookupDeclaration)
                .WriteInteger("encoder_reference", entry.Reference, 4, true)
                .WriteInteger("instruction_count", entry.Count);
            lookupWriter.WriteObject(lookupEntry);
        }

        lookupWriter.EndList();
        writer.EndDeclaration().WriteNewline();
    }

    private static void EmitDefinitionsTable(DeclarationWriter writer, EncoderDefinitionRegistry registry, DefinitionRegistry instructions)
    {
        var definitionWriter = writer
            .BeginDeclaration("const", "ZydisEncodableInstruction", "encoder_instructions[]")
            .WriteInitializerList()
            .BeginList();
        var encodableInstructionDeclaration = new ObjectDeclaration<EncodableDefinition>();

        foreach (var definition in registry.Definitions)
        {
            var definitionEntry = definitionWriter.CreateObjectWriter(encodableInstructionDeclaration)
                .WriteInteger("instruction_reference", instructions.GetDefinitionId(definition.Instruction), 4, true)
                .WriteInteger("operand_mask", definition.GetOperandMask(), 4, true)
                .WriteInteger("opcode", definition.Instruction.Opcode, 2, true)
                .WriteInteger("modrm", definition.Modrm, 2, true)
                .WriteExpression("encoding", definition.Instruction.Encoding.ToZydisString())
                .WriteExpression("opcode_map", definition.Instruction.OpcodeMap.ToZydisString())
                .WriteExpression("modes", definition.Modes.ToZydisString())
                .WriteExpression("address_sizes", definition.GetEffectiveAddressSize().ToZydisString())
                .WriteExpression("operand_sizes", definition.OperandSizes.ToZydisString())
                .WriteExpression("mandatory_prefix", "ZYDIS_MANDATORY_PREFIX_{0}", definition.MandatoryPrefix.ToZydisString())
                .WriteBool("rex_w", definition.RexW)
                .WriteExpression("rex2", "ZYDIS_REX2_TYPE_{0}", definition.Rex2.ToZydisString())
                .WriteBool("evex_nd", definition.EvexNd)
                .WriteBool("evex_nf", definition.EvexNf)
                .WriteBool("apx_osz", definition.ApxOsz)
                .WriteExpression("vector_length", "ZYDIS_VECTOR_LENGTH_{0}", definition.VectorLength.ToZydisEncoderString())
                .WriteExpression("accepts_hint", "ZYDIS_SIZE_HINT_{0}", definition.GetSizeHint().ToZydisString())
                .WriteBool("swappable", definition.IsSwappable);
            definitionWriter.WriteObject(definitionEntry);
        }

        definitionWriter.EndList();
        writer.EndDeclaration().WriteNewline();
    }
}

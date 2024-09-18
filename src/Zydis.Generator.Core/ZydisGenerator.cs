using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTable;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.DecoderTree.Emitters;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Core.Definitions.Emitters;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core;

public sealed class ZydisGenerator
{
    private readonly DefinitionRegistry _definitionRegistry = new();
    private readonly DecoderTreeBuilder _decoderTreeBuilder = new();
    private readonly EncodingRegistry _encodingRegistry = new();
    private readonly OperandsRegistry _operandsRegistry = new();
    private readonly AccessedFlagsRegistry _accessedFlagsRegistry = new();

    public async Task ReadDefinitionsAsync(string filename, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);

        var i = 0;

        await foreach (var definition in DefinitionReader.ReadAsync(filename, cancellationToken).ConfigureAwait(false))
        {
            i++;
            _definitionRegistry.InsertDefinition(definition);
            _decoderTreeBuilder.InsertDefinition(definition);
        }

        _decoderTreeBuilder.InsertOpcodeTableSwitchNodes();
        _decoderTreeBuilder.Optimize();

        var allDefinitions = Enum.GetValues<InstructionEncoding>()
            .SelectMany(encoding => _definitionRegistry[encoding])
            .ToArray();

        _encodingRegistry.Initialize(allDefinitions);
        _operandsRegistry.Initialize(allDefinitions);
        _accessedFlagsRegistry.Initialize(allDefinitions);

        //var emitter = new OpcodeTableConsoleEmitter(_definitionRegistry, _encodingRegistry, null);
        //emitter.Emit(_decoderTreeBuilder.OpcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null));
    }

    public async Task GenerateDataTablesAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);

        if (!Directory.Exists(outputDirectory))
        {
            throw new ArgumentException("Output directory does not exist.", nameof(outputDirectory));
        }

        var statistics = new DecoderTableEmitterStatistics();

        await using var tableWriter = new StreamWriter(Path.Combine(outputDirectory, "src", "Generated", "DecoderTables.inc"), false, Encoding.UTF8); // TODO: ConfigureAwait

        await GenerateOpcodeTables(tableWriter, statistics).ConfigureAwait(false);
        await GenerateOpcodeTableLookup(tableWriter).ConfigureAwait(false);

        await tableWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

        await using var definitionWriter = new StreamWriter(Path.Combine(outputDirectory, "src", "Generated", "InstructionDefinitions.inc"), false, Encoding.UTF8);

        await DefinitionEmitter.EmitAsync(definitionWriter, _definitionRegistry, _operandsRegistry, _accessedFlagsRegistry).ConfigureAwait(false);

        await using var operandsWriter = new StreamWriter(Path.Combine(outputDirectory, "src", "Generated", "OperandDefinitions.inc"), false, Encoding.UTF8);

        await OperandsEmitter.EmitAsync(operandsWriter, _operandsRegistry, cancellationToken).ConfigureAwait(false);

        await using var encodingsWriter = new StreamWriter(Path.Combine(outputDirectory, "src", "Generated", "InstructionEncodings.inc"), false, Encoding.UTF8);

        await EncodingEmitter.EmitAsync(encodingsWriter, _encodingRegistry, cancellationToken).ConfigureAwait(false);

        await using var flagsWriter = new StreamWriter(Path.Combine(outputDirectory, "src", "Generated", "AccessedFlags.inc"), false, Encoding.UTF8);

        await AffectedFlagsEmitter.EmitAsync(flagsWriter, _accessedFlagsRegistry, cancellationToken).ConfigureAwait(false);
    }

    private async Task GenerateOpcodeTables(StreamWriter writer, DecoderTableEmitterStatistics statistics)
    {
        foreach (var table in _decoderTreeBuilder.OpcodeTables.Tables)
        {
            if (!table.HasNonZeroEntries)
            {
                continue; // Skip empty tables.
            }

            var declarationWriter = new DeclarationWriter(writer, true);
            declarationWriter
                .BeginDeclaration("static const", "ZydisDecoderTreeNode", $"DECODER_TREE_{table}[]");

            var initializerListWriter = declarationWriter.WriteInitializerList().BeginList();

            var emitter = new DecoderTableCodeEmitter(initializerListWriter, _definitionRegistry, _encodingRegistry, statistics);
            emitter.Emit(table);

            initializerListWriter.EndList();
            declarationWriter.EndDeclaration();

            await writer.WriteLineAsync().ConfigureAwait(false);
        }
    }

    private async Task GenerateOpcodeTableLookup(StreamWriter writer)
    {
        await writer.WriteLineAsync().ConfigureAwait(false);

        var declarationWriter = new DeclarationWriter(writer, true);
        declarationWriter
            .BeginDeclaration("static const", "ZydisDecoderTreeNode* const", "OPCODE_TABLE_TREES[]");

        var initializerListWriter = declarationWriter.WriteInitializerList().BeginList();

        foreach (var table in _decoderTreeBuilder.OpcodeTables.Tables)
        {
            initializerListWriter.WriteInlineComment("{0,-12}", table);

            if (!table.HasNonZeroEntries)
            {
                initializerListWriter.WriteNull();
                continue; // Skip empty tables.
            }

            initializerListWriter.WriteExpression("&DECODER_TREE_{0}[0]", table);
        }

        initializerListWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
    }
}

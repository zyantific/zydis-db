using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.DecoderTree.Emitters;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Core.Definitions.Emitters;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core;

public sealed class ZydisGenerator
{
    private readonly DefinitionRegistry _definitionRegistry = new();
    private readonly VariablePositionTreeBuilder _variablePositionTreeBuilder = new();
    private readonly EncodingRegistry _encodingRegistry = new();
    private readonly OperandsRegistry _operandsRegistry = new();
    private readonly AccessedFlagsRegistry _accessedFlagsRegistry = new();
    private readonly EncoderDefinitionRegistry _encoderRegistry = new();
    private readonly ConditionCodeRegistry _conditionCodeRegistry = new();
    private readonly RelativeInfoRegistry _relativeInfoRegistry = new();
    private readonly FormatterStringsRegistry _formatterStringsRegistry = new();

    /// <summary>
    /// The variable-position construction statistics from the most recent <see cref="ReadDefinitionsAsync"/>;
    /// <see cref="BuilderStatistics.Empty"/> until definitions have been read.
    /// </summary>
    public BuilderStatistics DecoderTreeStatistics { get; private set; } = BuilderStatistics.Empty;

    /// <summary>
    /// The per-table decoder-table emission measurements from the most recent <see cref="GenerateDataTablesAsync"/>.
    /// </summary>
    public IReadOnlyList<TableEmissionStatistics> DecoderTableEmissionStatistics { get; private set; } = [];

    private OpcodeTables OpcodeTables => _variablePositionTreeBuilder.OpcodeTables;

    public async Task ReadDefinitionsAsync(string datafilesPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(datafilesPath);

        await foreach (var definition in DefinitionReader.ReadAsync<InstructionDefinition>(Path.Join(datafilesPath, "instructions.json"), cancellationToken).ConfigureAwait(false))
        {
            _definitionRegistry.InsertDefinition(definition);
            _encoderRegistry.InsertDefinition(definition);
            _variablePositionTreeBuilder.InsertDefinition(definition);
        }

        // The variable-position builder compacts each group inside its constructor, so it needs no separate
        // optimization pass.
        _variablePositionTreeBuilder.Build();
        _variablePositionTreeBuilder.InsertOpcodeTableSwitchNodes();
        DecoderTreeStatistics = _variablePositionTreeBuilder.Statistics;

        var allDefinitions = Enum.GetValues<InstructionEncoding>()
            .SelectMany(encoding => _definitionRegistry[encoding])
            .ToArray();

        _encodingRegistry.Initialize(allDefinitions);
        _operandsRegistry.Initialize(allDefinitions);
        _accessedFlagsRegistry.Initialize(allDefinitions);

        _encoderRegistry.Optimize();
        _conditionCodeRegistry.Initialize(_encoderRegistry.Definitions);
        _relativeInfoRegistry.Initialize(_encoderRegistry.Definitions);

        //var emitter = new OpcodeTableConsoleEmitter(_definitionRegistry, _encodingRegistry, null);
        //emitter.Emit(OpcodeTables.GetTable(InstructionEncoding.Default, OpcodeMap.MAP0, null));

        await _formatterStringsRegistry.ReadAsync(Path.Join(datafilesPath, "formatter_strings.json"), cancellationToken);
    }

    public async Task GenerateDataTablesAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);

        if (!Directory.Exists(outputDirectory))
        {
            throw new ArgumentException("Output directory does not exist.", nameof(outputDirectory));
        }

        var statistics = new DecoderTableEmitterStatistics();
        var generatedSourcesPath = Path.Combine(outputDirectory, "src", "Generated");
        var generatedIncludePath = Path.Combine(outputDirectory, "include", "Zydis", "Generated");

        await using var tableWriter = CreateWriter(Path.Combine(generatedSourcesPath, "DecoderTables.inc")); // TODO: ConfigureAwait

        await GenerateOpcodeTables(tableWriter, statistics).ConfigureAwait(false);
        await GenerateOpcodeTableLookup(tableWriter).ConfigureAwait(false);

        await tableWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

        await using var definitionWriter = CreateWriter(Path.Combine(generatedSourcesPath, "InstructionDefinitions.inc"));

        await DefinitionEmitter.EmitAsync(definitionWriter, _definitionRegistry, _operandsRegistry, _accessedFlagsRegistry).ConfigureAwait(false);

        await using var operandsWriter = CreateWriter(Path.Combine(generatedSourcesPath, "OperandDefinitions.inc"));

        await OperandsEmitter.EmitAsync(operandsWriter, _operandsRegistry, cancellationToken).ConfigureAwait(false);

        await using var encodingsWriter = CreateWriter(Path.Combine(generatedSourcesPath, "InstructionEncodings.inc"));

        await EncodingEmitter.EmitAsync(encodingsWriter, _encodingRegistry, cancellationToken).ConfigureAwait(false);

        await using var flagsWriter = CreateWriter(Path.Combine(generatedSourcesPath, "AccessedFlags.inc"));

        await AffectedFlagsEmitter.EmitAsync(flagsWriter, _accessedFlagsRegistry, cancellationToken).ConfigureAwait(false);

        await using var encoderWriter = CreateWriter(Path.Combine(generatedSourcesPath, "EncoderTables.inc"));

        await EncoderTablesEmitter.EmitAsync(encoderWriter, _encoderRegistry, _definitionRegistry).ConfigureAwait(false);

        await using var conditionCodeWriter = CreateWriter(Path.Combine(generatedSourcesPath, "GetCcInfo.inc"));

        await ConditionCodeEmitter.EmitAsync(conditionCodeWriter, _conditionCodeRegistry).ConfigureAwait(false);

        await using var relativeInfoWriter = CreateWriter(Path.Combine(generatedSourcesPath, "GetRelInfo.inc"));

        await RelativeInfoEmitter.EmitAsync(relativeInfoWriter, _relativeInfoRegistry).ConfigureAwait(false);

        async Task GenerateEnum(string enumName, string prefix, bool useInternalStringType, IEnumerable<string> items, string invalidValue)
        {
            var emitter = new EnumEmitter(enumName, prefix, new SortedSet<string>(items).Prepend(invalidValue));
            await using var enumDefWriter = CreateWriter(Path.Combine(generatedIncludePath, $"Enum{enumName}.h"));
            await emitter.EmitDefinitionAsync(enumDefWriter).ConfigureAwait(false);
            await using var enumStringsWriter = CreateWriter(Path.Combine(generatedSourcesPath, $"Enum{enumName}.inc"));
            await emitter.EmitStringsAsync(enumStringsWriter, useInternalStringType).ConfigureAwait(false);
        }
        await GenerateEnum(
            "Mnemonic",
            "MNEMONIC",
            true,
            _definitionRegistry.Select(definition => definition.Mnemonic),
            "invalid"
        ).ConfigureAwait(false);
        await GenerateEnum(
            "InstructionCategory",
            "CATEGORY",
            false,
            _definitionRegistry.Select(definition => definition.MetaInfo.Category),
            "INVALID"
        ).ConfigureAwait(false);
        await GenerateEnum(
            "ISASet",
            "ISA_SET",
            false,
            _definitionRegistry.Select(definition => definition.MetaInfo.IsaSet),
            "INVALID"
        ).ConfigureAwait(false);
        await GenerateEnum(
            "ISAExt",
            "ISA_EXT",
            false,
            _definitionRegistry.Select(definition => definition.MetaInfo.IsaExtension),
            "INVALID"
        ).ConfigureAwait(false);

        await using var formatterStringsWriter = CreateWriter(Path.Combine(generatedSourcesPath, "FormatterStrings.inc"));
        await FormatterStringsEmitter.EmitAsync(formatterStringsWriter, _formatterStringsRegistry).ConfigureAwait(false);
    }

    private StreamWriter CreateWriter(string path)
    {
        var writer = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false))
        {
            NewLine = "\n"
        };
        return writer;
    }

    private async Task GenerateOpcodeTables(StreamWriter writer, DecoderTableEmitterStatistics statistics)
    {
        var emissionStatistics = new List<TableEmissionStatistics>();

        foreach (var table in OpcodeTables.Tables)
        {
            if (table.EnumerateSlots().All(x => x is null))
            {
                continue; // Skip empty tables.
            }

            var declarationWriter = new DeclarationWriter(writer, true);
            declarationWriter
                .BeginDeclaration("static const", "ZydisDecoderTreeNode", $"DECODER_TREE_{table}[]");

            var initializerListWriter = declarationWriter.WriteInitializerList().BeginList();

            var emitter = new DecoderTableCodeEmitter(initializerListWriter, _definitionRegistry, _encodingRegistry, statistics);
            var size = emitter.Emit(table);

            emissionStatistics.Add(
                new TableEmissionStatistics(table.ToString()!, size, emitter.EmittedNodeCount, emitter.CloneCount));

            initializerListWriter.EndList();
            declarationWriter.EndDeclaration();

            await writer.WriteLineAsync().ConfigureAwait(false);
        }

        DecoderTableEmissionStatistics = emissionStatistics;
    }

    private async Task GenerateOpcodeTableLookup(StreamWriter writer)
    {
        await writer.WriteLineAsync().ConfigureAwait(false);

        var declarationWriter = new DeclarationWriter(writer, true);
        declarationWriter
            .BeginDeclaration("static const", "ZydisDecoderTreeNode* const", "OPCODE_TABLE_TREES[]");

        var initializerListWriter = declarationWriter.WriteInitializerList().BeginList();

        foreach (var table in OpcodeTables.Tables)
        {
            initializerListWriter.WriteInlineComment("{0,-12}", table);

            if (table.EnumerateSlots().All(x => x is null))
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

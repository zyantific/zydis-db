using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Walks every decoder-tree leaf, expands it to its machine modes and collects the samples and the report.
/// </summary>
internal sealed class CorpusGenerator
{
    private static readonly ModeNode.Slot[] AllModes = [ModeNode.Slot.M16, ModeNode.Slot.M32, ModeNode.Slot.M64];
    private static readonly ModeNode.Slot[] Modes64 = [ModeNode.Slot.M64];
    private static readonly ModeNode.Slot[] ModesNot64 = [ModeNode.Slot.M16, ModeNode.Slot.M32];

    // The order of the corpus files and of the report sections.
    private static readonly InstructionEncoding[] EncodingOrder =
    [
        InstructionEncoding.Default,
        InstructionEncoding.AMD3DNOW,
        InstructionEncoding.XOP,
        InstructionEncoding.VEX,
        InstructionEncoding.EVEX,
        InstructionEncoding.MVEX
    ];

    private readonly OpcodeTables _tables;
    private readonly IReadOnlyList<InstructionDefinition> _definitions;
    private readonly IReadOnlyDictionary<InstructionDefinition, string> _formIds;
    private readonly Func<InstructionDefinition, int> _definitionId;
    private readonly Dictionary<InstructionDefinition, int> _definitionIds = new(ReferenceEqualityComparer.Instance);

    /// <param name="tables">The built decoder tree.</param>
    /// <param name="definitions">
    /// Every parsed definition, twins included, for the registry size and the unreachable definitions.
    /// </param>
    /// <param name="definitionId">The id the emitted tables use for a definition.</param>
    /// <param name="formIds">The FormId of every definition, keyed by reference.</param>
    public CorpusGenerator(OpcodeTables tables, IReadOnlyList<InstructionDefinition> definitions,
        Func<InstructionDefinition, int> definitionId, IReadOnlyDictionary<InstructionDefinition, string> formIds)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(formIds);

        _tables = tables;
        _definitions = definitions;
        _definitionId = definitionId;
        _formIds = formIds;
    }

    public CorpusReport Generate()
    {
        var accumulators = EncodingOrder.ToDictionary(encoding => encoding, _ => new EncodingAccumulator());

        foreach (var table in _tables.Tables.Where(table => table.EnumerateSlots().Any(slot => slot is not null)))
        {
            accumulators[table.Encoding].TablesWalked++;
        }

        foreach (var path in DecoderTreePathEnumerator.Enumerate(_tables))
        {
            var accumulator = accumulators[path.Encoding];
            accumulator.LeavesFound++;

            var id = GetDefinitionId(path.Definition);
            var form = _formIds[path.Definition];

            foreach (var mode in ExpandModes(path))
            {
                switch (SampleBuilder.Build(path, mode, id, form))
                {
                    case Built built:
                        accumulator.Samples.Add(built.Sample);
                        accumulator.SampleIds.Add(id);
                        break;
                    case Impossible impossible:
                        accumulator.Impossible.Add(new CorpusIssue(mode, path, id, form, impossible.Reason));
                        break;
                    case Unencodable unencodable:
                        accumulator.Unencodable.Add(new CorpusIssue(mode, path, id, form, unencodable.Reason));
                        break;
                    default:
                        throw new InvalidOperationException("Unknown sample build result.");
                }
            }
        }

        foreach (var definition in _definitions)
        {
            var registry = accumulators[definition.Encoding].Registry;
            var id = GetDefinitionId(definition);

            if (!registry.TryGetValue(id, out var sharing))
            {
                sharing = [];
                registry.Add(id, sharing);
            }

            sharing.Add(definition);
        }

        var samples = EncodingOrder
            .SelectMany(encoding => accumulators[encoding].Samples
                .OrderBy(sample => sample.Id)
                .ThenBy(sample => int.Parse(sample.Mode, CultureInfo.InvariantCulture))
                .ThenBy(sample => sample.Path, StringComparer.Ordinal))
            .ToArray();

        var encodings = EncodingOrder
            .Select(encoding => accumulators[encoding].ToReport(encoding, _formIds))
            .ToArray();

        return new CorpusReport(samples, encodings);
    }

    /// <summary>
    /// The machine modes of a leaf: the slots of its <c>mode</c> and <c>mode_compact</c> steps, 64-bit mode only for
    /// MVEX, else all three.
    /// </summary>
    private static IEnumerable<ModeNode.Slot> ExpandModes(DecoderTreePath path)
    {
        IEnumerable<ModeNode.Slot> modes = (path.Encoding is InstructionEncoding.MVEX) ? Modes64 : AllModes;

        foreach (var step in path.Steps)
        {
            var served = step.Node switch
            {
                SampleBuilder.ModeAxis => step.Slots.Select(slot => slot switch
                {
                    "16" => ModeNode.Slot.M16,
                    "32" => ModeNode.Slot.M32,
                    "64" => ModeNode.Slot.M64,
                    _ => throw new InvalidOperationException($"Unknown mode slot '{slot}'.")
                }),
                SampleBuilder.ModeCompactAxis => step.Slots.SelectMany(slot => slot switch
                {
                    "64" => Modes64,
                    "!64" => ModesNot64,
                    _ => throw new InvalidOperationException($"Unknown mode_compact slot '{slot}'.")
                }),
                _ => null
            };

            if (served is not null)
            {
                modes = modes.Intersect(served);
            }
        }

        return [.. modes.Order()];
    }

    private int GetDefinitionId(InstructionDefinition definition)
    {
        // The registry lookup is linear in the number of definitions of the encoding.
        if (!_definitionIds.TryGetValue(definition, out var id))
        {
            id = _definitionId(definition);
            _definitionIds.Add(definition, id);
        }

        return id;
    }

    private sealed class EncodingAccumulator
    {
        public int TablesWalked { get; set; }

        public int LeavesFound { get; set; }

        public List<CorpusSample> Samples { get; } = [];

        public HashSet<int> SampleIds { get; } = [];

        public List<CorpusIssue> Impossible { get; } = [];

        public List<CorpusIssue> Unencodable { get; } = [];

        public SortedDictionary<int, List<InstructionDefinition>> Registry { get; } = [];

        public CorpusEncodingReport ToReport(InstructionEncoding encoding,
            IReadOnlyDictionary<InstructionDefinition, string> formIds)
        {
            var unreachable = Registry
                .Where(entry => !SampleIds.Contains(entry.Key))
                .Select(entry => new CorpusUnreachableDefinition(entry.Key,
                    [.. entry.Value.Select(definition => formIds[definition])], entry.Value[0].Mnemonic))
                .ToArray();

            return new CorpusEncodingReport(encoding, TablesWalked, LeavesFound, Samples.Count, Registry.Count,
                Sorted(Impossible), Sorted(Unencodable), unreachable);
        }

        private static CorpusIssue[] Sorted(List<CorpusIssue> issues)
        {
            return [.. issues
                .OrderBy(issue => issue.Id)
                .ThenBy(issue => issue.Mode)
                .ThenBy(issue => issue.Path.PathString, StringComparer.Ordinal)];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// The samples of a corpus run and, per encoding, the counts and the leaves and definitions without a sample.
/// </summary>
internal sealed class CorpusReport
{
    public CorpusReport(IReadOnlyList<CorpusSample> samples, IReadOnlyList<CorpusEncodingReport> encodings)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(encodings);

        Samples = samples;
        Encodings = encodings;
    }

    /// <summary>
    /// The samples, sorted by encoding, definition id, machine mode and path.
    /// </summary>
    public IReadOnlyList<CorpusSample> Samples { get; }

    public IReadOnlyList<CorpusEncodingReport> Encodings { get; }

    /// <summary>
    /// The number of (leaf, machine mode) pairs the walk could not encode. The corpus is incomplete unless it is 0.
    /// </summary>
    public int UnencodableCount => Encodings.Sum(encoding => encoding.Unencodable.Count);

    public string Render()
    {
        var builder = new StringBuilder();

        foreach (var encoding in Encodings)
        {
            builder.Append(CultureInfo.InvariantCulture, $"encoding {CorpusTags.EncodingTag(encoding.Encoding)}\n");
            AppendCount(builder, "tables walked", encoding.TablesWalked);
            AppendCount(builder, "leaves found", encoding.LeavesFound);
            AppendCount(builder, "samples written", encoding.SamplesWritten);
            AppendCount(builder, "registry size", encoding.RegistrySize);
            AppendCount(builder, "distinct sample ids", encoding.DistinctSampleIds);

            AppendCount(builder, "impossible by architecture", encoding.Impossible.Count);
            foreach (var issue in encoding.Impossible)
            {
                AppendIssue(builder, issue);
            }

            AppendCount(builder, "unencodable", encoding.Unencodable.Count);
            foreach (var issue in encoding.Unencodable)
            {
                AppendIssue(builder, issue);
            }

            AppendCount(builder, "unreachable definitions", encoding.Unreachable.Count);
            foreach (var definition in encoding.Unreachable)
            {
                AppendDefinition(builder, encoding.Encoding, definition);
            }

            AppendCount(builder, "definitions without a sample", encoding.WithoutSample.Count);
            foreach (var definition in encoding.WithoutSample)
            {
                AppendDefinition(builder, encoding.Encoding, definition);
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendCount(StringBuilder builder, string name, int count)
    {
        builder.Append(CultureInfo.InvariantCulture, $"  {name + ":",-32}{count}\n");
    }

    private static void AppendDefinition(StringBuilder builder, InstructionEncoding encoding,
        CorpusDefinitionEntry definition)
    {
        builder.Append(CultureInfo.InvariantCulture,
            $"    {CorpusTags.EncodingTag(encoding)} {definition.Id} {definition.Form} {definition.Mnemonic}");

        if (definition.Reasons.Count > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $": {string.Join(" | ", definition.Reasons)}");
        }

        builder.Append('\n');
    }

    private static void AppendIssue(StringBuilder builder, CorpusIssue issue)
    {
        builder.Append(CultureInfo.InvariantCulture,
            $"    mode={CorpusTags.ModeTag(issue.Mode)} map={CorpusTags.MapTag(issue.Path.Map)} " +
            $"opcode={issue.Path.Opcode:X2} id={issue.Id} form={issue.Form} path={issue.Path.PathString}: " +
            $"{issue.Reason}\n");
    }
}

/// <summary>
/// The counts and lists of one encoding.
/// </summary>
/// <param name="Encoding">The encoding.</param>
/// <param name="TablesWalked">The opcode tables of the encoding that hold at least one entry.</param>
/// <param name="LeavesFound">The decoder-tree paths that end in a definition of the encoding.</param>
/// <param name="SamplesWritten">The samples of the encoding.</param>
/// <param name="RegistrySize">The number of distinct definition ids of the encoding.</param>
/// <param name="DistinctSampleIds">The number of distinct definition ids the samples carry.</param>
/// <param name="Impossible">The (leaf, machine mode) pairs no instruction can reach.</param>
/// <param name="Unencodable">The (leaf, machine mode) pairs the walk could not encode.</param>
/// <param name="Unreachable">The definitions that no decoder-tree path reaches.</param>
/// <param name="WithoutSample">
/// The definitions that paths reach but that got no sample, because every (leaf, machine mode) pair is impossible or
/// unencodable.
/// </param>
internal sealed record CorpusEncodingReport(
    InstructionEncoding Encoding,
    int TablesWalked,
    int LeavesFound,
    int SamplesWritten,
    int RegistrySize,
    int DistinctSampleIds,
    IReadOnlyList<CorpusIssue> Impossible,
    IReadOnlyList<CorpusIssue> Unencodable,
    IReadOnlyList<CorpusDefinitionEntry> Unreachable,
    IReadOnlyList<CorpusDefinitionEntry> WithoutSample);

/// <summary>
/// A (leaf, machine mode) pair without a sample.
/// </summary>
internal sealed record CorpusIssue(ModeNode.Slot Mode, DecoderTreePath Path, int Id, string Form, string Reason);

/// <summary>
/// A definition the report lists, with the distinct reasons its (leaf, machine mode) pairs got no sample.
/// </summary>
internal sealed record CorpusDefinitionEntry(int Id, string Form, string Mnemonic, IReadOnlyList<string> Reasons);

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Per encoding, the samples of a corpus run, the counts and the leaves and definitions without a sample.
/// </summary>
internal sealed class CorpusReport
{
    public CorpusReport(IReadOnlyList<CorpusEncodingReport> encodings)
    {
        ArgumentNullException.ThrowIfNull(encodings);

        Encodings = encodings;
    }

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
            AppendCount(builder, "samples written", encoding.Samples.Count);
            AppendCount(builder, "registry size", encoding.RegistrySize);
            AppendCount(builder, "distinct sample ids", encoding.DistinctSampleIds);

            AppendIssues(builder, "impossible by architecture or routing", encoding.Impossible);
            AppendIssues(builder, "unencodable", encoding.Unencodable);
            AppendDefinitions(builder, "unreachable definitions", encoding.Encoding, encoding.Unreachable);
            AppendDefinitions(builder, "definitions without a sample", encoding.Encoding, encoding.WithoutSample);

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendCount(StringBuilder builder, string name, int count)
    {
        builder.Append(CultureInfo.InvariantCulture, $"  {name + ":",-32}{count}\n");
    }

    private static void AppendDefinitions(StringBuilder builder, string name, InstructionEncoding encoding,
        IReadOnlyList<CorpusDefinitionEntry> definitions)
    {
        AppendCount(builder, name, definitions.Count);

        foreach (var definition in definitions)
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"    {CorpusTags.EncodingTag(encoding)} {definition.Id} {definition.Form} {definition.Mnemonic}");

            if (definition.Reasons.Count > 0)
            {
                builder.Append(CultureInfo.InvariantCulture, $": {string.Join(" | ", definition.Reasons)}");
            }

            builder.Append('\n');
        }
    }

    private static void AppendIssues(StringBuilder builder, string name, IReadOnlyList<CorpusIssue> issues)
    {
        AppendCount(builder, name, issues.Count);

        foreach (var issue in issues)
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"    mode={CorpusTags.ModeTag(issue.Mode)} map={CorpusTags.MapTag(issue.Path.Map)} " +
                $"opcode={issue.Path.Opcode:X2} id={issue.Id} form={issue.Form} path={issue.Path.PathString}: " +
                $"{issue.Reason}\n");
        }
    }
}

/// <summary>
/// The counts and lists of one encoding.
/// </summary>
/// <param name="Encoding">The encoding.</param>
/// <param name="TablesWalked">The opcode tables of the encoding that hold at least one entry.</param>
/// <param name="LeavesFound">The decoder-tree paths that end in a definition of the encoding.</param>
/// <param name="Samples">The samples of the encoding, sorted by definition id, machine mode and path.</param>
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
    IReadOnlyList<CorpusSample> Samples,
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

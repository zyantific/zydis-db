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
            builder.Append(CultureInfo.InvariantCulture, $"encoding {SampleBuilder.EncodingTag(encoding.Encoding)}\n");
            AppendCount(builder, "tables walked", encoding.TablesWalked);
            AppendCount(builder, "leaves found", encoding.LeavesFound);
            AppendCount(builder, "samples written", encoding.SamplesWritten);
            AppendCount(builder, "registry size", encoding.RegistrySize);

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
                builder.Append(CultureInfo.InvariantCulture,
                    $"    {SampleBuilder.EncodingTag(encoding.Encoding)} {definition.Id} " +
                    $"{string.Join(',', definition.Forms)} {definition.Mnemonic}\n");
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static void AppendCount(StringBuilder builder, string name, int count)
    {
        builder.Append(CultureInfo.InvariantCulture, $"  {name + ":",-28}{count}\n");
    }

    private static void AppendIssue(StringBuilder builder, CorpusIssue issue)
    {
        builder.Append(CultureInfo.InvariantCulture,
            $"    mode={SampleBuilder.ModeTag(issue.Mode)} map={SampleBuilder.MapTag(issue.Path.Map)} " +
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
/// <param name="Impossible">The (leaf, machine mode) pairs no instruction can reach.</param>
/// <param name="Unencodable">The (leaf, machine mode) pairs the walk could not encode.</param>
/// <param name="Unreachable">The definition ids no sample carries.</param>
internal sealed record CorpusEncodingReport(
    InstructionEncoding Encoding,
    int TablesWalked,
    int LeavesFound,
    int SamplesWritten,
    int RegistrySize,
    IReadOnlyList<CorpusIssue> Impossible,
    IReadOnlyList<CorpusIssue> Unencodable,
    IReadOnlyList<CorpusUnreachableDefinition> Unreachable);

/// <summary>
/// A (leaf, machine mode) pair without a sample.
/// </summary>
internal sealed record CorpusIssue(ModeNode.Slot Mode, DecoderTreePath Path, int Id, string Form, string Reason);

/// <summary>
/// A definition id that no sample carries, with the FormIds of every definition that shares it.
/// </summary>
internal sealed record CorpusUnreachableDefinition(int Id, IReadOnlyList<string> Forms, string Mnemonic);

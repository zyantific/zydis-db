using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Writes one <c>&lt;encoding&gt;.tsv</c> file per encoding and <c>report.txt</c>.
/// </summary>
internal static class CorpusWriter
{
    public static void Write(CorpusReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrEmpty(directory);

        Directory.CreateDirectory(directory);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // An encoding without samples still gets its header, so the checker can tell an empty file from a missing one.
        foreach (var encodingReport in report.Encodings)
        {
            var tag = CorpusTags.EncodingTag(encodingReport.Encoding);
            var lines = report.Samples
                .Where(sample => sample.Encoding == tag)
                .Select(sample => sample.ToTsvLine())
                .Prepend(CorpusSample.Header);

            File.WriteAllText(Path.Combine(directory, $"{tag}.tsv"), string.Join('\n', lines) + "\n", encoding);
        }

        File.WriteAllText(Path.Combine(directory, "report.txt"), report.Render(), encoding);
    }
}

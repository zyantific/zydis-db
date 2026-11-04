using System;
using System.IO;
using System.Text;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Writes one <c>&lt;encoding&gt;.tsv</c> file per encoding and <c>report.txt</c>.
/// </summary>
internal static class CorpusWriter
{
    public static void Write(CorpusReport report, string renderedReport, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(renderedReport);
        ArgumentException.ThrowIfNullOrEmpty(directory);

        Directory.CreateDirectory(directory);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // An encoding without samples still gets its header, so the checker can tell an empty file from a missing one.
        foreach (var encodingReport in report.Encodings)
        {
            var path = Path.Combine(directory, $"{CorpusTags.EncodingTag(encodingReport.Encoding)}.tsv");
            using var writer = new StreamWriter(path, append: false, encoding) { NewLine = "\n" };

            writer.WriteLine(CorpusSample.Header);
            foreach (var sample in encodingReport.Samples)
            {
                writer.WriteLine(sample.ToTsvLine());
            }
        }

        File.WriteAllText(Path.Combine(directory, "report.txt"), renderedReport, encoding);
    }
}

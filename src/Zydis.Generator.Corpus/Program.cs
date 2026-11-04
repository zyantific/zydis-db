using System;
using System.Threading.Tasks;

using Zydis.Generator.Core;
using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Corpus;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            await Console.Error.WriteLineAsync("usage: Zydis.Generator.Corpus path/to/datafiles/ path/to/output/")
                .ConfigureAwait(false);
            return 1;
        }

        var generator = new ZydisGenerator();
        await generator.ReadDefinitionsAsync(args[0]).ConfigureAwait(false);

        var formIds = FormId.AssignIds(generator.Definitions);
        var corpus = new CorpusGenerator(generator.OpcodeTables, generator.Definitions, generator.GetDefinitionId,
            formIds);
        var report = corpus.Generate();
        var renderedReport = report.Render();

        CorpusWriter.Write(report, renderedReport, args[1]);
        Console.Write(renderedReport);

        // A leaf without a sample leaves the corpus incomplete, while impossible leaves and unreachable definitions
        // are findings that do not fail the run.
        return (report.UnencodableCount > 0) ? 3 : 0;
    }
}

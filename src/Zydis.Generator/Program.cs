using System.Threading.Tasks;

using Zydis.Generator.Core;

namespace Zydis.Generator;

internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            await System.Console.Error.WriteLineAsync("usage: Zydis.Generator [path/to/datafiles/] [path/to/zydis/]").ConfigureAwait(false);
            return 1;
        }

        var generator = new ZydisGenerator();

        await generator.ReadDefinitionsAsync(args[0]).ConfigureAwait(false);

        await generator.GenerateDataTablesAsync(args[1]).ConfigureAwait(false);

        //var emitter = new DecoderTableConsoleEmitter{ SkipEmpty = true };
        //emitter.Emit(builder.OpcodeTables.GetTable(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.P66));
        return 0;
    }
}

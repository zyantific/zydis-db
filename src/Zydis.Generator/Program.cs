using System.Threading.Tasks;

using Zydis.Generator.Core;

namespace Zydis.Generator;

internal sealed class Program
{
    private static async Task Main(/*string[] args*/)
    {
        var generator = new ZydisGenerator();

        await generator.ReadDefinitionsAsync("D:\\zyantific\\zydis-db\\Data\\instructions.json").ConfigureAwait(false);

        await generator.GenerateDataTablesAsync("D:\\zyantific\\zydis").ConfigureAwait(false);

        //var emitter = new DecoderTableConsoleEmitter{ SkipEmpty = true };
        //emitter.Emit(builder.OpcodeTables.GetTable(InstructionEncoding.VEX, OpcodeMap.M0F, RefiningPrefix.P66));
    }
}

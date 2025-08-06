using System;
using System.IO;
using System.Threading.Tasks;

using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class ConditionCodeEmitter
{
    public static async Task EmitAsync(StreamWriter writer, ConditionCodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(registry);

        // TODO: Add something to handle indentation automatically
        writer.WriteLine("ZyanBool ZydisGetCcInfo(ZydisMnemonic mnemonic, ZydisSourceConditionCode *scc)");
        writer.WriteLine("{");
        writer.WriteLine("    switch (mnemonic)");
        writer.WriteLine("    {");
        foreach (var info in registry.ConditionCodeInfos)
        {
            foreach (var mnemonic in info.Mnemonics)
            {
                writer.WriteLine("    case ZYDIS_MNEMONIC_{0}:", mnemonic.ToUpperInvariant());
            }
            writer.WriteLine("        *scc = {0};", info.Scc);
            writer.WriteLine("        return ZYAN_TRUE;");
        }
        writer.WriteLine("    default:");
        writer.WriteLine("        *scc = ZYDIS_SCC_NONE;");
        writer.WriteLine("        return ZYAN_FALSE;");
        writer.WriteLine("    }");
        writer.WriteLine("}");
    }
}

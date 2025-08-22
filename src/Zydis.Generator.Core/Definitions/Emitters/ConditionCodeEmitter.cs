using System;
using System.IO;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class ConditionCodeEmitter
{
    public static async Task EmitAsync(StreamWriter writer, ConditionCodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(registry);

        var codeWriter = new CodeWriter(writer);
        codeWriter
            .WriteLine("ZyanBool ZydisGetCcInfo(ZydisMnemonic mnemonic, ZydisSourceConditionCode *scc)")
            .BeginBlock()
            .WriteLine("switch (mnemonic)")
            .BeginBlock(false);
        foreach (var info in registry.ConditionCodeInfos)
        {
            foreach (var mnemonic in info.Mnemonics)
            {
                codeWriter.WriteLine("case ZYDIS_MNEMONIC_{0}:", mnemonic.ToUpperInvariant());
            }
            codeWriter
                .BeginIndent()
                .WriteLine("*scc = {0};", info.Scc)
                .WriteLine("return ZYAN_TRUE;")
                .EndIndent();
        }
        codeWriter
            .WriteLine("default:")
            .BeginIndent()
            .WriteLine("*scc = ZYDIS_SCC_NONE;")
            .WriteLine("return ZYAN_FALSE;")
            .EndBlock()
            .EndBlock();
    }
}

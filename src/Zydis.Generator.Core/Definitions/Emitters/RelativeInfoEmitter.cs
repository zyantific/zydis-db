using System;
using System.IO;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal sealed class RelativeInfoEmitter
{
    public static async Task EmitAsync(StreamWriter writer, RelativeInfoRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(registry);

        // TODO: Add something to handle indentation automatically
        writer.WriteLine("const ZydisEncoderRelInfo *ZydisGetRelInfo(ZydisMnemonic mnemonic)");
        writer.WriteLine("{");
        writer.WriteLine("    static const ZydisEncoderRelInfo info_lookup[{0}] =", registry.Infos.Count);
        writer.WriteLine("    {");

        foreach (var info in registry.Infos)
        {
            var lookup = "";
            for (var i = 0; i < info.Size.GetLength(0); ++i)
            {
                lookup += $"{{ {info.Size[i, 0]}, {info.Size[i, 1]}, {info.Size[i, 2]} }}, ";
            }
            writer.Write("        {{ {{ {0} }}, ZYDIS_SIZE_HINT_{1}, ", lookup[..^2], info.SizeHint.ToZydisString());
            WriterExtensions.WriteBool(writer, info.AcceptsBranchHints);
            writer.Write(", ");
            WriterExtensions.WriteBool(writer, info.AcceptsBound);
            writer.WriteLine(" },");
        }

        writer.WriteLine("    };");
        writer.WriteLine("");
        writer.WriteLine("    switch (mnemonic)");
        writer.WriteLine("    {");

        var info_index = 0;
        foreach (var mnemonics in registry.Mnemonics)
        {
            foreach (var mnemonic in mnemonics)
            {
                writer.WriteLine("    case ZYDIS_MNEMONIC_{0}:", mnemonic.ToUpperInvariant());
            }
            writer.WriteLine("        return &info_lookup[{0}];", info_index++);
        }

        writer.WriteLine("    default:");
        writer.WriteLine("        return ZYAN_NULL;");
        writer.WriteLine("    }");
        writer.WriteLine("}");
    }
}

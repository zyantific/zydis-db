using System;
using System.IO;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;

using static Zydis.Generator.Core.CodeGeneration.ObjectDeclaration;
using static Zydis.Generator.Core.Definitions.Builder.RelativeInfoRegistry;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal sealed class RelativeInfoEmitter
{
    public static async Task EmitAsync(StreamWriter writer, RelativeInfoRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(registry);

        var codeWriter = new CodeWriter(writer);
        codeWriter
            .WriteLine("const ZydisEncoderRelInfo *ZydisGetRelInfo(ZydisMnemonic mnemonic)")
            .BeginBlock()
            .WriteLine("static const ZydisEncoderRelInfo info_lookup[{0}] =", registry.Infos.Count)
            .BeginBlock();
        var relInfoDeclaration = new ObjectDeclaration<RelInfo>(InitializerType.Positional);
        var sizeArrayDeclaration = new ArrayObjectDeclaration(3);

        foreach (var info in registry.Infos)
        {
            var relInfoEntry = new ObjectWriter(relInfoDeclaration, null);
            var sizeEntry = new ObjectWriter(sizeArrayDeclaration, null);
            for (var i = 0; i < info.Size.GetLength(0); ++i)
            {
                sizeEntry.WriteIntegerArray(i, info.Size[i, 0], info.Size[i, 1], info.Size[i, 2]);
            }
            relInfoEntry
                .WriteObject("size", sizeEntry)
                .WriteExpression("accepts_scaling_hints", "ZYDIS_SIZE_HINT_{0}", info.SizeHint.ToZydisString())
                .WriteBool("accepts_branch_hints", info.AcceptsBranchHints)
                .WriteBool("accepts_bound", info.AcceptsBound);
            codeWriter.WriteLine("{0},", relInfoEntry.GetExpression());
        }

        codeWriter
            .EndBlock(true)
            .Newline()
            .WriteLine("switch (mnemonic)")
            .BeginBlock(false);

        var info_index = 0;
        foreach (var mnemonics in registry.Mnemonics)
        {
            foreach (var mnemonic in mnemonics)
            {
                codeWriter.WriteLine("case ZYDIS_MNEMONIC_{0}:", mnemonic.ToUpperInvariant());
            }
            codeWriter
                .BeginIndent()
                .WriteLine("return &info_lookup[{0}];", info_index++)
                .EndIndent();
        }

        codeWriter
            .WriteLine("default:")
            .BeginIndent()
            .WriteLine("return ZYAN_NULL;")
            .EndBlock()
            .EndBlock();
    }
}

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal static class FormatterStringsEmitter
{
    public static async Task EmitAsync(StreamWriter writer, FormatterStringsRegistry registry)
    {
        await writer.WriteLineAsync("#pragma pack(push, 1)\n").ConfigureAwait(false);

        var declarationWriter = DeclarationWriter.Create(writer, false);
        foreach (var definition in registry.Definitions)
        {
            var cName = definition.Name.ToUpperInvariant();
            var fullString = definition.FullString;
            Debug.Assert(fullString != null, "definition.FullString != null");
            declarationWriter
                .BeginDeclaration("static const", "ZydisShortString", $"STR_{cName}")
                .WriteInitializerZydisShortString(fullString)
                .EndDeclaration();
            await writer.WriteLineAsync().ConfigureAwait(false);

            var tokens = definition.Tokens;
            if (tokens != null && tokens.Length > 0)
            {
                // size per token:
                //  - 1 byte for type
                //  - 1 byte for length of the token
                //  - n bytes for token string contents
                //  - 1 byte for zero-termination
                var size = tokens.Length * 3 + fullString.Length;
                var initializerListWriter = declarationWriter.BeginDeclaration("static const", $@"struct ZydisPredefinedToken{cName}_
{{
  ZyanU8 size;
  ZyanU8 next;
  ZyanU8 data[{size}];
}}", $"TOK_DATA_{cName}")
                    .WriteInitializerList(false)
                    .BeginList()
                    .WriteInteger(size) // size
                    .WriteInteger(size - tokens.Last().Value.Length - 1); // next (offset to the string of the last token)
                var tokenListWriter = initializerListWriter
                    .WriteInitializerList()
                    .BeginList();
                var i = 0;
                foreach (var token in tokens)
                {
                    tokenListWriter
                        .WriteExpression(token.Type.ToZydisString()) // ZydisFormatterToken.type
                        .WriteInteger(i + 1 == tokens.Length ? 0 : token.Value.Length + 1); // ZydisFormatterToken.next
                    foreach (var ch in token.Value)
                    {
                        tokenListWriter.WriteChar(ch);
                    }
                    tokenListWriter.WriteChar('\0');
                    i++;
                }
                tokenListWriter.EndList();
                initializerListWriter.EndList();
                declarationWriter.EndDeclaration();
                await writer.WriteLineAsync().ConfigureAwait(false);
                if (!definition.SkipPointerDefinition)
                {
                    declarationWriter.BeginDeclaration("static const", "ZydisPredefinedToken* const", $"TOK_{cName}")
                        .WriteInitializerExpression($"(const ZydisPredefinedToken* const)&TOK_DATA_{cName}")
                        .EndDeclaration();
                    await writer.WriteLineAsync().ConfigureAwait(false);
                }
                await writer.WriteLineAsync().ConfigureAwait(false);
            }
        }

        await writer.WriteLineAsync("#pragma pack(pop)").ConfigureAwait(false);
    }
}

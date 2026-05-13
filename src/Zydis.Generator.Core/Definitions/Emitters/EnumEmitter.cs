using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal class EnumEmitter(string enumName, string prefix, IEnumerable<string> items)
{
    /*private string _enumName;
    private SortedSet<string> _items;

    public EnumEmitter(string enumName, IEnumerable<string> items)
    {
        _enumName = enumName;
        _items = [..items];
    }*/


    public async Task EmitDefinitionAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync($@"/**
 * Defines the `Zydis{enumName}` enum.
 */
typedef enum Zydis{enumName}_
{{").ConfigureAwait(false);
        foreach (var item in items)
        {
            await writer.WriteLineAsync($"    ZYDIS_{prefix}_{item.ToUpperInvariant()},").ConfigureAwait(false);
        }
        await writer.WriteLineAsync($@"
    /**
     * Maximum value of this enum.
     */
    ZYDIS_{prefix}_MAX_VALUE = ZYDIS_{prefix}_{items.Last().ToUpperInvariant()},
    /**
     * The minimum number of bits required to represent all values of this enum.
     */
    ZYDIS_{prefix}_REQUIRED_BITS = ZYAN_BITS_TO_REPRESENT(ZYDIS_{prefix}_MAX_VALUE)
}} Zydis{enumName};").ConfigureAwait(false);
    }

    public async Task EmitStringsAsync(StreamWriter writer, bool useInternalStringType)
    {
        var declarationWriter = new DeclarationWriter(writer, true);
        declarationWriter
            .BeginDeclaration(
                "static const",
                useInternalStringType ? "ZydisShortString" : "char*",
                $"STR_{enumName.ToUpperInvariant()}[]");
        var initializerListWriter = declarationWriter.WriteInitializerList().BeginList();
        foreach (var item in items)
        {
            if (useInternalStringType)
            {
                initializerListWriter.WriteZydisShortString(item);
            }
            else
            {
                initializerListWriter.WriteString(item);
            }
        }
        initializerListWriter.EndList();
        declarationWriter.EndDeclaration();
        await writer.WriteLineAsync().ConfigureAwait(false);
    }
}

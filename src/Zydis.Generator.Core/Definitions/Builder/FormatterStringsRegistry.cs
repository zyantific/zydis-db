using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions.Builder;

public class FormatterStringsRegistry
{
    public readonly List<FormatterStringDefinition> Definitions = [];

    public async Task ReadAsync(string filename, CancellationToken cancellationToken = default)
    {
        await foreach (var definition in DefinitionReader.ReadAsync<FormatterStringDefinition>(filename, cancellationToken).ConfigureAwait(false))
        {
            Definitions.Add(definition);
        }
    }
}

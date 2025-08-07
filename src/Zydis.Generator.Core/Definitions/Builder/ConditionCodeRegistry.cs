using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Extensions;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class ConditionCodeRegistry
{
    public record ConditionCodeInfo(string Scc, List<string> Mnemonics);

    public readonly List<ConditionCodeInfo> ConditionCodeInfos = new();

    public void Initialize(List<EncodableDefinition> definitions)
    {
        Dictionary<SourceConditionCode, List<string>> cases = new();
        foreach (var definition in definitions)
        {
            var sccFilter = definition.GetIntFilter(SelectorDefinitions.EvexScc, -1);
            if (!definition.Instruction.HasAnnotation<AnnotationApxCc>() && sccFilter == -1)
            {
                continue;
            }
            var scc = sccFilter != -1 ? Enum.GetValues<SourceConditionCode>()[1 + sccFilter] : SourceConditionCode.None;
            cases.GetOrCreate(scc).Add(definition.Instruction.Mnemonic);
        }
        var keys = cases.Keys.ToList().OrderBy(x => x.ToZydisString());
        var infos = new List<ConditionCodeInfo>();
        foreach (var key in keys)
        {
            infos.Add(new(key.ToZydisString(), [.. cases[key].Distinct().OrderBy(x => x)]));
        }
        ConditionCodeInfos.AddRange(infos.OrderBy(x => x.Mnemonics.Count));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

public sealed record InstructionDefinition
{
    public required string Mnemonic { get; init; }

    public InstructionEncoding Encoding { get; init; } = InstructionEncoding.Default;

    public OpcodeMap OpcodeMap { get; init; } = OpcodeMap.MAP0;

    [JsonConverter(typeof(HexadecimalIntConverter<byte>))]
    public required byte Opcode { get; init; }

    [JsonPropertyName("filters")]
    public IReadOnlyDictionary<string, JsonElement>? SelectorValues { get; init; }

    [JsonPropertyName("meta_info")]
    public required InstructionMetaInfo MetaInfo { get; init; }

    [JsonPropertyName("affected_flags")]
    public InstructionFlags? AffectedFlags { get; init; }

    public InstructionVexInfo? Vex { get; init; }
    public InstructionEvexInfo? Evex { get; init; }
    public InstructionMvexInfo? Mvex { get; init; }

    public OperandSizeMap OpsizeMap { get; init; }

    public AddressSizeMap AdsizeMap { get; init; }

    public IReadOnlyList<InstructionOperand>? Operands { get; init; }

    public PrefixFlags PrefixFlags { get; init; }

    public ExceptionClass? ExceptionClass { get; init; }

    public InstructionFlagsEnum Flags { get; init; }

    public int? Cpl { get; init; }

    public string? Comment { get; init; }

    public IEnumerable<InstructionOperand> AllOperands => [.. Operands ?? [], .. AffectedFlags?.GetFlagsRegisterOperands() ?? []];

    public int NumberOfOperands => AllOperands.Count();

    public int NumberOfVisibleOperands => (Operands is null) ? 0 : Operands.TakeWhile(x => x.Visibility is not OperandVisibility.Hidden).Count();

    public SelectorTableIndex? GetSelectorIndex(SelectorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return SelectorValues?.TryGetValue(definition.Name, out var value) ?? false
            ? definition.TryParseIndex(value.ValueKind is JsonValueKind.String ? value.ToString() : throw new InvalidDataException(), out var result)
                ? result
                : null
            : null;
    }

    public BranchType GetBranchType()
    {
        if (Flags.HasFlag(InstructionFlagsEnum.IsShortBranch))
        {
            return BranchType.ShortRel;
        }
        else if (Flags.HasFlag(InstructionFlagsEnum.IsNearBranch))
        {
            return BranchType.NearRel;
        }
        else if (Flags.HasFlag(InstructionFlagsEnum.IsFarBranch))
        {
            return BranchType.Far;
        }
        else if (Flags.HasFlag(InstructionFlagsEnum.IsAbsBranch))
        {
            return BranchType.Absolute;
        }
        return BranchType.None;
    }
}

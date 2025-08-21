using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

[Emittable(3, "operand_reference")]
[Emittable(6, "flags_reference")]
[Emittable(7, "requires_protected_mode")]
[Emittable(8, "no_compat_mode")]
[Emittable(9, "category")]
[Emittable(10, "isa_set")]
[Emittable(11, "isa_ext")]
[Emittable(12, "branch_type")]
[Emittable(14, "op_reg")]
[Emittable(15, "op_rm")]
[Emittable(16, "cpu_state")]
[Emittable(17, "fpu_state")]
[Emittable(18, "xmm_state")]
[Emittable(19, "accepts_segment")]
public sealed record InstructionDefinition
{
    [Emittable(0)]
    public required string Mnemonic { get; init; }

    public InstructionEncoding Encoding { get; init; } = InstructionEncoding.Default;

    public OpcodeMap OpcodeMap { get; init; } = OpcodeMap.MAP0;

    [JsonConverter(typeof(HexadecimalIntConverter<byte>))]
    public required byte Opcode { get; init; }

    [JsonPropertyName("filters")]
    public IReadOnlyDictionary<string, JsonElement>? Pattern { get; init; }

    [JsonPropertyName("meta_info")]
    public required InstructionMetaInfo MetaInfo { get; init; }

    [JsonPropertyName("affected_flags")]
    public InstructionFlags? AffectedFlags { get; init; }

    public InstructionVexInfo? Vex { get; init; }
    public InstructionEvexInfo? Evex { get; init; }
    public InstructionMvexInfo? Mvex { get; init; }

    [Emittable(4, "operand_size_map")]
    public OperandSizeMap OpsizeMap { get; init; }

    [Emittable(5, "address_size_map")]
    public AddressSizeMap AdsizeMap { get; init; }

    public IReadOnlyList<InstructionOperand>? Operands { get; init; }

    public PrefixFlags PrefixFlags { get; init; }

    [Emittable(13, "exception_class")]
    public ExceptionClass? ExceptionClass { get; init; }

    public InstructionFlagsEnum Flags { get; init; }

    public int? Cpl { get; init; }

    public string? Comment { get; init; }

    public IReadOnlyList<Annotation>? Annotations { get; init; }

    public IEnumerable<InstructionOperand> AllOperands => [.. Operands ?? [], .. AffectedFlags?.GetFlagsRegisterOperands() ?? []];

    [Emittable(1, "operand_count")]
    public int NumberOfOperands => AllOperands.Count();

    [Emittable(2, "operand_count_visible")]
    public int NumberOfVisibleOperands => (Operands is null) ? 0 : Operands.TakeWhile(x => x.Visibility is not OperandVisibility.Hidden).Count();

    public bool HasAnnotation<T>() => Annotations?.Any(x => x.GetType() == typeof(T)) ?? false;

    public T? GetAnnotation<T>() where T : Annotation => Annotations?.FirstOrDefault(x => x.GetType() == typeof(T)) as T ?? null;

    public DecisionNodeIndex? GetDecisionNodeIndex(DecisionNodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return Pattern?.TryGetValue(definition.Name, out var value) ?? false
            ? definition.ParseSlotIndex(value.ValueKind is JsonValueKind.String ? value.ToString() : throw new InvalidDataException())
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

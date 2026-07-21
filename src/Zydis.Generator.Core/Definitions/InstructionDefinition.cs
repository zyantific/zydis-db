using System;
using System.Collections.Generic;
using System.Linq;
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

    // Opcode 0x00 is a legitimate value, so it must always be written even though it equals
    // default(byte); the shared serializer options otherwise omit default values.
    [JsonConverter(typeof(HexadecimalIntConverter<byte>))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required byte Opcode { get; init; }

    [JsonPropertyName("filters")]
    [JsonConverter(typeof(FilterPatternConverter))]
    public IReadOnlyList<FilterEntry>? Pattern { get; init; }

    // Informational modrm flags with no decision-node counterpart; carried through from the XED import.
    public bool ForceModrmReg { get; init; }

    public bool ForceModrmRm { get; init; }

    public IReadOnlyList<InstructionOperand>? Operands { get; init; }

    [Emittable(4, "operand_size_map")]
    public OperandSizeMap OpsizeMap { get; init; }

    [Emittable(5, "address_size_map")]
    public AddressSizeMap AdsizeMap { get; init; }

    public int? Cpl { get; init; }

    public InstructionFlagsEnum Flags { get; init; }

    [JsonPropertyName("meta_info")]
    public required InstructionMetaInfo MetaInfo { get; init; }

    [Emittable(13, "exception_class")]
    public ExceptionClass? ExceptionClass { get; init; }

    public InstructionMvexInfo? Mvex { get; init; }

    public PrefixFlags PrefixFlags { get; init; }

    [JsonPropertyName("affected_flags")]
    public InstructionFlags? AffectedFlags { get; init; }

    public InstructionEvexInfo? Evex { get; init; }
    public InstructionVexInfo? Vex { get; init; }

    public string? Comment { get; init; }

    public IReadOnlyList<Annotation>? Annotations { get; init; }

    [JsonIgnore]
    public IEnumerable<InstructionOperand> AllOperands => [.. Operands ?? [], .. AffectedFlags?.GetFlagsRegisterOperands() ?? []];

    [Emittable(1, "operand_count")]
    [JsonIgnore]
    public int NumberOfOperands => AllOperands.Count();

    [Emittable(2, "operand_count_visible")]
    [JsonIgnore]
    public int NumberOfVisibleOperands => (Operands is null) ? 0 : Operands.TakeWhile(x => x.Visibility is not OperandVisibility.Hidden).Count();

    public bool HasAnnotation<T>() => Annotations?.Any(x => x.GetType() == typeof(T)) ?? false;

    public T? GetAnnotation<T>() where T : Annotation => Annotations?.FirstOrDefault(x => x.GetType() == typeof(T)) as T ?? null;

    public DecisionNodeIndex? GetDecisionNodeIndex(DecisionNodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return GetFilterValue(definition.Name) is { } value ? definition.ParseSlotIndex(value) : null;
    }

    /// <summary>
    /// Returns the value of the filter named <paramref name="name"/>, or <c>null</c> when absent.
    /// </summary>
    public string? GetFilterValue(string name) => Pattern?.FirstOrDefault(x => x.Filter == name)?.Value;

    public bool HasFilter(string name) => Pattern?.Any(x => x.Filter == name) ?? false;

    /// <summary>
    /// Lifts legacy in-filters <c>force_modrm_reg</c> / <c>force_modrm_rm</c> entries (bool-valued members of the
    /// old object-form "filters") into the top-level flag properties. Invoked by <see cref="Serialization.DefinitionReader"/>
    /// so the rest of the pipeline only ever sees pure filter lists.
    /// </summary>
    internal InstructionDefinition NormalizeLegacyPatternFlags()
    {
        if (Pattern is null || !Pattern.Any(x => x.Filter is "force_modrm_reg" or "force_modrm_rm"))
        {
            return this;
        }

        return this with
        {
            Pattern = Pattern.Where(x => x.Filter is not ("force_modrm_reg" or "force_modrm_rm")).ToList(),
            ForceModrmReg = ForceModrmReg || Pattern.Any(x => x.Filter is "force_modrm_reg" && x.Value is "true"),
            ForceModrmRm = ForceModrmRm || Pattern.Any(x => x.Filter is "force_modrm_rm" && x.Value is "true"),
        };
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

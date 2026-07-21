using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

/// <summary>
/// Assigns a deterministic, human-readable identifier to every instruction form (one row of
/// <c>instructions.json</c>) in a dataset. Ids are derived from the mnemonic and visible operand
/// shape first, falling back to the opcode and then to a curated set of ISA-semantic filter axes
/// only for the forms that still collide, so most ids stay short.
/// </summary>
public static class FormId
{
    /// <summary>
    /// Filter axes considered, in this order, once a form still collides after adding its opcode.
    /// Curated by enumerating every filter key seen in the live dataset - an axis absent from both
    /// this list and <see cref="ExcludedAxisKeys"/> makes <see cref="AssignIds"/> fail loudly instead
    /// of silently ignoring a key the list doesn't know about.
    /// </summary>
    private static readonly (string Key, string Tag)[] AxisOrder =
    [
        ("mode", ""),
        ("rex_2", ""),
        ("mandatory_prefix", "P"),
        ("rex_w", "W"),
        ("address_size", "A"),
        ("operand_size", "O"),
        ("vector_length", "V"),
        ("evex_u", "U"),
        ("evex_b", "B"),
        ("evex_nd", "ND"),
        ("evex_nf", "NF"),
        ("evex_scc", "SCC"),
        ("mvex_e", "E"),
        ("prefix_group1", "PG1"),
        ("modrm_reg", "REG"),
        ("modrm_rm", "RM"),
        ("modrm_mod", "MOD"),
        ("rex_b", "REXB"),
        ("feature_amd", "AMD"),
        ("feature_mpx", "MPX"),
        ("feature_knc", "KNC"),
        ("feature_cet", "CET"),
        ("feature_iprefetch", "IPREFETCH"),
        ("feature_cldemote", "CLDEMOTE"),
        ("feature_ud0_compat", "UD0COMPAT"),
        ("feature_lzcnt", "LZCNT"),
        ("feature_tzcnt", "TZCNT")
    ];

    /// <summary>
    /// Internal decoder-tree wiring hints with no ISA-semantic meaning, so they never contribute to
    /// an id and are exempt from the unknown-axis check in <see cref="ValidateAxisKeys"/>.
    /// </summary>
    private static readonly HashSet<string> ExcludedAxisKeys = ["force_modrm_reg", "force_modrm_rm", "force_modrm_mod"];

    /// <summary>
    /// Axes whose two values form a plain on/off pair, where the "off" value carries no information
    /// beyond "not the other one" and is dropped just like an absent axis. <c>mandatory_prefix</c>,
    /// <c>mode</c> and <c>modrm_mod</c> are deliberately not listed here: for those three, "axis
    /// absent" and "axis present with its seemingly-default value" are different conditions, and
    /// treating mandatory_prefix's "none" as droppable once silently reintroduced a real collision.
    /// </summary>
    private static readonly Dictionary<string, string> AxisOffValues = new()
    {
        ["rex_2"] = "no_rex2",
        ["rex_w"] = "0",
        ["evex_nd"] = "0",
        ["evex_nf"] = "0",
        ["evex_b"] = "0",
        ["mvex_e"] = "0",
        ["prefix_group1"] = "0",
        ["rex_b"] = "0",
        ["feature_mpx"] = "0",
        ["feature_amd"] = "0",
        ["feature_cldemote"] = "0",
        ["feature_cet"] = "0",
        ["feature_knc"] = "0",
        ["feature_iprefetch"] = "0",
        ["feature_ud0_compat"] = "0"
    };

    /// <summary>
    /// Assigns a unique id to every definition in <paramref name="definitions"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// A definition's <see cref="InstructionDefinition.Pattern"/> contains a filter key that is
    /// neither a curated axis nor an excluded one.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Two definitions still produce the same id after exhausting the full axis list.
    /// </exception>
    public static IReadOnlyDictionary<InstructionDefinition, string> AssignIds(IEnumerable<InstructionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var all = definitions as IReadOnlyCollection<InstructionDefinition> ?? [.. definitions];

        ValidateAxisKeys(all);

        var ids = new Dictionary<InstructionDefinition, string>(all.Count, ReferenceEqualityComparer.Instance);
        var owner = new Dictionary<string, InstructionDefinition>(all.Count);

        foreach (var baseGroup in all.GroupBy(GetBaseKey))
        {
            if (baseGroup.Count() == 1)
            {
                Claim(ids, owner, baseGroup.Key, baseGroup.Single());
                continue;
            }

            foreach (var opcodeGroup in baseGroup.GroupBy(d => $"{baseGroup.Key}_{d.OpcodeMap}_{d.Opcode:X2}"))
            {
                if (opcodeGroup.Count() == 1)
                {
                    Claim(ids, owner, opcodeGroup.Key, opcodeGroup.Single());
                    continue;
                }

                AssignByAxisCascade(ids, owner, opcodeGroup.Key, [.. opcodeGroup]);
            }
        }

        return ids;
    }

    private static void AssignByAxisCascade(
        Dictionary<InstructionDefinition, string> ids, Dictionary<string, InstructionDefinition> owner,
        string prefix, IReadOnlyList<InstructionDefinition> members)
    {
        // Only axes that actually differ across this group's members are worth a tag; this is what
        // keeps ids short instead of appending the whole axis list to every collision.
        var differingAxes = AxisOrder
            .Where(axis => members.Select(m => GetFilterValue(m, axis.Key)).Distinct().Count() > 1)
            .ToList();

        foreach (var member in members)
        {
            var suffix = string.Join("_", differingAxes.Select(axis => GetAxisTag(member, axis.Key, axis.Tag)).OfType<string>());
            var id = suffix.Length == 0 ? prefix : $"{prefix}_{suffix}";
            Claim(ids, owner, id, member);
        }
    }

    private static void Claim(
        Dictionary<InstructionDefinition, string> ids, Dictionary<string, InstructionDefinition> owner,
        string id, InstructionDefinition definition)
    {
        if (owner.TryGetValue(id, out var existing))
        {
            throw new InvalidOperationException(
                $"Form id '{id}' would be assigned to both {Describe(existing)} and {Describe(definition)}. " +
                "The curated axis list no longer distinguishes every form; it needs a new axis.");
        }

        owner.Add(id, definition);
        ids.Add(definition, id);
    }

    internal static string GetBaseKey(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var tokens = (definition.Operands ?? [])
            .Where(operand => operand.Visibility != OperandVisibility.Hidden)
            .Select(GetOperandToken);

        var body = string.Join("_", tokens);
        var key = body.Length == 0 ? definition.Mnemonic : $"{definition.Mnemonic}_{body}";

        if (definition.Encoding != InstructionEncoding.Default)
        {
            key += $"_{definition.Encoding}";
        }

        return key.ToUpperInvariant();
    }

    internal static string GetOperandToken(InstructionOperand operand)
    {
        ArgumentNullException.ThrowIfNull(operand);

        switch (operand.Type)
        {
            case OperandType.ImplicitReg:
            case OperandType.ImplicitMem:
            case OperandType.ImplicitImm1:
                // The operand type alone is uninformative for a fixed operand; the concrete register
                // (or memory base, for implicit memory operands with no fixed register) is what
                // actually distinguishes one implicit operand from another.
                if (operand.Register != Register.None)
                {
                    return operand.Register.ToString().ToLowerInvariant();
                }
                if (operand.MemoryBase is not null)
                {
                    return $"mem_{operand.MemoryBase}".ToLowerInvariant();
                }
                return operand.Type.ToString().ToLowerInvariant();

            case OperandType.GPR8: return "r8";
            case OperandType.GPR16: return "r16";
            case OperandType.GPR32: return "r32";
            case OperandType.GPR64: return "r64";
            case OperandType.GPR16_32_64: return "rv";
            case OperandType.GPR32_32_64: return "ry";
            case OperandType.GPR16_32_32: return "rz";
            case OperandType.GPRASZ: return "r_asz";
            case OperandType.FPR: return "st";
            case OperandType.MMX: return "mm";
            case OperandType.XMM: return "xmm";
            case OperandType.YMM: return "ymm";
            case OperandType.ZMM: return "zmm";
            case OperandType.TMM: return "tmm";
            case OperandType.BND: return "bnd";
            case OperandType.SREG: return "sreg";
            case OperandType.CR: return "cr";
            case OperandType.DR: return "dr";
            case OperandType.MASK: return "k";

            case OperandType.MEM:
                return (operand.Width16 == operand.Width32 && operand.Width32 == operand.Width64)
                    ? $"m{operand.Width32}"
                    : "mv";
            case OperandType.MEMVSIBX: return "vsibx";
            case OperandType.MEMVSIBY: return "vsiby";
            case OperandType.MEMVSIBZ: return "vsibz";

            case OperandType.IMM:
                return operand.Encoding switch
                {
                    OperandEncoding.Uimm8 or OperandEncoding.Simm8 => "imm8",
                    OperandEncoding.Uimm16 or OperandEncoding.Simm16 => "imm16",
                    OperandEncoding.Uimm32 or OperandEncoding.Simm32 => "imm32",
                    OperandEncoding.Uimm64 or OperandEncoding.Simm64 => "imm64",
                    OperandEncoding.Uimm16_32_64 or OperandEncoding.Simm16_32_64 => "immv",
                    OperandEncoding.Uimm32_32_64 or OperandEncoding.Simm32_32_64 => "immy",
                    OperandEncoding.Uimm16_32_32 or OperandEncoding.Simm16_32_32 => "immz",
                    _ => "imm"
                };

            case OperandType.REL:
                return operand.Encoding switch
                {
                    OperandEncoding.Jimm8 => "rel8",
                    OperandEncoding.Jimm16 => "rel16",
                    OperandEncoding.Jimm32 => "rel32",
                    OperandEncoding.Jimm64 => "rel64",
                    OperandEncoding.Jimm16_32_64 => "relv",
                    OperandEncoding.Jimm32_32_64 => "rely",
                    OperandEncoding.Jimm16_32_32 => "relz",
                    _ => "rel"
                };

            case OperandType.ABS: return "abs";
            case OperandType.PTR: return "ptr";
            case OperandType.AGEN: return "agen";
            case OperandType.AGENNoRel: return "agen";
            case OperandType.MOFFS: return "moffs";
            case OperandType.MIB: return "mib";
            case OperandType.DFV: return "dfv";
            default: return operand.Type.ToString().ToLowerInvariant();
        }
    }

    private static void ValidateAxisKeys(IEnumerable<InstructionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (definition.Pattern is null)
            {
                continue;
            }

            foreach (var key in definition.Pattern.Keys)
            {
                if (ExcludedAxisKeys.Contains(key) || AxisOrder.Any(axis => axis.Key == key))
                {
                    continue;
                }

                throw new NotSupportedException(
                    $"Unknown filter key '{key}' on {Describe(definition)}. Add it to the curated axis " +
                    $"list in {nameof(FormId)}, or to the excluded set if it carries no ISA-semantic meaning.");
            }
        }
    }

    private static string? GetFilterValue(InstructionDefinition definition, string axisKey)
    {
        return definition.Pattern is not null && definition.Pattern.TryGetValue(axisKey, out var value)
            ? value.ToString()
            : null;
    }

    private static string? GetAxisTag(InstructionDefinition definition, string axisKey, string prefix)
    {
        var value = GetFilterValue(definition, axisKey);
        if (value is null || (AxisOffValues.TryGetValue(axisKey, out var off) && value == off))
        {
            return null;
        }

        var sanitized = value.Replace("!", "NOT", StringComparison.Ordinal).ToUpperInvariant();
        return prefix.Length == 0 ? sanitized : $"{prefix}{sanitized}";
    }

    private static string Describe(InstructionDefinition definition)
    {
        var filters = definition.Pattern is { Count: > 0 }
            ? string.Join(';', definition.Pattern
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"))
            : "none";

        return $"{definition.Mnemonic.ToUpperInvariant()} (encoding={definition.Encoding}, opcode={definition.OpcodeMap}/0x{definition.Opcode:X2}, filters=[{filters}])";
    }
}

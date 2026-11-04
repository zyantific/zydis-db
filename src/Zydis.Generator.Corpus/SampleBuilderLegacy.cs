using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Builds the samples of the legacy encoding, REX2 included, and of the 3DNow encoding.
/// </summary>
internal static class SampleBuilderLegacy
{
    private static readonly bool[] Booleans = [false, true];

    // F3 before F2, which is the slot order of mandatory_prefix and gives a bare prefix_group1=1 step an F3.
    private static readonly byte[] Group1Prefixes = [0x00, 0xF3, 0xF2];

    public static SampleBuildResult Build(DecoderTreePath path, ModeNode.Slot mode, int id, string formId)
    {
        var steps = path.Steps;
        var definition = path.Definition;
        var is3DNow = path.Encoding is InstructionEncoding.AMD3DNOW;

        if (SampleBuilder.FindArchitecturalConflict(steps, mode, hasRexPrefixes: true) is { } impossible)
        {
            return new Impossible(impossible);
        }

        if (steps.FirstOrDefault(step => !IsLegacyStep(step)) is { } foreign)
        {
            return new Unencodable($"{DecoderTreePath.FormatStep(foreign)} does not apply to the legacy encoding");
        }

        if (!SampleBuilder.TrySelectPrefixState(steps, mode, LegacyPrefixStates(mode), out var prefixes,
                out var prefixFailure))
        {
            return new Unencodable(prefixFailure);
        }

        if (prefixes.Rex2 && (path.Map is not (OpcodeMap.MAP0 or OpcodeMap.M0F)))
        {
            return new Unencodable($"REX2 has no {CorpusTags.MapTag(path.Map)} table");
        }

        // A 66 that the definition consumes as its mandatory prefix does not override the operand size. No 3DNow
        // definition consumes one.
        var consumes66 = !is3DNow && (prefixes.MandatoryCandidate == 0x66) && ConsumesMandatoryPrefix(definition);
        var operandSizePrefix = prefixes.OperandSize66 && !consumes66;

        var (osz, asz) = EffectiveSizes.Compute(mode, operandSizePrefix, prefixes.AddressSize67, prefixes.W,
            definition.OpsizeMap, definition.AdsizeMap);
        var operandWidth = EffectiveSizes.ComputeOperandWidth(mode, operandSizePrefix, prefixes.W,
            definition.OpsizeMap);

        var hasModrmEncoding = SampleBuilder.HasModrmEncoding(definition);
        var hasModrmStep = steps.Any(step => step.Node is SampleBuilder.ModrmModAxis or
            SampleBuilder.ModrmModCompactAxis or SampleBuilder.ModrmRegAxis or SampleBuilder.ModrmRmAxis);

        // The decoder reads the ModRM byte of every 3DNow instruction through the definition at slot 0C of the 3DNow
        // table, before the suffix opcode selects the actual definition.
        SampleBuilder.Modrm? modrm = null;
        if (hasModrmEncoding || hasModrmStep || is3DNow)
        {
            if (!SampleBuilder.TryChooseModrm(steps, definition, SampleBuilder.AssignRegisters(definition), asz,
                    out var chosen, out var modrmFailure))
            {
                return new Unencodable(modrmFailure);
            }

            modrm = chosen;
        }

        var bytes = new List<byte>();
        AppendLegacyPrefixes(bytes, prefixes);
        AppendRexPrefix(bytes, prefixes, path.Map);

        if (!prefixes.Rex2)
        {
            bytes.AddRange(EscapeBytes(path.Map));
        }

        var readsAddressing = (hasModrmEncoding || is3DNow) &&
            !definition.Flags.HasFlag(InstructionFlagsEnum.ForceRegForm);

        if (!is3DNow)
        {
            bytes.Add(path.Opcode);
        }

        if (modrm is { } fields)
        {
            SampleBuilder.AppendModrm(bytes, fields, asz, readsAddressing);
        }

        // The 3DNow opcode is a suffix that follows the ModRM, SIB and displacement bytes.
        if (is3DNow)
        {
            bytes.Add(path.Opcode);
        }

        if (!SampleBuilder.TryAppendImmediates(bytes, definition, operandWidth, asz, out var immediateFailure))
        {
            return new Unencodable(immediateFailure);
        }

        var decoderModes = SampleBuilder.GetDecoderModes(steps, prefixes.Rex2);

        return new Built(SampleBuilder.CreateSample(path, mode, bytes, decoderModes, osz, asz, id, formId));
    }

    /// <summary>
    /// The legacy prefix, REX and REX2 combinations a legacy or 3DNow sample can carry in <paramref name="mode"/>,
    /// fewest bytes first.
    /// </summary>
    private static IReadOnlyList<SampleBuilder.PrefixState> LegacyPrefixStates(ModeNode.Slot mode)
    {
        var states = new List<SampleBuilder.PrefixState>();

        foreach (var rex2 in Booleans)
        {
            foreach (var w in Booleans)
            {
                foreach (var b in Booleans)
                {
                    if ((rex2 || w || b) && (mode is not ModeNode.Slot.M64))
                    {
                        continue;
                    }

                    foreach (var addressSize67 in Booleans)
                    {
                        foreach (var group1 in Group1Prefixes)
                        {
                            foreach (var operandSize66 in Booleans)
                            {
                                states.Add(new SampleBuilder.PrefixState(group1, operandSize66, addressSize67, w, b,
                                    rex2));
                            }
                        }
                    }
                }
            }
        }

        // OrderBy is stable, so states of equal size keep the enumeration order, which puts the lowest slot of each
        // axis first.
        return [.. states.OrderBy(state => state.ByteCount)];
    }

    private static void AppendLegacyPrefixes(List<byte> bytes, SampleBuilder.PrefixState state)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (state.Group1 != 0)
        {
            bytes.Add(state.Group1);
        }

        if (state.OperandSize66)
        {
            bytes.Add(0x66);
        }

        if (state.AddressSize67)
        {
            bytes.Add(0x67);
        }
    }

    /// <summary>
    /// Appends the REX prefix when W or B is set, or the REX2 prefix for <paramref name="state"/>.
    /// </summary>
    /// <param name="bytes">The sample bytes.</param>
    /// <param name="state">The prefix state.</param>
    /// <param name="map">
    /// The opcode map of the table. REX2 has no escape bytes, its <c>M0</c> bit selects the <c>0F</c> table instead.
    /// </param>
    private static void AppendRexPrefix(List<byte> bytes, SampleBuilder.PrefixState state, OpcodeMap map)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var w = state.W ? 1 : 0;
        var b = state.B ? 1 : 0;

        if (state.Rex2)
        {
            var m0 = (map is OpcodeMap.M0F) ? 1 : 0;
            bytes.Add(0xD5);
            bytes.Add((byte)((m0 << 7) | (w << 3) | b));
            return;
        }

        if ((w | b) != 0)
        {
            bytes.Add((byte)(0x40 | (w << 3) | b));
        }
    }

    private static bool IsLegacyStep(PathStep step)
    {
        if (step.Node.StartsWith(SampleBuilder.SwitchTableAxisPrefix, StringComparison.Ordinal))
        {
            return step.Slots.Contains(SampleBuilder.SwitchTableDefaultSlot);
        }

        return step.Node.StartsWith(SampleBuilder.FeatureAxisPrefix, StringComparison.Ordinal) || step.Node is
            SampleBuilder.AddressSizeAxis or
            SampleBuilder.MandatoryPrefixAxis or
            SampleBuilder.ModeAxis or
            SampleBuilder.ModeCompactAxis or
            SampleBuilder.ModrmModAxis or
            SampleBuilder.ModrmModCompactAxis or
            SampleBuilder.ModrmRegAxis or
            SampleBuilder.ModrmRmAxis or
            SampleBuilder.OperandSizeAxis or
            SampleBuilder.PrefixGroup1Axis or
            SampleBuilder.Rex2Axis or
            SampleBuilder.RexBAxis or
            SampleBuilder.RexWAxis;
    }

    private static byte[] EscapeBytes(OpcodeMap map)
    {
        return map switch
        {
            OpcodeMap.MAP0 => [],
            OpcodeMap.M0F => [0x0F],
            OpcodeMap.M0F38 => [0x0F, 0x38],
            OpcodeMap.M0F3A => [0x0F, 0x3A],
            OpcodeMap.M0F0F => [0x0F, 0x0F],
            _ => throw new ArgumentOutOfRangeException(nameof(map), map, null)
        };
    }

    /// <summary>
    /// <see langword="true"/> if the decoder consumes a mandatory-prefix candidate for <paramref name="definition"/>,
    /// which the generator encodes from the raw <c>mandatory_prefix</c> filter.
    /// </summary>
    private static bool ConsumesMandatoryPrefix(InstructionDefinition definition)
    {
        if (!(definition.Pattern?.TryGetValue(SampleBuilder.MandatoryPrefixAxis, out var value) ?? false))
        {
            return false;
        }

        return value.GetString() switch
        {
            "none" or "ignore" => false,
            "66" or "f3" or "f2" => true,
            // A negated prefix decodes for every candidate but the named one and consumes none.
            { } negated when negated.StartsWith('!') => false,
            var unknown => throw new InvalidOperationException(
                $"Invalid value '{unknown}' for the {SampleBuilder.MandatoryPrefixAxis} filter of " +
                $"'{definition.Mnemonic}'.")
        };
    }
}

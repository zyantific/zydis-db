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
    // F3 before F2, which is the slot order of mandatory_prefix and gives a bare prefix_group1=1 step an F3.
    private static readonly byte[] Group1Prefixes = [0x00, 0xF3, 0xF2];

    // Static initializers run in textual order, so these two follow the Group1Prefixes they read.
    private static readonly SampleBuilder.PrefixState[] PrefixStates64 = CreateLegacyPrefixStates(is64: true);
    private static readonly SampleBuilder.PrefixState[] PrefixStatesNot64 = CreateLegacyPrefixStates(is64: false);

    public static SampleBuildResult Build(DecoderTreePath path, ModeNode.Slot mode)
    {
        var steps = path.Steps;
        var definition = path.Definition;
        var is3DNow = path.Encoding is InstructionEncoding.AMD3DNOW;

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

        // A 66 that the definition consumes as its mandatory prefix does not override the operand size.
        var consumes66 = (prefixes.MandatoryCandidate == 0x66) && ConsumesMandatoryPrefix(definition);
        var operandSizePrefix = prefixes.OperandSize66 && !consumes66;

        var (osz, asz, operandWidth) = EffectiveSizes.Compute(mode, operandSizePrefix, prefixes.AddressSize67,
            prefixes.W, definition.OpsizeMap, definition.AdsizeMap);

        // The decoder reads the ModRM byte of every 3DNow instruction through the definition at slot 0C of the 3DNow
        // table, before the suffix opcode selects the actual definition.
        if (!SampleBuilder.TryChooseModrm(steps, definition, SampleBuilder.AssignRegisters(definition), asz,
                forcesModrm: is3DNow, out var modrm, out var modrmFailure))
        {
            return new Unencodable(modrmFailure);
        }

        var bytes = new List<byte>();
        AppendLegacyPrefixes(bytes, prefixes);
        AppendRexPrefix(bytes, prefixes, path.Map);

        if (!prefixes.Rex2)
        {
            bytes.AddRange(EscapeBytes(path.Map));
        }

        if (!is3DNow)
        {
            bytes.Add(path.Opcode);
        }

        if (modrm is { } fields)
        {
            SampleBuilder.AppendModrm(bytes, fields, asz);
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

        var decoderModes = SampleBuilder.GetDecoderModes(steps,
            prefixes.Rex2 ? SampleBuilder.ApxRequirement.Required : SampleBuilder.ApxRequirement.None);

        return new Built(bytes, decoderModes, osz, asz);
    }

    /// <summary>
    /// The legacy prefix, REX and REX2 combinations a legacy or 3DNow sample can carry in <paramref name="mode"/>,
    /// fewest bytes first.
    /// </summary>
    private static SampleBuilder.PrefixState[] LegacyPrefixStates(ModeNode.Slot mode)
    {
        return (mode is ModeNode.Slot.M64) ? PrefixStates64 : PrefixStatesNot64;
    }

    private static SampleBuilder.PrefixState[] CreateLegacyPrefixStates(bool is64)
    {
        // OrderBy is stable, so states of equal size keep the enumeration order, which puts the lowest slot of each
        // axis first.
        return [.. (from rex2 in SampleBuilder.Booleans
                    from w in SampleBuilder.Booleans
                    from b in SampleBuilder.Booleans
                    where is64 || !(rex2 || w || b)
                    from addressSize67 in SampleBuilder.Booleans
                    from group1 in Group1Prefixes
                    from operandSize66 in SampleBuilder.Booleans
                    select new SampleBuilder.PrefixState(group1, operandSize66, addressSize67, w, b, rex2))
                .OrderBy(state => state.ByteCount)];
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
            bytes.Add(SampleBuilder.AddressSizePrefix);
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

        return SampleBuilder.IsSharedStep(step) || step.Node is
            SampleBuilder.MandatoryPrefixAxis or
            SampleBuilder.PrefixGroup1Axis or
            SampleBuilder.Rex2Axis or
            SampleBuilder.RexBAxis;
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
        var mandatoryPrefix = MandatoryPrefixNode.NodeDefinition.Instance;

        // A negated prefix decodes for every candidate but the named one and consumes none.
        return (definition.GetDecisionNodeIndex(mandatoryPrefix) is { IsNegated: false, Index: var index }) &&
            (mandatoryPrefix.GetSlotName(index) is "66" or "f3" or "f2");
    }
}

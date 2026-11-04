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
            return new Unencodable($"{SampleBuilder.FormatStep(foreign)} does not apply to the legacy encoding");
        }

        if (!SampleBuilder.TrySelectPrefixState(steps, mode, SampleBuilder.LegacyPrefixStates(mode), out var prefixes,
                out var prefixFailure))
        {
            return new Unencodable(prefixFailure);
        }

        if (prefixes.Rex2 && (path.Map is not (OpcodeMap.MAP0 or OpcodeMap.M0F)))
        {
            return new Unencodable($"REX2 has no {SampleBuilder.MapTag(path.Map)} table");
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
        SampleBuilder.AppendLegacyPrefixes(bytes, prefixes);
        SampleBuilder.AppendRexPrefix(bytes, prefixes, path.Map);

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

        return value.GetString() is "66" or "f3" or "f2";
    }
}

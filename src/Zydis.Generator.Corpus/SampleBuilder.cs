using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Corpus;

/// <summary>
/// Builds the bytes of one decoder-tree leaf in one machine mode. The encoding-specific payloads live in the
/// <c>SampleBuilder*</c> classes, the parts they share live here. The rules follow the decoder in Zydis
/// <c>src/Decoder.c</c>.
/// </summary>
internal static class SampleBuilder
{
    internal const string AddressSizeAxis = "address_size";
    internal const string EvexUAxis = "evex_u";
    internal const string FeatureAxisPrefix = "feature_";
    internal const string MandatoryPrefixAxis = "mandatory_prefix";
    internal const string ModeAxis = "mode";
    internal const string ModeCompactAxis = "mode_compact";
    internal const string ModrmModAxis = "modrm_mod";
    internal const string ModrmModCompactAxis = "modrm_mod_compact";
    internal const string ModrmRegAxis = "modrm_reg";
    internal const string ModrmRmAxis = "modrm_rm";
    internal const string OperandSizeAxis = "operand_size";
    internal const string PrefixGroup1Axis = "prefix_group1";
    internal const string Rex2Axis = "rex_2";
    internal const string RexBAxis = "rex_b";
    internal const string RexWAxis = "rex_w";
    internal const string SwitchTableAxisPrefix = "switch_table_";
    internal const string SwitchTableDefaultSlot = "default";
    internal const string VectorLengthAxis = "vector_length";

    internal const byte AddressSizePrefix = 0x67;

    private static readonly int[] AllModrmFieldValues = [0, 1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] RegisterFormMods = [3];
    private static readonly int[] MemoryFormMods = [0, 1, 2];

    // The rm values of an `else` or multi-slot step in preference order. Under a 32-bit or 64-bit address size
    // `rm=4` adds a SIB byte and `rm=5` with `mod=0` is disp32 or RIP-relative, under a 16-bit address size `rm=6`
    // with `mod=0` is a bare disp16, so these come last.
    private static readonly int[] MemoryRmOrder = [0, 1, 2, 3, 6, 7, 4, 5];
    private static readonly int[] MemoryRmOrder16 = [0, 1, 2, 3, 4, 5, 7, 6];

    internal static readonly bool[] Booleans = [false, true];

    /// <summary>
    /// The prefix states of the encodings that carry W and the refining prefix in their payload: EVEX, MVEX, VEX and
    /// XOP.
    /// </summary>
    /// <remarks>
    /// A 66, F2 or F3 before 62, 8F or C4 makes the decoder fail with ZYDIS_STATUS_ILLEGAL_LEGACY_PFX, so the states
    /// with a 66 come last and only serve to name the operand_size step that needs one. The pp field replaces the
    /// refining prefix bytes.
    /// </remarks>
    private static readonly PrefixState[] PayloadPrefixStates =
    [
        .. from operandSize66 in Booleans
           from addressSize67 in Booleans
           from w in Booleans
           select new PrefixState(0x00, operandSize66, addressSize67, w, B: false, Rex2: false)
    ];

    /// <summary>
    /// Builds the sample of <paramref name="path"/> in <paramref name="mode"/>.
    /// </summary>
    public static SampleBuildResult Build(DecoderTreePath path, ModeNode.Slot mode)
    {
        ArgumentNullException.ThrowIfNull(path);

        var hasRexPrefixes = path.Encoding is InstructionEncoding.Default or InstructionEncoding.AMD3DNOW;
        if (FindArchitecturalConflict(path.Steps, mode, hasRexPrefixes) is { } impossible)
        {
            return new Impossible(impossible);
        }

        return path.Encoding switch
        {
            InstructionEncoding.Default or InstructionEncoding.AMD3DNOW => SampleBuilderLegacy.Build(path, mode),
            InstructionEncoding.VEX => SampleBuilderVex.BuildVex(path, mode),
            InstructionEncoding.XOP => SampleBuilderVex.BuildXop(path, mode),
            InstructionEncoding.EVEX => SampleBuilderEvex.BuildEvex(path, mode),
            InstructionEncoding.MVEX => SampleBuilderMvex.BuildMvex(path, mode),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path.Encoding, null)
        };
    }

    public static CorpusSample CreateSample(DecoderTreePath path, ModeNode.Slot mode, Built built, int id,
        string formId)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(built);
        ArgumentNullException.ThrowIfNull(formId);

        return new CorpusSample(
            Convert.ToHexString([.. built.Bytes]),
            mode,
            built.DecoderModes.Enable,
            built.DecoderModes.Disable,
            path.Encoding,
            path.Map,
            path.Opcode,
            built.Osz,
            built.Asz,
            id,
            formId,
            path.Definition.Mnemonic,
            path.PathString);
    }

    /// <summary>
    /// Returns the first step whose every slot the architecture or the decoder's routing rules out in
    /// <paramref name="mode"/>.
    /// </summary>
    /// <param name="steps">The path steps.</param>
    /// <param name="mode">The machine mode.</param>
    /// <param name="hasRexPrefixes">
    /// <see langword="true"/> for the encodings whose W and B bits come from a REX or REX2 prefix, which only 64-bit
    /// mode has. VEX, XOP, EVEX and MVEX carry them in their payload in every mode.
    /// </param>
    /// <returns>The reason for the report, or <see langword="null"/> if the mode can reach every step.</returns>
    private static string? FindArchitecturalConflict(IReadOnlyList<PathStep> steps, ModeNode.Slot mode,
        bool hasRexPrefixes)
    {
        var is64 = mode is ModeNode.Slot.M64;

        foreach (var step in steps)
        {
            // ZydisNodeHandlerMandatoryPrefix yields the indices of none, 66, f3 and f2 only, so no byte sequence
            // reaches a leaf behind slot 0.
            if ((step.Node == MandatoryPrefixAxis) && step.Slots.All(slot => slot == "ignore"))
            {
                return "mandatory_prefix slot ignore is never routed";
            }

            var isImpossible = step.Node switch
            {
                RexWAxis or RexBAxis => hasRexPrefixes && !is64 && step.Slots.All(slot => slot == "1"),
                Rex2Axis => hasRexPrefixes && !is64 && step.Slots.All(slot => slot == "rex2"),
                AddressSizeAxis => step.Slots.All(slot => slot == (is64 ? "16" : "64")),
                OperandSizeAxis => !is64 && step.Slots.All(slot => slot == "64"),
                _ => false
            };

            if (isImpossible)
            {
                return $"{DecoderTreePath.FormatStep(step)} in {CorpusTags.ModeTag(mode)}-bit mode";
            }
        }

        return null;
    }

    /// <summary>
    /// The slot a prefix axis routes to for the given prefixes, or <see langword="null"/> for an axis no prefix
    /// decides.
    /// </summary>
    public static string? RoutePrefixAxis(string axis, PrefixState state, ModeNode.Slot mode)
    {
        return axis switch
        {
            // ZydisNodeHandlerMandatoryPrefix routes by the candidate, before the definition decides whether it
            // consumes the prefix.
            MandatoryPrefixAxis => state.MandatoryCandidate switch
            {
                0x66 => "66",
                0xF3 => "f3",
                0xF2 => "f2",
                _ => "none"
            },

            // ZydisNodeHandlerOperandSize routes by the mode, a 66 byte and W, before the size map applies.
            OperandSizeAxis => mode switch
            {
                ModeNode.Slot.M64 when state.W => "64",
                ModeNode.Slot.M16 => state.OperandSize66 ? "32" : "16",
                _ => state.OperandSize66 ? "16" : "32"
            },

            AddressSizeAxis => mode switch
            {
                ModeNode.Slot.M16 => state.AddressSize67 ? "32" : "16",
                ModeNode.Slot.M32 => state.AddressSize67 ? "16" : "32",
                _ => state.AddressSize67 ? "32" : "64"
            },

            PrefixGroup1Axis => (state.Group1 != 0) ? "1" : "0",
            RexWAxis => state.W ? "1" : "0",
            RexBAxis => state.B ? "1" : "0",
            Rex2Axis => state.Rex2 ? "rex2" : "no_rex2",
            _ => null
        };
    }

    /// <summary>
    /// Selects the first of <paramref name="candidates"/> that routes every prefix step of the path to one of its
    /// slots.
    /// </summary>
    /// <returns><see langword="true"/> if a candidate satisfies every prefix step.</returns>
    public static bool TrySelectPrefixState(IReadOnlyList<PathStep> steps, ModeNode.Slot mode,
        IReadOnlyList<PrefixState> candidates, out PrefixState selected, out string failure)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(candidates);

        var prefixSteps = steps.Where(step => RoutePrefixAxis(step.Node, default, mode) is not null).ToArray();

        foreach (var candidate in candidates)
        {
            if (prefixSteps.All(step => Routes(step, candidate, mode)))
            {
                selected = candidate;
                failure = string.Empty;
                return true;
            }
        }

        selected = default;
        failure = DiagnosePrefixConflict(prefixSteps, mode, candidates);
        return false;
    }

    /// <summary>
    /// Selects the refining prefix and the prefix state of an encoding that carries W and the refining prefix in its
    /// payload.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the table has a refining prefix and a state without a <c>66</c> byte serves every
    /// prefix step.
    /// </returns>
    internal static bool TrySelectPayloadPrefixes(DecoderTreePath path, ModeNode.Slot mode,
        out RefiningPrefix refiningPrefix, out PrefixState prefixes, out string failure)
    {
        ArgumentNullException.ThrowIfNull(path);

        var encoding = CorpusTags.EncodingTag(path.Encoding);
        prefixes = default;

        if (path.Prefix is not { } prefix)
        {
            refiningPrefix = default;
            failure = $"the {encoding} table of {CorpusTags.MapTag(path.Map)} has no refining prefix";
            return false;
        }

        refiningPrefix = prefix;

        if (!TrySelectPrefixState(path.Steps, mode, PayloadPrefixStates, out prefixes, out failure))
        {
            return false;
        }

        if (prefixes.OperandSize66)
        {
            var operandSize = path.Steps.First(step => step.Node == OperandSizeAxis);
            failure = $"{DecoderTreePath.FormatStep(operandSize)} needs a 66 prefix, which {encoding} does not allow";
            return false;
        }

        return true;
    }

    private static bool Routes(PathStep step, PrefixState state, ModeNode.Slot mode)
    {
        return step.Slots.Contains(RoutePrefixAxis(step.Node, state, mode));
    }

    private static string DiagnosePrefixConflict(PathStep[] prefixSteps, ModeNode.Slot mode,
        IReadOnlyList<PrefixState> candidates)
    {
        var modeText = $"in {CorpusTags.ModeTag(mode)}-bit mode";

        foreach (var step in prefixSteps)
        {
            if (!candidates.Any(state => Routes(step, state, mode)))
            {
                return $"{DecoderTreePath.FormatStep(step)} has no encodable slot {modeText}";
            }
        }

        for (var i = 0; i < prefixSteps.Length; i++)
        {
            for (var j = i + 1; j < prefixSteps.Length; j++)
            {
                var (first, second) = (prefixSteps[i], prefixSteps[j]);
                if (!candidates.Any(state => Routes(first, state, mode) && Routes(second, state, mode)))
                {
                    return $"{DecoderTreePath.FormatStep(first)} contradicts " +
                        $"{DecoderTreePath.FormatStep(second)} {modeText}";
                }
            }
        }

        return $"{string.Join(", ", prefixSteps.Select(DecoderTreePath.FormatStep))} contradict each other {modeText}";
    }

    /// <summary>
    /// Chooses the first slot of <paramref name="preference"/> that every step of <paramref name="axes"/> serves. An
    /// axis the path leaves open takes the first slot.
    /// </summary>
    /// <param name="encoding">The encoding tag the failure reason names.</param>
    internal static bool TryChooseSlot(IReadOnlyList<PathStep> steps, string[] axes, string[] preference,
        string encoding, out string slot, out string failure)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var allowed = new HashSet<string>(preference, StringComparer.Ordinal);
        PathStep? first = null;

        foreach (var step in steps.Where(step => axes.Contains(step.Node)))
        {
            allowed.IntersectWith(step.Slots);
            if (allowed.Count == 0)
            {
                slot = string.Empty;
                failure = ((first is null) || !preference.Any(step.Slots.Contains))
                    ? $"{DecoderTreePath.FormatStep(step)} has no {encoding} encoding"
                    : $"{DecoderTreePath.FormatStep(first)} contradicts {DecoderTreePath.FormatStep(step)}";
                return false;
            }

            first ??= step;
        }

        slot = preference.First(allowed.Contains);
        failure = string.Empty;
        return true;
    }

    /// <summary>
    /// The decoder modes the path depends on. A <c>feature_*</c> step takes slot <c>0</c> when it serves it, which
    /// needs no mode change for most features, and still names the mode so the checker pins it.
    /// </summary>
    /// <param name="steps">The path steps.</param>
    /// <param name="apx">How the sample depends on APX.</param>
    /// <param name="requiresKnc">
    /// <see langword="true"/> if the sample decodes only in a build with KNC support, which puts <c>knc</c> on the
    /// enable list.
    /// </param>
    public static DecoderModes GetDecoderModes(IReadOnlyList<PathStep> steps, ApxRequirement apx = ApxRequirement.None,
        bool requiresKnc = false)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var enable = new SortedSet<string>(StringComparer.Ordinal);
        var disable = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var step in steps.Where(step => step.Node.StartsWith(FeatureAxisPrefix, StringComparison.Ordinal)))
        {
            var feature = step.Node[FeatureAxisPrefix.Length..];
            var decoderMode = (feature == "amd") ? "amd_branches" : feature;

            if (step.Slots.Contains("0"))
            {
                disable.Add(decoderMode);
            }
            else
            {
                enable.Add(decoderMode);
            }
        }

        switch (apx)
        {
            case ApxRequirement.Forbidden:
                disable.Add("apx");
                break;
            case ApxRequirement.Required:
                // APX is on by default. Naming it keeps a changed default from silently breaking the APX samples.
                enable.Add("apx");
                break;
        }

        if (requiresKnc)
        {
            enable.Add("knc");
        }

        return new DecoderModes(Join(enable), Join(disable));

        static string Join(SortedSet<string> modes) => (modes.Count == 0) ? "-" : string.Join(',', modes);
    }

    /// <summary>
    /// Assigns each register operand of <paramref name="definition"/> the lowest valid index of its class, in operand
    /// order, with the next index for each further operand of the same class.
    /// </summary>
    public static RegisterAssignment AssignRegisters(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var next = new Dictionary<RegisterKind, int>();
        int? reg = null;
        int? rm = null;
        int? vvvv = null;
        int? is4 = null;
        var hasVsib = false;

        foreach (var operand in definition.Operands ?? [])
        {
            hasVsib |= operand.Type is OperandType.MEMVSIBX or OperandType.MEMVSIBY or OperandType.MEMVSIBZ;

            var registerKind = GetRegisterKind(operand.Type);
            if (registerKind is RegisterKind.INVALID)
            {
                continue;
            }

            var index = next.GetValueOrDefault(registerKind, FirstRegisterIndex(registerKind));

            switch (operand.Encoding)
            {
                case OperandEncoding.ModrmReg:
                    reg = index;
                    break;
                case OperandEncoding.ModrmRm:
                    rm = index;
                    break;
                case OperandEncoding.NDSNDD:
                    vvvv = index;
                    break;
                case OperandEncoding.IS4:
                    is4 = index;
                    break;
                default:
                    continue;
            }

            next[registerKind] = index + 1;
        }

        int? vsibIndex = hasVsib ? next.GetValueOrDefault(RegisterKind.VR, FirstRegisterIndex(RegisterKind.VR)) : null;

        return new RegisterAssignment(reg, rm, vvvv, is4, vsibIndex);
    }

    /// <summary>
    /// <see langword="true"/> if the decoder reads a ModRM byte for <paramref name="definition"/>, by the rule the
    /// generator applies to the encoding info of the definition.
    /// </summary>
    private static bool HasModrmEncoding(InstructionDefinition definition)
    {
        var pattern = definition.Pattern;
        if ((pattern is not null) && (pattern.ContainsKey(ModrmModAxis) || pattern.ContainsKey(ModrmRegAxis) ||
                pattern.ContainsKey(ModrmRmAxis)))
        {
            return true;
        }

        return (definition.Operands ?? [])
            .Any(operand => operand.Encoding is OperandEncoding.ModrmReg or OperandEncoding.ModrmRm);
    }

    /// <summary>
    /// Chooses the ModRM fields when the decoder reads a ModRM byte for the path, which it does for a definition with
    /// a ModRM in its encoding info and for a path through a ModRM or <c>evex_u</c> filter.
    /// </summary>
    /// <param name="forcesModrm">
    /// <see langword="true"/> if the decoder reads a ModRM byte with its addressing form, whatever the encoding info of
    /// the definition says.
    /// </param>
    /// <param name="modrm">The ModRM fields, or <see langword="null"/> if the decoder reads no ModRM byte.</param>
    /// <param name="failure">The reason the path leaves no ModRM byte the sample can carry.</param>
    /// <returns>
    /// <see langword="true"/> if every ModRM step has a slot the fields can take and a VSIB operand gets its SIB form.
    /// </returns>
    public static bool TryChooseModrm(IReadOnlyList<PathStep> steps, InstructionDefinition definition,
        RegisterAssignment registers, int addressWidth, bool forcesModrm, out Modrm? modrm, out string failure)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registers);

        modrm = null;

        var hasModrmEncoding = forcesModrm || HasModrmEncoding(definition);

        // ZydisNodeHandlerEvexU reads the ModRM byte itself, because U means X4 on a memory form under APX.
        if (hasModrmEncoding || steps.Any(step => IsModrmAxis(step.Node) || (step.Node == EvexUAxis)))
        {
            if (!TryChooseModrmFields(steps, definition, registers, addressWidth, hasModrmEncoding, out var fields,
                    out failure))
            {
                return false;
            }

            modrm = fields;
        }

        // The decoder reads the VSIB index from a SIB byte, which needs rm=4 and a memory form.
        if ((registers.VsibIndex is not null) && modrm is not { Mod: not 3, Rm: 4 })
        {
            failure = "the ModRM steps exclude the SIB form a VSIB operand needs";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    /// <summary>
    /// Chooses the ModRM fields from the path slots and the register assignment. A <c>modrm_mod</c> axis the path
    /// leaves open takes the register form unless an operand in <c>rm</c> is a memory operand.
    /// </summary>
    /// <returns><see langword="true"/> if every ModRM step has a slot the fields can take.</returns>
    private static bool TryChooseModrmFields(IReadOnlyList<PathStep> steps, InstructionDefinition definition,
        RegisterAssignment registers, int addressWidth, bool hasModrmEncoding, out Modrm modrm, out string failure)
    {
        modrm = default;

        var mods = FieldValues(steps, ModrmModAxis);
        var regs = FieldValues(steps, ModrmRegAxis);
        var rms = FieldValues(steps, ModrmRmAxis);

        foreach (var step in steps.Where(step => step.Node == ModrmModCompactAxis))
        {
            var compact = new HashSet<int>();
            foreach (var slot in step.Slots)
            {
                switch (slot)
                {
                    case "reg":
                        compact.UnionWith(RegisterFormMods);
                        break;
                    case "mem":
                        compact.UnionWith(MemoryFormMods);
                        break;
                    default:
                        failure = $"unknown slot {ModrmModCompactAxis}={slot}";
                        return false;
                }
            }

            mods.IntersectWith(compact);
        }

        var isForcedRegisterForm = definition.Flags.HasFlag(InstructionFlagsEnum.ForceRegForm);
        var prefersMemory = !isForcedRegisterForm && (definition.Operands ?? [])
            .Any(operand => (operand.Encoding is OperandEncoding.ModrmRm) && IsMemoryOperand(operand.Type));

        int[] modOrder = prefersMemory ? [0, 1, 2, 3] : [3, 0, 1, 2];
        var mod = Choose(mods, modOrder);
        if (mod is null)
        {
            failure = "the ModRM steps exclude every mod value";
            return false;
        }

        var isRegisterForm = (mod == 3) || isForcedRegisterForm;

        var reg = Choose(regs, Prefer(registers.Reg, AllModrmFieldValues));
        var rm = Choose(rms, isRegisterForm
            ? Prefer(registers.Rm, AllModrmFieldValues)
            : (addressWidth == 16) ? MemoryRmOrder16 : MemoryRmOrder);

        if ((reg is null) || (rm is null))
        {
            failure = "the ModRM steps exclude every reg or rm value";
            return false;
        }

        modrm = new Modrm(mod.Value, reg.Value, rm.Value, ReadsAddressing: hasModrmEncoding && !isForcedRegisterForm);
        failure = string.Empty;
        return true;

        static int[] Prefer(int? preferred, int[] order) =>
            (preferred is { } value) ? [value & 0x07, .. order.Where(other => other != (value & 0x07))] : order;

        static int? Choose(HashSet<int> allowed, int[] order)
        {
            var index = Array.FindIndex(order, allowed.Contains);
            return (index < 0) ? null : order[index];
        }
    }

    internal static bool IsModrmAxis(string node)
    {
        return node is ModrmModAxis or ModrmModCompactAxis or ModrmRegAxis or ModrmRmAxis;
    }

    /// <summary>
    /// <see langword="true"/> for a step on an axis that every encoding can route.
    /// </summary>
    internal static bool IsSharedStep(PathStep step)
    {
        return step.Node.StartsWith(FeatureAxisPrefix, StringComparison.Ordinal) || IsModrmAxis(step.Node) ||
            step.Node is AddressSizeAxis or ModeAxis or ModeCompactAxis or OperandSizeAxis or RexWAxis;
    }

    /// <summary>
    /// <see langword="true"/> for a step on an axis that the EVEX, MVEX, VEX and XOP encodings can route.
    /// </summary>
    internal static bool IsPayloadStep(PathStep step)
    {
        return IsSharedStep(step) || (step.Node == VectorLengthAxis);
    }

    /// <summary>
    /// Appends the ModRM byte and, when the decoder reads the addressing form, the SIB byte and the displacement. The
    /// canonical memory operand is base-only. A fixed <c>rm=4</c> gets a SIB byte with scale 0, base rax and index rcx,
    /// which is <c>08</c>, unless <paramref name="sibIndex"/> names another index.
    /// </summary>
    /// <param name="bytes">The sample bytes.</param>
    /// <param name="modrm">The ModRM fields.</param>
    /// <param name="addressWidth">The effective address size.</param>
    /// <param name="sibIndex">
    /// The SIB index register in place of rcx, for the vector register of a VSIB operand.
    /// </param>
    public static void AppendModrm(List<byte> bytes, Modrm modrm, int addressWidth, int? sibIndex = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        bytes.Add((byte)((modrm.Mod << 6) | (modrm.Reg << 3) | modrm.Rm));

        if (!modrm.ReadsAddressing || (modrm.Mod == 3))
        {
            return;
        }

        int displacementWidth;
        if (addressWidth == 16)
        {
            displacementWidth = modrm.Mod switch
            {
                0 => (modrm.Rm == 6) ? 16 : 0,
                1 => 8,
                _ => 16
            };
        }
        else
        {
            if (modrm.Rm == 4)
            {
                bytes.Add((byte)((sibIndex ?? 1) << 3));
            }

            displacementWidth = modrm.Mod switch
            {
                0 => (modrm.Rm == 5) ? 32 : 0,
                1 => 8,
                _ => 32
            };
        }

        AppendValue(bytes, displacementWidth);
    }

    /// <summary>
    /// Appends the displacement and immediate operands of <paramref name="definition"/> in the order the decoder
    /// reads them: the displacement first, then the immediates in operand order.
    /// </summary>
    /// <param name="bytes">The sample bytes.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="operandWidth">
    /// The operand width that selects the size of variable immediates, see
    /// <see cref="EffectiveSizes.Compute"/>.
    /// </param>
    /// <param name="addressWidth">The effective address size, which selects the size of a displacement operand.</param>
    /// <param name="is4Byte">
    /// The <c>is4</c> immediate byte, or <see langword="null"/> for an encoding without one.
    /// </param>
    /// <param name="failure">The operand the builder cannot encode.</param>
    /// <returns><see langword="true"/> if every operand could be encoded.</returns>
    public static bool TryAppendImmediates(List<byte> bytes, InstructionDefinition definition, int operandWidth,
        int addressWidth, out string failure, byte? is4Byte = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(definition);

        var displacement = new List<byte>();
        var immediates = new List<byte>();
        var hasIs4Byte = false;

        foreach (var operand in definition.Operands ?? [])
        {
            // A far pointer is an offset of the operand size followed by a 16-bit selector, and the generator stops
            // collecting immediates at it.
            if (operand.Type is OperandType.PTR)
            {
                AppendValue(immediates, (operandWidth == 16) ? 16 : 32);
                AppendValue(immediates, 16);
                break;
            }

            if (DisplacementWidth(operand.Encoding, addressWidth) is { } displacementWidth)
            {
                AppendValue(displacement, displacementWidth);
            }
            else if (ImmediateWidth(operand.Encoding, operandWidth) is { } immediateWidth)
            {
                AppendValue(immediates, immediateWidth);
            }
            else if ((operand.Encoding is OperandEncoding.IS4) && (is4Byte is { } value))
            {
                // The register and the 4-bit immediate of an is4 encoding share one byte, which the generator
                // places at the first is4 operand.
                if (!hasIs4Byte)
                {
                    immediates.Add(value);
                    hasIs4Byte = true;
                }
            }
            else if (!IsByteFreeEncoding(operand.Encoding))
            {
                failure = $"operand {operand.Type} has the unsupported encoding {operand.Encoding}";
                return false;
            }
        }

        bytes.AddRange(displacement);
        bytes.AddRange(immediates);

        failure = string.Empty;
        return true;
    }

    /// <summary>
    /// Appends a value of <paramref name="width"/> bits whose little-endian bytes count up from <c>11</c>, so a
    /// truncation or a sign extension shows in the decode.
    /// </summary>
    public static void AppendValue(List<byte> bytes, int width)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        for (var i = 0; i < width / 8; i++)
        {
            bytes.Add((byte)(0x11 * (i + 1)));
        }
    }

    /// <summary>
    /// The vvvv field of an EVEX, MVEX, VEX or XOP payload, which stores the register index inverted.
    /// </summary>
    internal static int InvertedVvvv(int? index)
    {
        return ~(index ?? 0) & 0x0F;
    }

    /// <summary>
    /// The payload byte the EVEX, MVEX, VEX and XOP encodings share: W in bit 7, vvvv in bits 6 to 3, a bit of the
    /// encoding in bit 2 and pp in bits 1 and 0.
    /// </summary>
    internal static byte WvvvvPpByte(bool w, int vvvv, int bit2, RefiningPrefix prefix)
    {
        return (byte)(((w ? 1 : 0) << 7) | (vvvv << 3) | (bit2 << 2) | PpField(prefix));
    }

    /// <summary>
    /// The pp field of an EVEX, MVEX, VEX or XOP payload, which stands for the refining prefix byte.
    /// </summary>
    private static int PpField(RefiningPrefix prefix)
    {
        return prefix switch
        {
            RefiningPrefix.PNP => 0b00,
            RefiningPrefix.P66 => 0b01,
            RefiningPrefix.PF3 => 0b10,
            RefiningPrefix.PF2 => 0b11,
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null)
        };
    }

    private static HashSet<int> FieldValues(IReadOnlyList<PathStep> steps, string axis)
    {
        var values = new HashSet<int>(AllModrmFieldValues);

        foreach (var step in steps.Where(step => step.Node == axis))
        {
            values.IntersectWith(step.Slots.Select(slot => int.Parse(slot, CultureInfo.InvariantCulture)));
        }

        return values;
    }

    private static int? DisplacementWidth(OperandEncoding encoding, int addressWidth)
    {
        return encoding switch
        {
            OperandEncoding.Disp8 => 8,
            OperandEncoding.Disp16 => 16,
            OperandEncoding.Disp32 => 32,
            OperandEncoding.Disp64 => 64,
            OperandEncoding.Disp16_32_64 => addressWidth,
            OperandEncoding.Disp32_32_64 => Math.Max(32, addressWidth),
            OperandEncoding.Disp16_32_32 => Math.Min(32, addressWidth),
            _ => null
        };
    }

    private static int? ImmediateWidth(OperandEncoding encoding, int operandWidth)
    {
        return encoding switch
        {
            OperandEncoding.Uimm8 or OperandEncoding.Simm8 or OperandEncoding.Jimm8 => 8,
            OperandEncoding.Uimm16 or OperandEncoding.Simm16 or OperandEncoding.Jimm16 => 16,
            OperandEncoding.Uimm32 or OperandEncoding.Simm32 or OperandEncoding.Jimm32 => 32,
            OperandEncoding.Uimm64 or OperandEncoding.Simm64 or OperandEncoding.Jimm64 => 64,
            OperandEncoding.Uimm16_32_64 or OperandEncoding.Simm16_32_64 or OperandEncoding.Jimm16_32_64 =>
                operandWidth,
            OperandEncoding.Uimm32_32_64 or OperandEncoding.Simm32_32_64 or OperandEncoding.Jimm32_32_64 =>
                Math.Max(32, operandWidth),
            OperandEncoding.Uimm16_32_32 or OperandEncoding.Simm16_32_32 or OperandEncoding.Jimm16_32_32 =>
                Math.Min(32, operandWidth),
            _ => null
        };
    }

    /// <summary>
    /// <see langword="true"/> for the operand encodings that add no byte after the opcode and the ModRM byte, because
    /// they live in the opcode, the ModRM byte or a prefix payload.
    /// </summary>
    private static bool IsByteFreeEncoding(OperandEncoding encoding)
    {
        return encoding is OperandEncoding.MASK or OperandEncoding.ModrmReg or OperandEncoding.ModrmRm or
            OperandEncoding.NDSNDD or OperandEncoding.None or OperandEncoding.Opcode or OperandEncoding.OpcodeBits;
    }

    private static bool IsMemoryOperand(OperandType type)
    {
        return type is OperandType.AGEN or OperandType.AGENNoRel or OperandType.MEM or OperandType.MEMVSIBX or
            OperandType.MEMVSIBY or OperandType.MEMVSIBZ or OperandType.MIB;
    }

    private static RegisterKind GetRegisterKind(OperandType type)
    {
        return type switch
        {
            OperandType.GPR8 or OperandType.GPR16 or OperandType.GPR32 or OperandType.GPR64 or
                OperandType.GPR16_32_64 or OperandType.GPR32_32_64 or OperandType.GPR16_32_32 or
                OperandType.GPRASZ => RegisterKind.GPR,
            OperandType.BND => RegisterKind.BOUND,
            OperandType.BSR => RegisterKind.BSR,
            OperandType.CR => RegisterKind.CONTROL,
            OperandType.DR => RegisterKind.DEBUG,
            OperandType.FPR => RegisterKind.X87,
            OperandType.MASK => RegisterKind.MASK,
            OperandType.MMX => RegisterKind.MMX,
            OperandType.SREG => RegisterKind.SEGMENT,
            OperandType.TMM => RegisterKind.TMM,
            OperandType.XMM or OperandType.YMM or OperandType.ZMM => RegisterKind.VR,
            _ => RegisterKind.INVALID
        };
    }

    private static int FirstRegisterIndex(RegisterKind registerKind)
    {
        return registerKind switch
        {
            // The kinds with holes start at the first index the decoder accepts. Index 0 of a segment register is
            // ES, which unlike CS is a valid destination.
            RegisterKind.BOUND or RegisterKind.BSR or RegisterKind.CONTROL or RegisterKind.DEBUG or
                RegisterKind.SEGMENT or RegisterKind.TMM => 0,

            // Starting at 1 keeps the register operands apart from the [rax] memory base.
            _ => 1
        };
    }

    /// <summary>
    /// The legacy prefixes and the REX or REX2 bits of a sample.
    /// </summary>
    /// <param name="Group1">The group-1 prefix byte <c>F2</c> or <c>F3</c>, or <c>0</c> for none.</param>
    /// <param name="OperandSize66"><see langword="true"/> if the sample carries a <c>66</c> byte.</param>
    /// <param name="AddressSize67"><see langword="true"/> if the sample carries a <c>67</c> byte.</param>
    /// <param name="W">The W bit.</param>
    /// <param name="B">The B bit, which routes a <c>rex_b</c> step.</param>
    /// <param name="Rex2"><see langword="true"/> if W and B go into a REX2 prefix instead of a REX prefix.</param>
    internal readonly record struct PrefixState(byte Group1, bool OperandSize66, bool AddressSize67, bool W, bool B,
        bool Rex2)
    {
        /// <summary>
        /// The prefix the mandatory-prefix filter routes by: the last <c>F2</c> or <c>F3</c>, else a <c>66</c>.
        /// </summary>
        public byte MandatoryCandidate => (Group1 != 0) ? Group1 : (OperandSize66 ? (byte)0x66 : (byte)0x00);

        public int ByteCount =>
            ((Group1 != 0) ? 1 : 0) + (OperandSize66 ? 1 : 0) + (AddressSize67 ? 1 : 0) +
            (Rex2 ? 2 : ((W || B) ? 1 : 0));
    }

    /// <param name="ReadsAddressing">
    /// <see langword="false"/> if the decoder reads no SIB byte and displacement, because the definition has no ModRM
    /// in its encoding info (only a filter read the byte) or it forces the register form.
    /// </param>
    internal readonly record struct Modrm(int Mod, int Reg, int Rm, bool ReadsAddressing);

    /// <param name="Reg">The register index of the operand in <c>ModRM.reg</c>, if any.</param>
    /// <param name="Rm">The register index of the register operand in <c>ModRM.rm</c>, if any.</param>
    /// <param name="Vvvv">The register index of the operand in <c>vvvv</c>, if any.</param>
    /// <param name="Is4">The register index of the operand in the high nibble of the <c>is4</c> byte, if any.</param>
    /// <param name="VsibIndex">
    /// For a definition with a VSIB operand, the lowest vector register index that no register operand takes. The
    /// decoder rejects a gather whose VSIB index equals its destination or its mask.
    /// </param>
    internal sealed record RegisterAssignment(int? Reg, int? Rm, int? Vvvv, int? Is4, int? VsibIndex)
    {
        /// <summary>
        /// The first of <see cref="Reg"/>, <see cref="Rm"/>, <see cref="Vvvv"/> and <see cref="VsibIndex"/> that is 8
        /// or above, which needs a register extension bit.
        /// </summary>
        public int? WideIndex
        {
            get
            {
                int?[] indices = [Reg, Rm, Vvvv, VsibIndex];
                return indices.FirstOrDefault(index => index >= 8);
            }
        }
    }

    /// <param name="Enable">The decoder modes to enable, comma-separated, or <c>-</c>.</param>
    /// <param name="Disable">The decoder modes to disable, comma-separated, or <c>-</c>.</param>
    internal readonly record struct DecoderModes(string Enable, string Disable);

    /// <summary>
    /// How a sample depends on the <c>apx</c> decoder mode.
    /// </summary>
    internal enum ApxRequirement
    {
        None,

        /// <summary>
        /// The decoder routes the sample to its leaf only with APX off, which puts <c>apx</c> on the disable list.
        /// </summary>
        Forbidden,

        /// <summary>
        /// The sample carries a REX2 prefix or is an EEVEX, map 4 or map 7 form, which puts <c>apx</c> on the enable
        /// list.
        /// </summary>
        Required
    }
}

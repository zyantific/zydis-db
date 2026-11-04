using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

    private static readonly bool[] Booleans = [false, true];

    // F3 before F2, which is the slot order of mandatory_prefix and gives a bare prefix_group1=1 step an F3.
    private static readonly byte[] Group1Prefixes = [0x00, 0xF3, 0xF2];

    private static readonly int[] AllModrmFieldValues = [0, 1, 2, 3, 4, 5, 6, 7];
    private static readonly int[] RegisterFormMods = [3];
    private static readonly int[] MemoryFormMods = [0, 1, 2];

    // The rm values of an `else` or multi-slot step in preference order. Under a 32-bit or 64-bit address size
    // `rm=4` adds a SIB byte and `rm=5` with `mod=0` is disp32 or RIP-relative, under a 16-bit address size `rm=6`
    // with `mod=0` is a bare disp16, so these come last.
    private static readonly int[] MemoryRmOrder = [0, 1, 2, 3, 6, 7, 4, 5];
    private static readonly int[] MemoryRmOrder16 = [0, 1, 2, 3, 4, 5, 7, 6];

    /// <summary>
    /// Builds the sample of <paramref name="path"/> in <paramref name="mode"/>.
    /// </summary>
    public static SampleBuildResult Build(DecoderTreePath path, ModeNode.Slot mode, int id, string formId)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(formId);

        return path.Encoding switch
        {
            InstructionEncoding.Default or InstructionEncoding.AMD3DNOW =>
                SampleBuilderLegacy.Build(path, mode, id, formId),
            _ => new Unencodable($"no builder for {EncodingTag(path.Encoding)}")
        };
    }

    public static string EncodingTag(InstructionEncoding encoding)
    {
        return encoding switch
        {
            InstructionEncoding.Default => "default",
            InstructionEncoding.AMD3DNOW => "3dnow",
            InstructionEncoding.XOP => "xop",
            InstructionEncoding.VEX => "vex",
            InstructionEncoding.EVEX => "evex",
            InstructionEncoding.MVEX => "mvex",
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
        };
    }

    public static string MapTag(OpcodeMap map)
    {
        return map switch
        {
            OpcodeMap.MAP0 => "map0",
            OpcodeMap.M0F => "0f",
            OpcodeMap.M0F38 => "0f38",
            OpcodeMap.M0F3A => "0f3a",
            OpcodeMap.M0F0F => "0f0f",
            OpcodeMap.MAP4 => "map4",
            OpcodeMap.MAP5 => "map5",
            OpcodeMap.MAP6 => "map6",
            OpcodeMap.MAP7 => "map7",
            OpcodeMap.XOP8 => "xop8",
            OpcodeMap.XOP9 => "xop9",
            OpcodeMap.XOPA => "xopa",
            _ => throw new ArgumentOutOfRangeException(nameof(map), map, null)
        };
    }

    public static string ModeTag(ModeNode.Slot mode)
    {
        return mode switch
        {
            ModeNode.Slot.M16 => "16",
            ModeNode.Slot.M32 => "32",
            ModeNode.Slot.M64 => "64",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    public static string FormatStep(PathStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var slots = string.Join('|', step.Slots);

        return step.IsElse ? $"{step.Node}=else:{slots}" : $"{step.Node}={slots}";
    }

    public static CorpusSample CreateSample(DecoderTreePath path, ModeNode.Slot mode, IReadOnlyCollection<byte> bytes,
        DecoderModes decoderModes, int osz, int asz, int id, string formId)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(bytes);

        return new CorpusSample(
            Convert.ToHexString([.. bytes]),
            ModeTag(mode),
            decoderModes.Enable,
            decoderModes.Disable,
            EncodingTag(path.Encoding),
            MapTag(path.Map),
            path.Opcode.ToString("X2", CultureInfo.InvariantCulture),
            osz.ToString(CultureInfo.InvariantCulture),
            asz.ToString(CultureInfo.InvariantCulture),
            id,
            formId,
            path.Definition.Mnemonic,
            path.PathString);
    }

    /// <summary>
    /// Returns the first step whose every slot the architecture rules out in <paramref name="mode"/>.
    /// </summary>
    /// <param name="steps">The path steps.</param>
    /// <param name="mode">The machine mode.</param>
    /// <param name="hasRexPrefixes">
    /// <see langword="true"/> for the encodings whose W and B bits come from a REX or REX2 prefix, which only 64-bit
    /// mode has. VEX, XOP, EVEX and MVEX carry them in their payload in every mode.
    /// </param>
    /// <returns>The reason for the report, or <see langword="null"/> if the mode can reach every step.</returns>
    public static string? FindArchitecturalConflict(IReadOnlyList<PathStep> steps, ModeNode.Slot mode,
        bool hasRexPrefixes)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var is64 = mode is ModeNode.Slot.M64;

        foreach (var step in steps)
        {
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
                return $"{FormatStep(step)} in {ModeTag(mode)}-bit mode";
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
    /// The legacy prefix, REX and REX2 combinations a legacy or 3DNow sample can carry in <paramref name="mode"/>,
    /// fewest bytes first.
    /// </summary>
    public static IReadOnlyList<PrefixState> LegacyPrefixStates(ModeNode.Slot mode)
    {
        var states = new List<PrefixState>();

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
                                states.Add(new PrefixState(group1, operandSize66, addressSize67, w, b, rex2));
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

    private static bool Routes(PathStep step, PrefixState state, ModeNode.Slot mode)
    {
        return step.Slots.Contains(RoutePrefixAxis(step.Node, state, mode));
    }

    private static string DiagnosePrefixConflict(PathStep[] prefixSteps, ModeNode.Slot mode,
        IReadOnlyList<PrefixState> candidates)
    {
        var modeText = $"in {ModeTag(mode)}-bit mode";

        foreach (var step in prefixSteps)
        {
            if (!candidates.Any(state => Routes(step, state, mode)))
            {
                return $"{FormatStep(step)} has no encodable slot {modeText}";
            }
        }

        for (var i = 0; i < prefixSteps.Length; i++)
        {
            for (var j = i + 1; j < prefixSteps.Length; j++)
            {
                var (first, second) = (prefixSteps[i], prefixSteps[j]);
                if (!candidates.Any(state => Routes(first, state, mode) && Routes(second, state, mode)))
                {
                    return $"{FormatStep(first)} contradicts {FormatStep(second)} {modeText}";
                }
            }
        }

        return $"{string.Join(", ", prefixSteps.Select(FormatStep))} contradict each other {modeText}";
    }

    public static void AppendLegacyPrefixes(List<byte> bytes, PrefixState state)
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
    public static void AppendRexPrefix(List<byte> bytes, PrefixState state, OpcodeMap map)
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

    /// <summary>
    /// The decoder modes the path depends on. A <c>feature_*</c> step takes slot <c>0</c> when it serves it, which
    /// needs no mode change for most features, and still names the mode so the checker pins it.
    /// </summary>
    public static DecoderModes GetDecoderModes(IReadOnlyList<PathStep> steps, bool hasRex2)
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

        // APX is on by default. Naming it keeps a changed default from silently breaking the REX2 samples.
        if (hasRex2)
        {
            enable.Add("apx");
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

        var next = new Dictionary<RegisterClass, int>();
        int? reg = null;
        int? rm = null;

        foreach (var operand in definition.Operands ?? [])
        {
            var registerClass = GetRegisterClass(operand.Type);
            if (registerClass is RegisterClass.None)
            {
                continue;
            }

            var index = next.GetValueOrDefault(registerClass, FirstRegisterIndex(registerClass));

            switch (operand.Encoding)
            {
                case OperandEncoding.ModrmReg:
                    reg = index;
                    break;
                case OperandEncoding.ModrmRm:
                    rm = index;
                    break;
                default:
                    continue;
            }

            next[registerClass] = index + 1;
        }

        return new RegisterAssignment(reg, rm);
    }

    /// <summary>
    /// <see langword="true"/> if the decoder reads a ModRM byte for <paramref name="definition"/>, by the rule the
    /// generator applies to the encoding info of the definition.
    /// </summary>
    public static bool HasModrmEncoding(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

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
    /// Chooses the ModRM fields from the path slots and the register assignment. A <c>modrm_mod</c> axis the path
    /// leaves open takes the register form unless an operand in <c>rm</c> is a memory operand.
    /// </summary>
    /// <returns><see langword="true"/> if every ModRM step has a slot the fields can take.</returns>
    public static bool TryChooseModrm(IReadOnlyList<PathStep> steps, InstructionDefinition definition,
        RegisterAssignment registers, int addressWidth, out Modrm modrm, out string failure)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(registers);

        modrm = default;

        var mods = FieldValues(steps, ModrmModAxis);
        var regs = FieldValues(steps, ModrmRegAxis);
        var rms = FieldValues(steps, ModrmRmAxis);

        foreach (var step in steps.Where(step => step.Node == ModrmModCompactAxis))
        {
            mods.IntersectWith(step.Slots.SelectMany(slot => (slot == "reg") ? RegisterFormMods : MemoryFormMods));
        }

        var isForcedRegisterForm = definition.Flags.HasFlag(InstructionFlagsEnum.ForceRegForm);
        var prefersMemory = !isForcedRegisterForm && (definition.Operands ?? [])
            .Any(operand => (operand.Encoding is OperandEncoding.ModrmRm) && IsMemoryOperand(operand.Type));

        int[] modOrder = prefersMemory ? [0, 1, 2, 3] : [3, 0, 1, 2];
        var mod = modOrder.Cast<int?>().FirstOrDefault(value => mods.Contains(value!.Value));
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

        modrm = new Modrm(mod.Value, reg.Value, rm.Value);
        failure = string.Empty;
        return true;

        static int[] Prefer(int? preferred, int[] order) =>
            (preferred is { } value) ? [value & 0x07, .. order.Where(other => other != (value & 0x07))] : order;

        static int? Choose(HashSet<int> allowed, int[] order) =>
            order.Cast<int?>().FirstOrDefault(value => allowed.Contains(value!.Value));
    }

    /// <summary>
    /// Appends the ModRM byte and, when the decoder reads the addressing form, the SIB byte and the displacement. The
    /// canonical memory operand is base-only, a fixed <c>rm=4</c> gets the SIB byte <c>08</c> (scale 0, index rcx,
    /// base rax).
    /// </summary>
    /// <param name="bytes">The sample bytes.</param>
    /// <param name="modrm">The ModRM fields.</param>
    /// <param name="addressWidth">The effective address size.</param>
    /// <param name="readsAddressing">
    /// <see langword="false"/> if the definition reads no SIB byte and displacement, because it has no ModRM in its
    /// encoding info (only a filter read the byte) or it forces the register form.
    /// </param>
    public static void AppendModrm(List<byte> bytes, Modrm modrm, int addressWidth, bool readsAddressing)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        bytes.Add((byte)((modrm.Mod << 6) | (modrm.Reg << 3) | modrm.Rm));

        if (!readsAddressing || (modrm.Mod == 3))
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
                bytes.Add(0x08);
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
    /// <see cref="EffectiveSizes.ComputeOperandWidth"/>.
    /// </param>
    /// <param name="addressWidth">The effective address size, which selects the size of a displacement operand.</param>
    /// <param name="failure">The operand the builder cannot encode.</param>
    /// <returns><see langword="true"/> if every operand could be encoded.</returns>
    public static bool TryAppendImmediates(List<byte> bytes, InstructionDefinition definition, int operandWidth,
        int addressWidth, out string failure)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(definition);

        var displacements = new List<int>();
        var immediates = new List<int>();

        foreach (var operand in definition.Operands ?? [])
        {
            // A far pointer is an offset of the operand size followed by a 16-bit selector, and the generator stops
            // collecting immediates at it.
            if (operand.Type is OperandType.PTR)
            {
                immediates.Add((operandWidth == 16) ? 16 : 32);
                immediates.Add(16);
                break;
            }

            if (DisplacementWidth(operand.Encoding, addressWidth) is { } displacementWidth)
            {
                displacements.Add(displacementWidth);
            }
            else if (ImmediateWidth(operand.Encoding, operandWidth) is { } immediateWidth)
            {
                immediates.Add(immediateWidth);
            }
            else if (operand.Encoding is OperandEncoding.IS4)
            {
                failure = $"operand {operand.Type} has the unsupported encoding {operand.Encoding}";
                return false;
            }
        }

        foreach (var width in displacements.Concat(immediates))
        {
            AppendValue(bytes, width);
        }

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

    private static bool IsMemoryOperand(OperandType type)
    {
        return type is OperandType.MEM or OperandType.MEMVSIBX or OperandType.MEMVSIBY or OperandType.MEMVSIBZ or
            OperandType.AGEN or OperandType.AGENNoRel or OperandType.MIB;
    }

    private static RegisterClass GetRegisterClass(OperandType type)
    {
        return type switch
        {
            OperandType.GPR8 or OperandType.GPR16 or OperandType.GPR32 or OperandType.GPR64 or
                OperandType.GPR16_32_64 or OperandType.GPR32_32_64 or OperandType.GPR16_32_32 or
                OperandType.GPRASZ => RegisterClass.Gpr,
            OperandType.BND => RegisterClass.Bound,
            OperandType.BSR => RegisterClass.BlockScale,
            OperandType.CR => RegisterClass.Control,
            OperandType.DR => RegisterClass.Debug,
            OperandType.FPR => RegisterClass.X87,
            OperandType.MASK => RegisterClass.Mask,
            OperandType.MMX => RegisterClass.Mmx,
            OperandType.SREG => RegisterClass.Segment,
            OperandType.TMM => RegisterClass.Tile,
            OperandType.XMM or OperandType.YMM or OperandType.ZMM => RegisterClass.Vector,
            _ => RegisterClass.None
        };
    }

    private static int FirstRegisterIndex(RegisterClass registerClass)
    {
        return registerClass switch
        {
            // The classes with holes start at the first index the decoder accepts. Index 0 of a segment register is
            // ES, which unlike CS is a valid destination.
            RegisterClass.BlockScale or RegisterClass.Bound or RegisterClass.Control or RegisterClass.Debug or
                RegisterClass.Segment or RegisterClass.Tile => 0,

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

    internal readonly record struct Modrm(int Mod, int Reg, int Rm);

    /// <param name="Reg">The register index of the operand in <c>ModRM.reg</c>, if any.</param>
    /// <param name="Rm">The register index of the register operand in <c>ModRM.rm</c>, if any.</param>
    internal sealed record RegisterAssignment(int? Reg, int? Rm);

    /// <param name="Enable">The decoder modes to enable, comma-separated, or <c>-</c>.</param>
    /// <param name="Disable">The decoder modes to disable, comma-separated, or <c>-</c>.</param>
    internal readonly record struct DecoderModes(string Enable, string Disable);

    private enum RegisterClass
    {
        None,
        BlockScale,
        Bound,
        Control,
        Debug,
        Gpr,
        Mask,
        Mmx,
        Segment,
        Tile,
        Vector,
        X87
    }
}

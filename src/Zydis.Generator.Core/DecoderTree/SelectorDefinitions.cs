using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zydis.Generator.Core.DecoderTree;

public static partial class SelectorDefinitions
{
    public static readonly SelectorDefinition OpcodeTable = new()
    {
        Name = "opcode",
        Parameters = [],
        Slots = Enumerable.Range(0, 256).Select(x => x.ToString("X2", CultureInfo.InvariantCulture)).ToArray(),
    };

    public static readonly SelectorDefinition XOP = new()
    {
        Name = "xop",
        Parameters = [],
        Slots =
        [
            "default",
            "np_xop8",
            "np_xop9",
            "np_xopa",
            "66_xop8",
            "66_xop9",
            "66_xopa",
            "f3_xop8",
            "f3_xop9",
            "f3_xopa",
            "f2_xop8",
            "f2_xop9",
            "f2_xopa"
        ],
    };

    public static readonly SelectorDefinition VEX = new()
    {
        Name = "vex",
        Parameters = [],
        Slots =
        [
            "default",
            "np",
            "np_0f",
            "np_0f38",
            "np_0f3a",
            "66",
            "66_0f",
            "66_0f38",
            "66_0f3a",
            "f3",
            "f3_0f",
            "f3_0f38",
            "f3_0f3a",
            "f2",
            "f2_0f",
            "f2_0f38",
            "f2_0f3a",
        ],
    };

    public static readonly SelectorDefinition EMVEX = new()
    {
        Name = "emvex",
        Parameters = [],
        Slots =
        [
            "default",
            "evex_np",
            "evex_np_0f",
            "evex_np_0f38",
            "evex_np_0f3a",
            "evex_np_map4",
            "evex_np_map5",
            "evex_np_map6",
            "evex_np_map7",
            "evex_66",
            "evex_66_0f",
            "evex_66_0f38",
            "evex_66_0f3a",
            "evex_66_map4",
            "evex_66_map5",
            "evex_66_map6",
            "evex_66_map7",
            "evex_f3",
            "evex_f3_0f",
            "evex_f3_0f38",
            "evex_f3_0f3a",
            "evex_f3_map4",
            "evex_f3_map5",
            "evex_f3_map6",
            "evex_f3_map7",
            "evex_f2",
            "evex_f2_0f",
            "evex_f2_0f38",
            "evex_f2_0f3a",
            "evex_f2_map4",
            "evex_f2_map5",
            "evex_f2_map6",
            "evex_f2_map7",
            "mvex_np",
            "mvex_np_0f",
            "mvex_np_0f38",
            "mvex_np_0f3a",
            "mvex_66",
            "mvex_66_0f",
            "mvex_66_0f38",
            "mvex_66_0f3a",
            "mvex_f3",
            "mvex_f3_0f",
            "mvex_f3_0f38",
            "mvex_f3_0f3a",
            "mvex_f2",
            "mvex_f2_0f",
            "mvex_f2_0f38",
            "mvex_f2_0f3a",
        ],
    };

    public static readonly SelectorDefinition Rex2Map = new()
    {
        Name = "rex2_map",
        Parameters = [],
        Slots = ["default", "rex2_default", "rex2_0f"],
    };

    public static readonly SelectorDefinition Mode = new()
    {
        Name = "mode",
        Parameters = [],
        Slots = ["16", "32", "64"],
    };

    public static readonly SelectorDefinition ModeCompact = new()
    {
        Name = "mode_compact",
        Parameters = [],
        Slots = ["64", "!64"],
    };

    public static readonly SelectorDefinition ModrmMod = new()
    {
        Name = "modrm_mod",
        Parameters = [],
        Slots = ["0", "1", "2", "3"],
    };

    public static readonly SelectorDefinition ModrmModCompact = new()
    {
        Name = "modrm_mod_compact",
        Parameters = [],
        Slots = ["3", "!3"],
    };

    public static readonly SelectorDefinition ModrmReg = new()
    {
        Name = "modrm_reg",
        Parameters = [],
        Slots = ["0", "1", "2", "3", "4", "5", "6", "7"],
    };

    public static readonly SelectorDefinition ModrmRm = new()
    {
        Name = "modrm_rm",
        Parameters = [],
        Slots = ["0", "1", "2", "3", "4", "5", "6", "7"],
    };

    public static readonly SelectorDefinition PrefixGroup1 = new()
    {
        Name = "prefix_group1",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition MandatoryPrefix = new()
    {
        Name = "mandatory_prefix",
        Parameters = [],
        Slots = ["ignore", "none", "66", "f3", "f2"],
    };

    public static readonly SelectorDefinition OperandSize = new()
    {
        Name = "operand_size",
        Parameters = [],
        Slots = ["16", "32", "64"],
    };

    public static readonly SelectorDefinition AddressSize = new()
    {
        Name = "address_size",
        Parameters = [],
        Slots = ["16", "32", "64"],
    };

    public static readonly SelectorDefinition VectorLength = new()
    {
        Name = "vector_length",
        Parameters = [],
        Slots = ["128", "256", "512"],
    };

    public static readonly SelectorDefinition RexW = new()
    {
        Name = "rex_w",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition RexB = new()
    {
        Name = "rex_b",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition EvexB = new()
    {
        Name = "evex_b",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition EvexU = new()
    {
        Name = "evex_u",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition MvexE = new()
    {
        Name = "mvex_e",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureAmd = new()
    {
        Name = "feature_amd",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureKnc = new()
    {
        Name = "feature_knc",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureMpx = new()
    {
        Name = "feature_mpx",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureCet = new()
    {
        Name = "feature_cet",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureLzcnt = new()
    {
        Name = "feature_lzcnt",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureTzcnt = new()
    {
        Name = "feature_tzcnt",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureWbnoinvd = new()
    {
        Name = "feature_wbnoinvd",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureCldemote = new()
    {
        Name = "feature_cldemote",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureCentaur = new()
    {
        Name = "feature_centaur",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureIprefetch = new()
    {
        Name = "feature_iprefetch",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition FeatureUd0Compat = new()
    {
        Name = "feature_ud0_compat",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition EvexNd = new()
    {
        Name = "evex_nd",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition EvexNf = new()
    {
        Name = "evex_nf",
        Parameters = [],
        Slots = ["0", "1"],
    };

    public static readonly SelectorDefinition EvexScc = new()
    {
        Name = "evex_scc",
        Parameters = [],
        Slots = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15"],
    };

    public static readonly SelectorDefinition Rex2 = new()
    {
        Name = "rex_2",
        Parameters = [],
        Slots = ["no_rex2", "rex2"],
    };

    private static readonly IReadOnlyDictionary<string, SelectorDefinition> ByName =
        new Dictionary<string, SelectorDefinition>
        {
            [OpcodeTable.Name] = OpcodeTable,
            [Mode.Name] = Mode,
            [ModeCompact.Name] = ModeCompact,
            [ModrmMod.Name] = ModrmMod,
            [ModrmModCompact.Name] = ModrmModCompact,
            [ModrmReg.Name] = ModrmReg,
            [ModrmRm.Name] = ModrmRm,
            [PrefixGroup1.Name] = PrefixGroup1,
            [MandatoryPrefix.Name] = MandatoryPrefix,
            [OperandSize.Name] = OperandSize,
            [AddressSize.Name] = AddressSize,
            [VectorLength.Name] = VectorLength,
            [RexW.Name] = RexW,
            [RexB.Name] = RexB,
            [EvexB.Name] = EvexB,
            [EvexU.Name] = EvexU,
            [MvexE.Name] = MvexE,
            [FeatureAmd.Name] = FeatureAmd,
            [FeatureKnc.Name] = FeatureKnc,
            [FeatureMpx.Name] = FeatureMpx,
            [FeatureCet.Name] = FeatureCet,
            [FeatureLzcnt.Name] = FeatureLzcnt,
            [FeatureTzcnt.Name] = FeatureTzcnt,
            [FeatureWbnoinvd.Name] = FeatureWbnoinvd,
            [FeatureCldemote.Name] = FeatureCldemote,
            [FeatureCentaur.Name] = FeatureCentaur,
            [FeatureIprefetch.Name] = FeatureIprefetch,
            [FeatureUd0Compat.Name] = FeatureUd0Compat,
            [EvexNd.Name] = EvexNd,
            [EvexNf.Name] = EvexNf,
            [EvexScc.Name] = EvexScc,
            [Rex2.Name] = Rex2,
        }.ToFrozenDictionary();

    [GeneratedRegex(@"^(?<type>[a-z,A-Z,0-9,_]+)(?:\[(?<arguments>[a-z,A-Z,0-9,_,\,]+)\])*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectorTypeRegex();

    /// <summary>
    /// Parses a selector type expression and extracts the corresponding selector definition and arguments.
    /// </summary>
    /// <param name="typeExpression">The selector type expression to parse.</param>
    /// <returns>
    /// A tuple containing the parsed <see cref="SelectorDefinition"/> and an array of arguments.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the input expression is not in a valid selector type format or if the selector type is unknown.
    /// </exception>
    public static (SelectorDefinition Definition, string[] Arguments) ParseSelectorType(string typeExpression)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeExpression);

        var match = SelectorTypeRegex().Match(typeExpression);
        if (!match.Success)
        {
            throw new NotSupportedException($"Invalid selector type '{typeExpression}'.");
        }

        var name = match.Groups["type"].Value;

        var definition = ByName.GetValueOrDefault(name);
        if (definition is null)
        {
            throw new NotSupportedException($"Unknown selector type '{name}'.");
        }

        var arguments = Array.Empty<string>();
        if (match.Groups["arguments"].Success)
        {
            arguments = match.Groups["arguments"].Value.Split(',');
        }

        if (arguments.Length != definition.NumberOfParameters)
        {
            throw new NotSupportedException(
                $"Invalid number of arguments for selector type '{definition}'. " +
                $"Expected {definition.NumberOfParameters}, got {arguments.Length}.");
        }

        return (definition, arguments);
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

using Zydis.Generator.Core.Common;

namespace Zydis.Generator.Core.DecoderTree;

public sealed class OpcodeTables
{
    private static readonly OrderedDictionary<InstructionEncoding, int> Lut = new(GenerateEncodingLookupIndices());

    private readonly OpcodeTableNode[] _tables;

    /// <summary>
    /// The list that contains the individual opcode tables.
    /// </summary>
    public IReadOnlyList<OpcodeTableNode> Tables => _tables;

    public OpcodeTables()
    {
        var tables = Enum.GetValues<InstructionEncoding>()
            .SelectMany(encoding => CartesianProduct([encoding], SupportedPrefixes(encoding), SupportedMaps(encoding)))
            .ToArray();

        _tables = new OpcodeTableNode[tables.Length];

        for (var i = 0; i < _tables.Length; ++i)
        {
            _tables[i] = new OpcodeTableNode(tables[i].Item1, tables[i].Item2, tables[i].Item3);
        }
    }

    /// <summary>
    /// Retrieves the opcode table node corresponding to the specified <paramref name="encoding"/>, opcode
    /// <paramref name="map"/>, and optional refining <paramref name="prefix"/>.
    /// </summary>
    /// <param name="encoding">The instruction encoding to use when locating the opcode table.</param>
    /// <param name="map">The opcode map to use when locating the opcode table.</param>
    /// <param name="prefix">
    /// An optional refining prefix used to further specify the opcode table. Must be <see langword="null"/> for
    /// <see cref="InstructionEncoding.Default"/>.
    /// </param>
    /// <returns>The <see cref="OpcodeTableNode"/> corresponding to the specified parameters.</returns>
    public OpcodeTableNode GetTable(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
    {
        return _tables[GetTableId(encoding, map, prefix)];
    }

    /// <summary>
    /// Attempts to retrieve the opcode table node corresponding to the specified <paramref name="encoding"/>, opcode
    /// <paramref name="map"/>, and optional refining <paramref name="prefix"/>.
    /// </summary>
    /// <param name="encoding">The instruction encoding to use when locating the opcode table.</param>
    /// <param name="map">The opcode map to use when locating the opcode table.</param>
    /// <param name="prefix">
    /// An optional refining prefix used to further specify the opcode table. Must be <see langword="null"/> for
    /// <see cref="InstructionEncoding.Default"/>.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="OpcodeTableNode"/> corresponding to the specified parameters,
    /// if the lookup was successful; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the opcode table node was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTable(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix, [NotNullWhen(true)] out OpcodeTableNode? result)
    {
        // Note:
        // For default encoding, the prefix must always be `null`. Mandatory (refining) prefixes are used with default
        // encoding, but very rarely and only in specific cases. We do not wire them in the opcode tables.

        var requiresPrefix = SupportedPrefixes(encoding).Any(x => x is not null);
        if (requiresPrefix != prefix.HasValue)
        {
            result = null;
            return false;
        }

        var index = (MapIndex(map) * PrefixCount(encoding)) + PrefixIndex(prefix);

        var tableCount = PrefixCount(encoding) * MapCount(encoding);
        if (index >= tableCount)
        {
            result = null;
            return false;
        }

        result = _tables[Lut[encoding] + index];
        return true;
    }

    /// <summary>
    /// Retrieves the opcode table id corresponding to the specified <paramref name="encoding"/>, opcode
    /// <paramref name="map"/>, and optional refining <paramref name="prefix"/>
    /// </summary>
    /// <param name="encoding">The instruction encoding.</param>
    /// <param name="map">The opcode map.</param>
    /// <param name="prefix">
    /// An optional refining prefix used to further specify the opcode table. Must be <see langword="null"/> for
    /// <see cref="InstructionEncoding.Default"/>.
    /// </param>
    /// <returns>The opcode table id corresponding to the specified parameters.</returns>
    public static int GetTableId(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
    {
        // Note:
        // For default encoding, the prefix must always be `null`. Mandatory (refining) prefixes are used with default
        // encoding, but very rarely and only in specific cases. We do not wire them in the opcode tables.

        var requiresPrefix = SupportedPrefixes(encoding).Any(x => x is not null);
        if (requiresPrefix != prefix.HasValue)
        {
            if (requiresPrefix)
            {
                throw new ArgumentException($"Encoding '{encoding}' requires a refining prefix.", nameof(prefix));
            }

            throw new ArgumentException($"Encoding '{encoding}' does not support refining prefixes.", nameof(prefix));
        }

        var index = (PrefixIndex(prefix) * MapCount(encoding)) + MapIndex(map);

        var tableCount = PrefixCount(encoding) * MapCount(encoding);
        if (index >= tableCount)
        {
            throw new ArgumentException("Invalid map or prefix for the given encoding.");
        }

        return Lut[encoding] + index;
    }

    public static string FormatTableString(InstructionEncoding encoding, OpcodeMap map, RefiningPrefix? prefix)
    {
        var mapString = map switch
        {
            OpcodeMap.MAP0 => null,
            OpcodeMap.M0F => "0F",
            OpcodeMap.M0F38 => "0F38",
            OpcodeMap.M0F3A => "0F3A",
            OpcodeMap.MAP4 => "MAP4",
            OpcodeMap.MAP5 => "MAP5",
            OpcodeMap.MAP6 => "MAP6",
            OpcodeMap.MAP7 => "MAP7",
            OpcodeMap.M0F0F => "0F0F",
            OpcodeMap.XOP8 => "XOP8",
            OpcodeMap.XOP9 => "XOP9",
            OpcodeMap.XOPA => "XOPA",
            _ => "???"
        };

        if (encoding is InstructionEncoding.Default)
        {
            return mapString ?? "PRIMARY";
        }

#pragma warning disable IDE0055
        var encodingString = encoding switch
        {
            InstructionEncoding.Default  => throw new UnreachableException(),
            InstructionEncoding.AMD3DNOW => "3DNW",
            InstructionEncoding.VEX      => "VEX",
            InstructionEncoding.EVEX     => "EVEX",
            InstructionEncoding.MVEX     => "MVEX",
            InstructionEncoding.XOP      => "XOP",
            _ => "???"
        };

        var prefixString = prefix switch
        {
            null               => throw new UnreachableException(/* Only valid for DEFAULT encoding */),
            RefiningPrefix.PNP => "NP",
            RefiningPrefix.P66 => "66",
            RefiningPrefix.PF3 => "F3",
            RefiningPrefix.PF2 => "F2",
            _ => "???"
        };

#pragma warning restore IDE0055

        return $"{encodingString}_{prefixString}{((mapString is null) ? null : $"_{mapString}")}";
    }

    private static IEnumerable<OpcodeMap> SupportedMaps(InstructionEncoding encoding)
    {
        return encoding switch
        {
            InstructionEncoding.Default => [
                OpcodeMap.MAP0,
                OpcodeMap.M0F,
                OpcodeMap.M0F38,
                OpcodeMap.M0F3A
            ],
            InstructionEncoding.AMD3DNOW => [
                OpcodeMap.M0F0F
            ],
            InstructionEncoding.VEX => [
                OpcodeMap.MAP0,
                OpcodeMap.M0F,
                OpcodeMap.M0F38,
                OpcodeMap.M0F3A
            ],
            InstructionEncoding.EVEX => [
                OpcodeMap.MAP0,
                OpcodeMap.M0F,
                OpcodeMap.M0F38,
                OpcodeMap.M0F3A,
                OpcodeMap.MAP4,
                OpcodeMap.MAP5,
                OpcodeMap.MAP6,
                OpcodeMap.MAP7
            ],
            InstructionEncoding.MVEX => [
                OpcodeMap.MAP0,
                OpcodeMap.M0F,
                OpcodeMap.M0F38,
                OpcodeMap.M0F3A
            ],
            InstructionEncoding.XOP => [
                OpcodeMap.XOP8,
                OpcodeMap.XOP9,
                OpcodeMap.XOPA
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
        };
    }

    private static IEnumerable<RefiningPrefix?> SupportedPrefixes(InstructionEncoding encoding)
    {
        return encoding switch
        {
            InstructionEncoding.Default => [
                null
            ],
            InstructionEncoding.AMD3DNOW => [
                RefiningPrefix.PNP
            ],
            InstructionEncoding.VEX => [
                RefiningPrefix.PNP,
                RefiningPrefix.P66,
                RefiningPrefix.PF3,
                RefiningPrefix.PF2
            ],
            InstructionEncoding.EVEX => [
                RefiningPrefix.PNP,
                RefiningPrefix.P66,
                RefiningPrefix.PF3,
                RefiningPrefix.PF2
            ],
            InstructionEncoding.MVEX => [
                RefiningPrefix.PNP,
                RefiningPrefix.P66,
                RefiningPrefix.PF3,
                RefiningPrefix.PF2
            ],
            InstructionEncoding.XOP => [
                RefiningPrefix.PNP
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
        };
    }

    private static int MapCount(InstructionEncoding encoding)
    {
        return SupportedMaps(encoding).Count();
    }

    private static int PrefixCount(InstructionEncoding encoding)
    {
        return SupportedPrefixes(encoding).Count();
    }

    private static int MapIndex(OpcodeMap map)
    {
        return map switch
        {
#pragma warning disable IDE0055
            OpcodeMap.MAP0  => 0, // ......................
            OpcodeMap.M0F   => 1, //    :                 :
            OpcodeMap.M0F38 => 2, //    :- Legacy | VEX   :
            OpcodeMap.M0F3A => 3, // ...:                 :- EVEX | MVEX
            OpcodeMap.MAP4  => 4, //                      :
            OpcodeMap.MAP5  => 5, //                      :
            OpcodeMap.MAP6  => 6, //                      :
            OpcodeMap.MAP7  => 7, // .....................:
            OpcodeMap.M0F0F => 0, // ....- 3DNow!
            OpcodeMap.XOP8  => 0, // ....
            OpcodeMap.XOP9  => 1, //    :- XOP
            OpcodeMap.XOPA  => 2, // ...:
#pragma warning restore IDE0055
            _ => throw new ArgumentOutOfRangeException(nameof(map), map, null)
        };
    }

    private static int PrefixIndex(RefiningPrefix? prefix)
    {
        return prefix switch
        {
#pragma warning disable IDE0055
            null               => 0,
            RefiningPrefix.PNP => 0,
            RefiningPrefix.P66 => 1,
            RefiningPrefix.PF3 => 2,
            RefiningPrefix.PF2 => 3,
#pragma warning restore IDE0055
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null)
        };
    }

    private static IEnumerable<KeyValuePair<InstructionEncoding, int>> GenerateEncodingLookupIndices()
    {
        var value = 0;

        foreach (var encoding in Enum.GetValues<InstructionEncoding>())
        {
            yield return new(encoding, value);
            value += PrefixCount(encoding) * MapCount(encoding);
        }
    }

    private static IEnumerable<ValueTuple<T1, T2, T3>> CartesianProduct<T1, T2, T3>(IEnumerable<T1> seq1, IEnumerable<T2> seq2,
        IEnumerable<T3> seq3)
    {
        return seq1
            .SelectMany(_ => seq2, ValueTuple.Create)
            .SelectMany(_ => seq3, (a, b) => ValueTuple.Create(a.Item1, a.Item2, b));
    }
}

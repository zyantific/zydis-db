using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<EvexTupleType>))]
public enum EvexTupleType
{
    [JsonStringEnumMemberName("invalid")]
    Invalid,

    [JsonStringEnumMemberName("no_scale")]
    NoScale,

    [JsonStringEnumMemberName("fv")]
    FV,

    [JsonStringEnumMemberName("hv")]
    HV,

    [JsonStringEnumMemberName("fvm")]
    FVM,

    [JsonStringEnumMemberName("t1s")]
    T1S,

    [JsonStringEnumMemberName("t1f")]
    T1F,

    [JsonStringEnumMemberName("gscat")]
    GSCAT,

    [JsonStringEnumMemberName("t2")]
    T2,

    [JsonStringEnumMemberName("t4")]
    T4,

    [JsonStringEnumMemberName("t8")]
    T8,

    [JsonStringEnumMemberName("hvm")]
    HVM,

    [JsonStringEnumMemberName("qvm")]
    QVM,

    [JsonStringEnumMemberName("ovm")]
    OVM,

    [JsonStringEnumMemberName("m128")]
    M128,

    [JsonStringEnumMemberName("dup")]
    DUP,

    [JsonStringEnumMemberName("t1_4x")]
    T14X,

    [JsonStringEnumMemberName("quarter")]
    QUARTER
}

public static class EvexTupleTypeExtensions
{
    public static string ToZydisString(this EvexTupleType value)
    {
        return value switch
        {
            EvexTupleType.Invalid => "INVALID",
            EvexTupleType.NoScale => "NO_SCALE",
            EvexTupleType.FV => "FV",
            EvexTupleType.HV => "HV",
            EvexTupleType.FVM => "FVM",
            EvexTupleType.T1S => "T1S",
            EvexTupleType.T1F => "T1F",
            EvexTupleType.GSCAT => "GSCAT",
            EvexTupleType.T2 => "T2",
            EvexTupleType.T4 => "T4",
            EvexTupleType.T8 => "T8",
            EvexTupleType.HVM => "HVM",
            EvexTupleType.QVM => "QVM",
            EvexTupleType.OVM => "OVM",
            EvexTupleType.M128 => "M128",
            EvexTupleType.DUP => "DUP",
            EvexTupleType.T14X => "T1_4X",
            EvexTupleType.QUARTER => "QUARTER",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<MvexFunctionality>))]
public enum MvexFunctionality
{
    [JsonStringEnumMemberName("ignored")]
    Ignored,

    [JsonStringEnumMemberName("invalid")]
    Invalid,

    [JsonStringEnumMemberName("rc")]
    RC,

    [JsonStringEnumMemberName("sae")]
    SAE,

    [JsonStringEnumMemberName("f32")]
    F32,            // No special operation (float32 elements)

    [JsonStringEnumMemberName("f64")]
    F64,            // No special operation (float64 elements)

    [JsonStringEnumMemberName("i32")]
    I32,            // No special operation (uint32 elements)

    [JsonStringEnumMemberName("i64")]
    I64,            // No special operation (uint64 elements)

    [JsonStringEnumMemberName("swizzle32")]
    Swizzle32,      // Sf32 / Si32 (register only)

    [JsonStringEnumMemberName("swizzle64")]
    Swizzle64,      // Sf64 / Si64 (register only)

    [JsonStringEnumMemberName("s_f32")]
    Sf32,           // (memory only)

    [JsonStringEnumMemberName("s_f32_bcst")]
    Sf32Bcst,       // (memory only, broadcast only)

    [JsonStringEnumMemberName("s_f32_bcst4to16")]
    Sf32Bcst4to16,  // (memory only, broadcast 4to16 only)

    [JsonStringEnumMemberName("s_f64")]
    Sf64,           // (memory only)

    [JsonStringEnumMemberName("s_i32")]
    Si32,           // (memory only)

    [JsonStringEnumMemberName("s_i32_bcst")]
    Si32Bcst,       // (memory only, broadcast only)

    [JsonStringEnumMemberName("s_i32_bcst4to16")]
    Si32Bcst4to16,  // (memory only, broadcast 4to16 only)

    [JsonStringEnumMemberName("si_64")]
    Si64,           // (memory only)

    [JsonStringEnumMemberName("u_f32")]
    Uf32,

    [JsonStringEnumMemberName("u_f64")]
    Uf64,

    [JsonStringEnumMemberName("u_i32")]
    Ui32,

    [JsonStringEnumMemberName("u_i64")]
    Ui64,

    [JsonStringEnumMemberName("d_f32")]
    Df32,

    [JsonStringEnumMemberName("d_f64")]
    Df64,

    [JsonStringEnumMemberName("d_i32")]
    Di32,

    [JsonStringEnumMemberName("d_i64")]
    Di64
}

public static class MvexFunctionalityExtensions
{
    public static string ToZydisString(this MvexFunctionality value)
    {
        return value switch
        {
            MvexFunctionality.Ignored => "IGNORED",
            MvexFunctionality.Invalid => "INVALID",
            MvexFunctionality.RC => "RC",
            MvexFunctionality.SAE => "SAE",
            MvexFunctionality.F32 => "F_32",
            MvexFunctionality.I32 => "I_32",
            MvexFunctionality.F64 => "F_64",
            MvexFunctionality.I64 => "I_64",
            MvexFunctionality.Swizzle32 => "SWIZZLE_32",
            MvexFunctionality.Swizzle64 => "SWIZZLE_64",
            MvexFunctionality.Sf32 => "SF_32",
            MvexFunctionality.Sf32Bcst => "SF_32_BCST",
            MvexFunctionality.Sf32Bcst4to16 => "SF_32_BCST_4TO16",
            MvexFunctionality.Sf64 => "SF_64",
            MvexFunctionality.Si32 => "SI_32",
            MvexFunctionality.Si32Bcst => "SI_32_BCST",
            MvexFunctionality.Si32Bcst4to16 => "SI_32_BCST_4TO16",
            MvexFunctionality.Si64 => "SI_64",
            MvexFunctionality.Uf32 => "UF_32",
            MvexFunctionality.Uf64 => "UF_64",
            MvexFunctionality.Ui32 => "UI_32",
            MvexFunctionality.Ui64 => "UI_64",
            MvexFunctionality.Df32 => "DF_32",
            MvexFunctionality.Df64 => "DF_64",
            MvexFunctionality.Di32 => "DI_32",
            MvexFunctionality.Di64 => "DI_64",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

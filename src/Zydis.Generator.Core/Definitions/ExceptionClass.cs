using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(JsonStringEnumConverter<ExceptionClass>))]
public enum ExceptionClass
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("sse1")]
    SSE1,

    [JsonStringEnumMemberName("sse2")]
    SSE2,

    [JsonStringEnumMemberName("sse3")]
    SSE3,

    [JsonStringEnumMemberName("sse4")]
    SSE4,

    [JsonStringEnumMemberName("sse5")]
    SSE5,

    [JsonStringEnumMemberName("sse7")]
    SSE7,

    [JsonStringEnumMemberName("avx1")]
    AVX1,

    [JsonStringEnumMemberName("avx2")]
    AVX2,

    [JsonStringEnumMemberName("avx3")]
    AVX3,

    [JsonStringEnumMemberName("avx4")]
    AVX4,

    [JsonStringEnumMemberName("avx5")]
    AVX5,

    [JsonStringEnumMemberName("avx6")]
    AVX6,

    [JsonStringEnumMemberName("avx7")]
    AVX7,

    [JsonStringEnumMemberName("avx8")]
    AVX8,

    [JsonStringEnumMemberName("avx11")]
    AVX11,

    [JsonStringEnumMemberName("avx12")]
    AVX12,

    [JsonStringEnumMemberName("avx14")]
    AVX14,

    [JsonStringEnumMemberName("e1")]
    E1,

    [JsonStringEnumMemberName("e1nf")]
    E1NF,

    [JsonStringEnumMemberName("e2")]
    E2,

    [JsonStringEnumMemberName("e2nf")]
    E2NF,

    [JsonStringEnumMemberName("e3")]
    E3,

    [JsonStringEnumMemberName("e3nf")]
    E3NF,

    [JsonStringEnumMemberName("e4")]
    E4,

    [JsonStringEnumMemberName("e4nf")]
    E4NF,

    [JsonStringEnumMemberName("e5")]
    E5,

    [JsonStringEnumMemberName("e5nf")]
    E5NF,

    [JsonStringEnumMemberName("e6")]
    E6,

    [JsonStringEnumMemberName("e6nf")]
    E6NF,

    [JsonStringEnumMemberName("e7nm")]
    E7NM,

    [JsonStringEnumMemberName("e7nm128")]
    E7NM128,

    [JsonStringEnumMemberName("e9nf")]
    E9NF,

    [JsonStringEnumMemberName("e10")]
    E10,

    [JsonStringEnumMemberName("e10nf")]
    E10NF,

    [JsonStringEnumMemberName("e11")]
    E11,

    [JsonStringEnumMemberName("e11nf")]
    E11NF,

    [JsonStringEnumMemberName("e12")]
    E12,

    [JsonStringEnumMemberName("e12np")]
    E12NP,

    [JsonStringEnumMemberName("k20")]
    K20,

    [JsonStringEnumMemberName("k21")]
    K21,

    [JsonStringEnumMemberName("amxe1")]
    AMXE1,

    [JsonStringEnumMemberName("amxe2")]
    AMXE2,

    [JsonStringEnumMemberName("amxe3")]
    AMXE3,

    [JsonStringEnumMemberName("amxe4")]
    AMXE4,

    [JsonStringEnumMemberName("amxe5")]
    AMXE5,

    [JsonStringEnumMemberName("amxe6")]
    AMXE6,

    [JsonStringEnumMemberName("amxe1_evex")]
    AMXE1EVEX,

    [JsonStringEnumMemberName("amxe2_evex")]
    AMXE2EVEX,

    [JsonStringEnumMemberName("amxe3_evex")]
    AMXE3EVEX,

    [JsonStringEnumMemberName("amxe4_evex")]
    AMXE4EVEX,

    [JsonStringEnumMemberName("amxe5_evex")]
    AMXE5EVEX,

    [JsonStringEnumMemberName("amxe6_evex")]
    AMXE6EVEX,

    [JsonStringEnumMemberName("apx_evex_int")]
    APXEVEXINT,

    [JsonStringEnumMemberName("apx_evex_keylocker")]
    APXEVEXKEYLOCKER,

    [JsonStringEnumMemberName("apx_evex_bmi")]
    APXEVEXBMI,

    [JsonStringEnumMemberName("apx_evex_ccmp")]
    APXEVEXCCMP,

    [JsonStringEnumMemberName("apx_evex_cfcmov")]
    APXEVEXCFCMOV,

    [JsonStringEnumMemberName("apx-evex-cmpccxadd")]
    APXEVEXCMPCCXADD,

    [JsonStringEnumMemberName("apx-evex-enqcmd")]
    APXEVEXENQCMD,

    [JsonStringEnumMemberName("apx-evex-invept")]
    APXEVEXINVEPT,

    [JsonStringEnumMemberName("apx-evex-invpcid")]
    APXEVEXINVPCID,

    [JsonStringEnumMemberName("apx-evex-invvpid")]
    APXEVEXINVVPID,

    [JsonStringEnumMemberName("apx-evex-kmov")]
    APXEVEXKMOV,

    [JsonStringEnumMemberName("apx-evex-pp2")]
    APXEVEXPP2,

    [JsonStringEnumMemberName("apx-evex-sha")]
    APXEVEXSHA,

    [JsonStringEnumMemberName("apx-evex-cet-wrss")]
    APXEVEXCETWRSS,

    [JsonStringEnumMemberName("apx-evex-cet-wruss")]
    APXEVEXCETWRUSS,

    [JsonStringEnumMemberName("apx-legacy-jmpabs")]
    APXLEGACYJMPABS,

    [JsonStringEnumMemberName("apx-evex-rao-int")]
    APXEVEXRAOINT,

    [JsonStringEnumMemberName("user-msr-evex")]
    USERMSREVEX,

    [JsonStringEnumMemberName("legacy-rao-int")]
    LEGACYRAOINT
}

public static class ExceptionClassExtensions
{
    public static string ToZydisString(this ExceptionClass value)
    {
        return value switch
        {
            ExceptionClass.None => "NONE",
            ExceptionClass.SSE1 => "SSE1",
            ExceptionClass.SSE2 => "SSE2",
            ExceptionClass.SSE3 => "SSE3",
            ExceptionClass.SSE4 => "SSE4",
            ExceptionClass.SSE5 => "SSE5",
            ExceptionClass.SSE7 => "SSE7",
            ExceptionClass.AVX1 => "AVX1",
            ExceptionClass.AVX2 => "AVX2",
            ExceptionClass.AVX3 => "AVX3",
            ExceptionClass.AVX4 => "AVX4",
            ExceptionClass.AVX5 => "AVX5",
            ExceptionClass.AVX6 => "AVX6",
            ExceptionClass.AVX7 => "AVX7",
            ExceptionClass.AVX8 => "AVX8",
            ExceptionClass.AVX11 => "AVX11",
            ExceptionClass.AVX12 => "AVX12",
            ExceptionClass.AVX14 => "AVX14",
            ExceptionClass.E1 => "E1",
            ExceptionClass.E1NF => "E1NF",
            ExceptionClass.E2 => "E2",
            ExceptionClass.E2NF => "E2NF",
            ExceptionClass.E3 => "E3",
            ExceptionClass.E3NF => "E3NF",
            ExceptionClass.E4 => "E4",
            ExceptionClass.E4NF => "E4NF",
            ExceptionClass.E5 => "E5",
            ExceptionClass.E5NF => "E5NF",
            ExceptionClass.E6 => "E6",
            ExceptionClass.E6NF => "E6NF",
            ExceptionClass.E7NM => "E7NM",
            ExceptionClass.E7NM128 => "E7NM128",
            ExceptionClass.E9NF => "E9NF",
            ExceptionClass.E10 => "E10",
            ExceptionClass.E10NF => "E10NF",
            ExceptionClass.E11 => "E11",
            ExceptionClass.E11NF => "E11NF",
            ExceptionClass.E12 => "E12",
            ExceptionClass.E12NP => "E12NP",
            ExceptionClass.K20 => "K20",
            ExceptionClass.K21 => "K21",
            ExceptionClass.AMXE1 => "AMXE1",
            ExceptionClass.AMXE2 => "AMXE2",
            ExceptionClass.AMXE3 => "AMXE3",
            ExceptionClass.AMXE4 => "AMXE4",
            ExceptionClass.AMXE5 => "AMXE5",
            ExceptionClass.AMXE6 => "AMXE6",
            ExceptionClass.AMXE1EVEX => "AMXE1_EVEX",
            ExceptionClass.AMXE2EVEX => "AMXE2_EVEX",
            ExceptionClass.AMXE3EVEX => "AMXE3_EVEX",
            ExceptionClass.AMXE4EVEX => "AMXE4_EVEX",
            ExceptionClass.AMXE5EVEX => "AMXE5_EVEX",
            ExceptionClass.AMXE6EVEX => "AMXE6_EVEX",
            ExceptionClass.APXEVEXINT => "APX_EVEX_INT",
            ExceptionClass.APXEVEXKEYLOCKER => "APX_EVEX_KEYLOCKER",
            ExceptionClass.APXEVEXBMI => "APX_EVEX_BMI",
            ExceptionClass.APXEVEXCCMP => "APX_EVEX_CCMP",
            ExceptionClass.APXEVEXCFCMOV => "APX_EVEX_CFCMOV",
            ExceptionClass.APXEVEXCMPCCXADD => "APX_EVEX_CMPCCXADD",
            ExceptionClass.APXEVEXENQCMD => "APX_EVEX_ENQCMD",
            ExceptionClass.APXEVEXINVEPT => "APX_EVEX_INVEPT",
            ExceptionClass.APXEVEXINVPCID => "APX_EVEX_INVPCID",
            ExceptionClass.APXEVEXINVVPID => "APX_EVEX_INVVPID",
            ExceptionClass.APXEVEXKMOV => "APX_EVEX_KMOV",
            ExceptionClass.APXEVEXPP2 => "APX_EVEX_PP2",
            ExceptionClass.APXEVEXSHA => "APX_EVEX_SHA",
            ExceptionClass.APXEVEXCETWRSS => "APX_EVEX_CET_WRSS",
            ExceptionClass.APXEVEXCETWRUSS => "APX_EVEX_CET_WRUSS",
            ExceptionClass.APXLEGACYJMPABS => "APX_LEGACY_JMPABS",
            ExceptionClass.APXEVEXRAOINT => "APX_EVEX_RAO_INT",
            ExceptionClass.USERMSREVEX => "USER_MSR_EVEX",
            ExceptionClass.LEGACYRAOINT => "LEGACY_RAO_INT",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

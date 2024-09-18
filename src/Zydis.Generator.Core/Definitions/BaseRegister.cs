using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1707 // Identifier contains underscores

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<BaseRegister>))]
public enum BaseRegister
{
    AGPR_REG,
    AGPR_RM,
    [JsonStringEnumMemberName("ax")] AAX,
    [JsonStringEnumMemberName("dx")] ADX,
    [JsonStringEnumMemberName("bx")] ABX,
    [JsonStringEnumMemberName("si")] ASI,
    [JsonStringEnumMemberName("di")] ADI,
    [JsonStringEnumMemberName("sp")] SSP,
    [JsonStringEnumMemberName("bp")] SBP
}

// ReSharper restore InconsistentNaming

#pragma warning restore CA1707 // Identifier contains underscores

public static class BaseRegisterExtensions
{
    public static string ToZydisString(this BaseRegister value)
    {
        return value switch
        {
            BaseRegister.AGPR_REG => "AGPR_REG",
            BaseRegister.AGPR_RM => "AGPR_RM",
            BaseRegister.AAX => "AAX",
            BaseRegister.ADX => "ADX",
            BaseRegister.ABX => "ABX",
            BaseRegister.ASI => "ASI",
            BaseRegister.ADI => "ADI",
            BaseRegister.SSP => "SSP",
            BaseRegister.SBP => "SBP",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

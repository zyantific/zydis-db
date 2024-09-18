using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<StaticBroadcast>))]
public enum StaticBroadcast
{
    None,
    [JsonStringEnumMemberName("1to2" )] Broadcast1to2,
    [JsonStringEnumMemberName("1to4" )] Broadcast1to4,
    [JsonStringEnumMemberName("1to8" )] Broadcast1to8,
    [JsonStringEnumMemberName("1to16")] Broadcast1to16,
    [JsonStringEnumMemberName("1to32")] Broadcast1to32,
    [JsonStringEnumMemberName("1to64")] Broadcast1to64,
    [JsonStringEnumMemberName("2to4" )] Broadcast2to4,
    [JsonStringEnumMemberName("2to8" )] Broadcast2to8,
    [JsonStringEnumMemberName("2to16")] Broadcast2to16,
    [JsonStringEnumMemberName("4to8" )] Broadcast4to8,
    [JsonStringEnumMemberName("4to16")] Broadcast4to16,
    [JsonStringEnumMemberName("8to16")] Broadcast8to16
}

// ReSharper restore InconsistentNaming

public static class StaticBroadcastExtensions
{
    public static string ToZydisString(this StaticBroadcast value)
    {
        return value switch
        {
            StaticBroadcast.None => "NONE",
            StaticBroadcast.Broadcast1to2  => "1_TO_2",
            StaticBroadcast.Broadcast1to4  => "1_TO_4",
            StaticBroadcast.Broadcast1to8  => "1_TO_8",
            StaticBroadcast.Broadcast1to16 => "1_TO_16",
            StaticBroadcast.Broadcast1to32 => "1_TO_32",
            StaticBroadcast.Broadcast1to64 => "1_TO_64",
            StaticBroadcast.Broadcast2to4  => "2_TO_4",
            StaticBroadcast.Broadcast2to8  => "2_TO_8",
            StaticBroadcast.Broadcast2to16 => "2_TO_16",
            StaticBroadcast.Broadcast4to8  => "4_TO_8",
            StaticBroadcast.Broadcast4to16 => "4_TO_16",
            StaticBroadcast.Broadcast8to16 => "8_TO_16",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

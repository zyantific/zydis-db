using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonConverter(typeof(SnakeCaseStringEnumConverter<OperandAccess>))]
public enum OperandAccess
{
    /// <summary>
    /// The operand is read.
    /// </summary>
    [JsonStringEnumMemberName("read")]
    Read,

    /// <summary>
    /// The operand is written.
    /// </summary>
    [JsonStringEnumMemberName("write")]
    Write,

    /// <summary>
    /// The operand is read and written.
    /// </summary>
    [JsonStringEnumMemberName("read_write")]
    ReadWrite,

    /// <summary>
    /// The operand is only read conditionally.
    /// </summary>
    [JsonStringEnumMemberName("condread")]
    CondRead,

    /// <summary>
    /// The operand is only written conditionally.
    /// </summary>
    [JsonStringEnumMemberName("condwrite")]
    CondWrite,

    /// <summary>
    /// The operand is always read but only conditionally written.
    /// </summary>
    [JsonStringEnumMemberName("read_condwrite")]
    ReadCondWrite,

    /// <summary>
    /// The operand is only read conditionally but always written.
    /// </summary>
    [JsonStringEnumMemberName("condread_write")]
    CondReadWrite
}

public static class OperandAccessExtensions
{
    public static string ToZydisString(this OperandAccess value)
    {
        return value switch
        {
            OperandAccess.Read => "READ",
            OperandAccess.Write => "WRITE",
            OperandAccess.ReadWrite => "READWRITE",
            OperandAccess.CondRead => "CONDREAD",
            OperandAccess.CondWrite => "CONDWRITE",
            OperandAccess.ReadCondWrite => "READ_CONDWRITE",
            OperandAccess.CondReadWrite => "CONDREAD_WRITE",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

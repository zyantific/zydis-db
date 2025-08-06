using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Helpers;
using Zydis.Generator.Core.Serialization;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1036

[JsonConverter(typeof(InstructionFlagsConverterFactory))]
public sealed record InstructionFlags :
    IComparable<InstructionFlags>,
    IComparable

#pragma warning restore CA1036
{
    public InstructionFlagsAccess Access { get; init; }
    public IReadOnlyDictionary<InstructionFlag, InstructionFlagOperation> Flags { get; init; } = FrozenDictionary<InstructionFlag, InstructionFlagOperation>.Empty;

    public IEnumerable<InstructionOperand> GetFlagsRegisterOperands()
    {
        var cpuAccess = GetFlagsRegisterOperandAccess(x => x.IsCpuFlag());
        if (cpuAccess is not null)
        {
            yield return new InstructionOperand
            {
                Access = cpuAccess.Value,
                Type = OperandType.ImplicitReg,
                Register = Register.SSZFLAGS,
                ElementType = ElementType.INT1,
                IsVisible = false
            };
        }

        var fpuAccess = GetFlagsRegisterOperandAccess(x => x.IsFpuFlag());
        if (fpuAccess is not null)
        {
            yield return new InstructionOperand
            {
                Access = fpuAccess.Value,
                Type = OperandType.ImplicitReg,
                Register = Register.regX87STATUS,
                ElementType = ElementType.Struct,
                IsVisible = false
            };
        }
    }

    private OperandAccess? GetFlagsRegisterOperandAccess(Predicate<InstructionFlag> predicate)
    {
        var doesRead = false;
        var doesWrite = false;

        foreach (var (_, operation) in Flags.Where(x => predicate(x.Key)))
        {
            if (operation is InstructionFlagOperation.Tested or InstructionFlagOperation.TestedModified)
            {
                doesRead = true;
                continue;
            }

            if (operation is InstructionFlagOperation.TestedModified
                or InstructionFlagOperation.Modified
                or InstructionFlagOperation.Set0
                or InstructionFlagOperation.Set1
                or InstructionFlagOperation.Undefined)
            {
                doesWrite = true;
            }
        }

        return (doesRead, doesWrite) switch
        {
            (true, true) => (Access is InstructionFlagsAccess.MayWrite) ? OperandAccess.ReadCondWrite : OperandAccess.ReadWrite,
            (true, false) => OperandAccess.Read,
            (false, true) => (Access is InstructionFlagsAccess.MayWrite) ? OperandAccess.CondWrite : OperandAccess.Write,
            _ => null
        };
    }

    public int CompareTo(InstructionFlags? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Access),
            x => x.CompareDictionary(x => x.Flags)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as InstructionFlags);
    }
}

[JsonConverter(typeof(SnakeCaseStringEnumConverter<InstructionFlagsAccess>))]
public enum InstructionFlagsAccess
{
    [JsonStringEnumMemberName("read_only")]
    ReadOnly,

    [JsonStringEnumMemberName("must_write")]
    MustWrite,

    [JsonStringEnumMemberName("may_write")]
    MayWrite
}

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<InstructionFlag>))]
public enum InstructionFlag
{
    CF,
    PF,
    AF,
    ZF,
    SF,
    TF,
    IF,
    DF,
    OF,
    IOPL,
    NT,
    RF,
    VM,
    AC,
    VIF,
    VIP,
    ID,
    C0,
    C1,
    C2,
    C3
}

public static class InstructionFlagExtensions
{
    public static bool IsCpuFlag(this InstructionFlag value)
    {
        return !IsFpuFlag(value);
    }

    public static bool IsFpuFlag(this InstructionFlag value)
    {
        return value is InstructionFlag.C0 or InstructionFlag.C1 or InstructionFlag.C2 or InstructionFlag.C3;
    }
}

// ReSharper restore InconsistentNaming

[JsonConverter(typeof(SnakeCaseStringEnumConverter<InstructionFlagOperation>))]
public enum InstructionFlagOperation
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("t")]
    Tested,

    [JsonStringEnumMemberName("t_m")]
    TestedModified,

    [JsonStringEnumMemberName("m")]
    Modified,

    [JsonStringEnumMemberName("0")]
    Set0,

    [JsonStringEnumMemberName("1")]
    Set1,

    [JsonStringEnumMemberName("u")]
    Undefined
}

internal sealed class InstructionFlagsConverterFactory :
    JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return (typeToConvert == typeof(InstructionFlags));
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new InstructionFlagsConverter(options);
    }
}

internal sealed class InstructionFlagsConverter :
    JsonConverter<InstructionFlags>
{
    private readonly JsonConverter<InstructionFlagsAccess> _accessConverter;
    private readonly JsonConverter<InstructionFlag> _keyConverter;
    private readonly JsonConverter<InstructionFlagOperation> _valueConverter;

    public InstructionFlagsConverter(JsonSerializerOptions options)
    {
        _accessConverter = (JsonConverter<InstructionFlagsAccess>)options.GetConverter(typeof(InstructionFlagsAccess));
        _keyConverter = (JsonConverter<InstructionFlag>)options.GetConverter(typeof(InstructionFlag));
        _valueConverter = (JsonConverter<InstructionFlagOperation>)options.GetConverter(typeof(InstructionFlagOperation));
    }

    public override InstructionFlags? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.ThrowIfTokenNotEquals(JsonTokenType.StartObject);
        reader.Read();

        var access = InstructionFlagsAccess.ReadOnly;
        var flags = new Dictionary<InstructionFlag, InstructionFlagOperation>();

        while (reader.TokenType is JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            if (name is null)
            {
                throw new InvalidDataException("");
            }

            if (string.Equals(name, "access", StringComparison.Ordinal))
            {
                reader.Read();
                access = _accessConverter.Read(ref reader, typeof(InstructionFlagsAccess), options);
                reader.Read();
                continue;
            }

            var key = _keyConverter.ReadAsPropertyName(ref reader, typeof(InstructionFlag), options);
            reader.Read();
            var value = _valueConverter.Read(ref reader, typeof(InstructionFlagOperation), options);
            reader.Read();

            flags[key] = value;
        }

        reader.ThrowIfTokenNotEquals(JsonTokenType.EndObject);

        return new InstructionFlags { Access = access, Flags = flags };
    }

    public override void Write(Utf8JsonWriter writer, InstructionFlags value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}

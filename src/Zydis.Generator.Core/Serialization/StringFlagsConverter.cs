using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Serialization;

internal sealed class StringFlagsConverterFactory<TEnum> :
    JsonConverterFactory
    where TEnum : struct, Enum
{
    public override bool CanConvert(Type typeToConvert)
    {
        return (typeToConvert == typeof(TEnum)) && (typeof(TEnum).GetCustomAttribute<FlagsAttribute>() is not null);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new StringFlagsConverter<TEnum>(options.PropertyNamingPolicy, options.PropertyNameCaseInsensitive);
    }
}

internal sealed class StringFlagsConverter<TEnum> :
    JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly JsonConverter<TEnum> _valueConverter;

    public StringFlagsConverter() : this(null, false)
    {
    }

    public StringFlagsConverter(JsonNamingPolicy? namingPolicy = null, bool ignoreCase = false)
    {
        _valueConverter = new StringEnumConverter<TEnum>(namingPolicy, ignoreCase);
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = default(TEnum);

        reader.ThrowIfTokenNotEquals(JsonTokenType.StartArray);
        reader.Read();

        while (reader.TokenType is JsonTokenType.String)
        {
            var flag = _valueConverter.Read(ref reader, typeToConvert, options);
            SetFlag(ref result, flag);

            reader.Read();
        }

        reader.ThrowIfTokenNotEquals(JsonTokenType.EndArray);

        return result;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        // An explicit "no flags set" value (distinct from the property being entirely absent, e.g. a
        // nullable flags value set to the zero member) is still written as an empty array. The reference
        // file formats it across two lines at the enclosing indent depth rather than the compact "[]"
        // this writer would otherwise produce, so that case is special-cased below.
        if (Convert.ToUInt64(value) is 0 && writer.Options.Indented)
        {
            var depth = writer.CurrentDepth;
            var indent = new string(writer.Options.IndentCharacter, depth * writer.Options.IndentSize);
            writer.WriteRawValue($"[{writer.Options.NewLine}{indent}]", skipInputValidation: true);
            return;
        }

        writer.WriteStartArray();

        // Enumerate in declaration order (ascending bit value) since a bitmask carries no record of the
        // order flags were originally written in.
        foreach (var flag in Enum.GetValues<TEnum>())
        {
            if (Convert.ToUInt64(flag) is 0)
            {
                continue;
            }

            if (value.HasFlag(flag))
            {
                _valueConverter.Write(writer, flag, options);
            }
        }

        writer.WriteEndArray();
    }

    private static void SetFlag<T>(ref T value, T flag) where T : Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) == typeof(ulong))
        {
            var numericValue = Convert.ToUInt64(value);
            numericValue |= Convert.ToUInt64(flag);
            value = (T)Enum.ToObject(typeof(T), numericValue);
        }
        else
        {
            var numericValue = Convert.ToInt64(value);
            numericValue |= Convert.ToInt64(flag);
            value = (T)Enum.ToObject(typeof(T), numericValue);
        }
    }
}

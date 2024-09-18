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
        throw new NotImplementedException();
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

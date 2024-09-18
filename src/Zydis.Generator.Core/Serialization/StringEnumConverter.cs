using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Serialization;

internal class StringEnumConverterFactory<TEnum> :
    JsonConverterFactory
    where TEnum : struct, Enum
{
    public override bool CanConvert(Type typeToConvert)
    {
        return (typeToConvert == typeof(TEnum));
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new StringEnumConverter<TEnum>(options.PropertyNamingPolicy, options.PropertyNameCaseInsensitive);
    }
}

internal class StringEnumConverter<TEnum> :
    JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<TEnum, string> _enumToString = [];
    private readonly Dictionary<string, TEnum> _stringToEnum;

    public StringEnumConverter() : this(null, false)
    {
    }

    public StringEnumConverter(JsonNamingPolicy? namingPolicy = null, bool ignoreCase = false)
    {
        var comparer = ignoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        _stringToEnum = new Dictionary<string, TEnum>(comparer);

        var type = typeof(TEnum);
        var values = Enum.GetValues<TEnum>();

        foreach (var value in values)
        {
            var memberName = value.ToString();
            var enumMember = type.GetMember(memberName)[0];
            var attr = enumMember.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();

            if (attr?.Name is not null)
            {
                _enumToString.Add(value, attr.Name);
                _stringToEnum.Add(attr.Name, value);
                continue;
            }

            var str = namingPolicy?.ConvertName(memberName) ?? memberName;
            _enumToString.Add(value, str);
            _stringToEnum.Add(str, value);
        }
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var stringValue = reader.GetString();
        if (stringValue is null)
        {
            throw new JsonException($"Could not read string value for '{typeToConvert.Name}' enum member.");
        }

        if (!_stringToEnum.TryGetValue(stringValue, out var result))
        {
            throw new DataException($"Could not convert value '{stringValue}' to enum type '{typeof(TEnum)}'.");
        }

        return result;
    }

    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(_enumToString[value]);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        Write(writer, value, options);
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Serialization;

internal sealed class HexadecimalIntConverter<T> :
    JsonConverter<T>
    where T : struct, IBinaryInteger<T>
{
    private readonly Type _marker = typeof(IBinaryInteger<T>);
    private readonly int _maxLengthInChars;

    public HexadecimalIntConverter()
    {
        var bits = int.CreateChecked(T.PopCount(T.AllBitsSet));

        _maxLengthInChars = bits / 8;
        if (bits % 8 is not 0)
        {
            ++_maxLengthInChars;
        }

        _maxLengthInChars *= 2;
    }

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.FindInterfaces((m, _) => (m == _marker), null).Length is not 0;
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => T.TryParse(reader.ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : throw new InvalidDataException("Failed to parse decimal integer value."),
            JsonTokenType.String => T.TryParse(reader.ValueSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result)
                ? result
                : throw new InvalidDataException("Failed to parse hexadecimal integer value."),
            _ => throw new InvalidDataException($"Expected a 'number' or 'string' token, but got '{reader.TokenType}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Span<char> dst = stackalloc char[_maxLengthInChars];

        if (!value.TryFormat(dst, out var written, $"X{_maxLengthInChars}", CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("Failed to format integer value.");
        }

        writer.WriteStringValue(dst[..written]);
    }
}

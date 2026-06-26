using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace Zydis.Generator.Core.CodeGeneration;

internal static class WriterExtensions
{
    private const string BoolTrue = "ZYAN_TRUE";
    private const string BoolFalse = "ZYAN_FALSE";

    public static void WriteBool(TextWriter writer, bool value)
    {
        writer.Write(value ? BoolTrue : BoolFalse);
    }

    public static void WriteChar(TextWriter writer, char value)
    {
        writer.Write("'{0}'", value switch
        {
            '\0' => "\\0",
            _ => value
        });
    }

    public static void WriteString(TextWriter writer, string value)
    {
        writer.Write("\"{0}\"", value);
    }

    public static void WriteZydisShortStringReference(TextWriter writer, string identifier)
    {
        writer.Write("ZYDIS_SHORTSTRING({0})", identifier);
    }

    public static void WriteInteger<TValue>(TextWriter writer, IBinaryInteger<TValue> value, int length, bool hex)
        where TValue : struct, IBinaryInteger<TValue>
    {
        Span<char> output = stackalloc char[20]; // maximum length of a signed int64
        if (!value.TryFormat(output, out var written, $"{(hex ? "X" : "D")}{length}", CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("Failed to format number");
        }

        if (hex)
        {
            writer.Write("0x");
        }

        writer.Write(output[..written]);
    }

    public static void WriteNull(TextWriter writer)
    {
        writer.Write("ZYAN_NULL");
    }

    public static Task WriteMultilineAsync(TextWriter writer, string? value)
    {
        return writer.WriteLineAsync(value?.ReplaceLineEndings(writer.NewLine));
    }
}

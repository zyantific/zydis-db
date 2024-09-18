using System;
using System.Buffers;
using System.Globalization;

namespace Zydis.Generator.Core.Serialization;

internal static class WordCaseExtensions
{
    private const int StackallocByteThreshold = 256;
    private const int StackallocCharThreshold = StackallocByteThreshold / 2;

    public static string ToCamelCase(this string name)
    {
        return ConvertName(name, WriteWordCamel);

        static int WriteWordCamel(bool first, ReadOnlySpan<char> word, Span<char> destination)
        {
            var written = word.ToLowerInvariant(destination);
            if (written > 0 && !first)
            {
                destination[0] = char.ToUpperInvariant(destination[0]);
            }

            return written;
        }
    }

    public static string ToPascalCase(this string name)
    {
        return ConvertName(name, WriteWordPascal);

        static int WriteWordPascal(bool first, ReadOnlySpan<char> word, Span<char> destination)
        {
            var written = word.ToLowerInvariant(destination);
            if (written > 0)
            {
                destination[0] = char.ToUpperInvariant(destination[0]);
            }

            return written;
        }
    }

    public static string ToSnakeCase(this string name, bool lowercase = true)
    {
        return ToSeparatorCase(name, '_', lowercase);
    }

    public static string ToKebabCase(this string name, bool lowercase = true)
    {
        return ToSeparatorCase(name, '-', lowercase);
    }

    public static string ToSeparatorCase(this string name, char separator, bool lowercase = true)
    {
        return ConvertName(name, WriteWordSeparator);

        int WriteWordSeparator(bool first, ReadOnlySpan<char> word, Span<char> destination)
        {
            var offset = first ? 0 : 1;
            if (offset >= destination.Length)
            {
                return 0;
            }

            if (!first)
            {
                destination[0] = separator;
            }

            destination = destination[offset..];

            var written = lowercase
                ? word.ToLowerInvariant(destination)
                : word.ToUpperInvariant(destination);

            return written + offset;
        }
    }

    private static string ConvertName(string name, TryWriteWord tryWriteWord)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Rented buffer 20% longer that the input.
        var rentedBufferLength = (12 * name.Length) / 10;
        var rentedBuffer = rentedBufferLength > StackallocCharThreshold
            ? ArrayPool<char>.Shared.Rent(rentedBufferLength)
            : null;

        var resultUsedLength = 0;
        var result = rentedBuffer ?? stackalloc char[StackallocCharThreshold];

        var first = 0;
        var chars = name.AsSpan();
        var previousCategory = CharCategory.Boundary;
        for (var index = 0; index < chars.Length; index++)
        {
            var current = chars[index];
            var currentCategoryUnicode = char.GetUnicodeCategory(current);
            if (currentCategoryUnicode is UnicodeCategory.SpaceSeparator or
                (>= UnicodeCategory.ConnectorPunctuation and <= UnicodeCategory.OtherPunctuation))
            {
                WriteWord(chars.Slice(first, index - first), ref result);

                previousCategory = CharCategory.Boundary;
                first = index + 1;

                continue;
            }

            if (index + 1 >= chars.Length)
            {
                continue;
            }

            var next = chars[index + 1];
            var currentCategory = currentCategoryUnicode switch
            {
                UnicodeCategory.LowercaseLetter => CharCategory.Lowercase,
                UnicodeCategory.UppercaseLetter => CharCategory.Uppercase,
                _ => previousCategory
            };

            if ((currentCategory == CharCategory.Lowercase) && char.IsUpper(next) || next == '_')
            {
                WriteWord(chars.Slice(first, index - first + 1), ref result);

                previousCategory = CharCategory.Boundary;
                first = index + 1;

                continue;
            }

            if (previousCategory == CharCategory.Uppercase &&
                currentCategoryUnicode == UnicodeCategory.UppercaseLetter &&
                char.IsLower(next))
            {
                WriteWord(chars.Slice(first, index - first), ref result);

                previousCategory = CharCategory.Boundary;
                first = index;

                continue;
            }

            previousCategory = currentCategory;
        }

        WriteWord(chars[first..], ref result);

        name = result[..resultUsedLength].ToString();

        if (rentedBuffer is not null)
        {
            result[..resultUsedLength].Clear();
            ArrayPool<char>.Shared.Return(rentedBuffer);
        }

        return name;

        void ExpandBuffer(ref Span<char> result)
        {
            var newBuffer = ArrayPool<char>.Shared.Rent(result.Length * 2);

            result.CopyTo(newBuffer);

            if (rentedBuffer is not null)
            {
                result[..resultUsedLength].Clear();
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }

            rentedBuffer = newBuffer;
            result = rentedBuffer;
        }

        void WriteWord(ReadOnlySpan<char> word, ref Span<char> result)
        {
            if (word.IsEmpty)
            {
                return;
            }

            while (true)
            {
                var written = tryWriteWord(resultUsedLength == 0, word, result[resultUsedLength..]);
                if (written > 0)
                {
                    resultUsedLength += written;
                    return;
                }

                ExpandBuffer(ref result);
            }
        }
    }

    private delegate int TryWriteWord(bool first, ReadOnlySpan<char> word, Span<char> destination);

    private enum CharCategory
    {
        Boundary,
        Lowercase,
        Uppercase,
    }
}

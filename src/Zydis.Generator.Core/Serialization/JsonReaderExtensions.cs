using System.IO;
using System.Text.Json;

namespace Zydis.Generator.Core.Serialization;

internal static class JsonReaderExtensions
{
    public static void ThrowIfTokenNotEquals(this ref Utf8JsonReader reader, JsonTokenType token)
    {
        if (reader.TokenType != token)
        {
            throw new InvalidDataException($"Expected a '{token}' token, but got '{reader.TokenType}'.");
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zydis.SourceGeneration.Helpers;

#pragma warning disable CA1812 // The factory is constructed in the projects that link this file, invisible to CA1812 in this compilation; the converter is created by the factory via reflection.

internal sealed class ImmutableEquatableArrayConverterFactory :
    JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               (typeToConvert.GetGenericTypeDefinition() == typeof(ImmutableEquatableArray<>));
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var args = typeToConvert.GetGenericArguments();

        return (JsonConverter)Activator.CreateInstance(
            typeof(ImmutableEquatableArrayConverter<>).MakeGenericType(args[0]),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: null,
            culture: null
        )!;
    }
}

internal sealed class ImmutableEquatableArrayConverter<T> :
    JsonConverter<ImmutableEquatableArray<T>>
    where T : IEquatable<T>
{
    public override ImmutableEquatableArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var values = JsonSerializer.Deserialize<T[]>(ref reader, options);

        return ImmutableEquatableArray.Create(values ?? []);
    }

    public override void Write(Utf8JsonWriter writer, ImmutableEquatableArray<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}

#pragma warning restore CA1812

using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zydis.Generator.SourceGenerator.Helpers;

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

#pragma warning disable CA1812 // Avoid uninstantiated internal classes.

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

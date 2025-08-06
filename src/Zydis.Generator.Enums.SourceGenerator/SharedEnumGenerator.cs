using System;
using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Zydis.Generator.SourceGenerator.CodeGeneration;
using Zydis.Generator.SourceGenerator.Helpers;

namespace Zydis.Generator.Enums.SourceGenerator;

[Generator]
internal sealed class SharedEnumGenerator :
    IIncrementalGenerator
{
    private const string FileSharedEnums = "shared_enums.json";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //if (!Debugger.IsAttached)
        //{
        //    Debugger.Launch();
        //}

        var options = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new ImmutableEquatableArrayConverterFactory() }
        };

        var sharedEnums = context.AdditionalTextsProvider
            .Where(static file => Path.GetFullPath(file.Path).EndsWith(FileSharedEnums, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => file.GetText(cancellationToken)?.ToString())
            .Where(static content => content is not null)
            .SelectMany((content, _) => JsonSerializer.Deserialize<SharedEnumDefinition[]>(content!, options)!);

        context.RegisterSourceOutput(sharedEnums, GenerateSharedEnum);
    }

    private static void GenerateSharedEnum(SourceProductionContext context, SharedEnumDefinition definition)
    {
        var writer = new SourceWriter();

        var enumTypeName = GeneratorUtils.GetGeneratorIdentifier(definition.Name);

        GeneratorUtils.StartFormatSourceFile(writer, GeneratorConstants.NamespaceEnums, null);

        if (!string.IsNullOrEmpty(definition.Description))
        {
            writer.WriteLine(
                $"""
                /// <summary>
                /// {definition.Description}
                /// </summary>
                """
            );
        }

        if (definition.IsFlagsEnum)
        {
            writer.WriteLine("[System.Flags]");
        }

        if (definition.IsSerializable)
        {
            writer.WriteLine($"[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<{enumTypeName}>))]");
        }

        writer.WriteLine(
            $$"""
            public enum {{enumTypeName}}
            {
            """);
        writer.Indentation++;

        for (var i = 0; i < definition.Members.Length; ++i)
        {
            var member = definition.Members[i];

            if (!string.IsNullOrEmpty(member.Description))
            {
                writer.WriteLine(
                    $"""
                    /// <summary>
                    /// {member.Description}
                    /// </summary>
                    """
                );
            }

            if (definition.IsSerializable)
            {
                writer.WriteLine($"[System.Text.Json.Serialization.JsonStringEnumMemberName(\"{GeneratorUtils.GetNativeIdentifier(member.Name)}\")]");
            }

            if (!definition.IsFlagsEnum)
            {
                writer.WriteLine($"{GeneratorUtils.GetGeneratorIdentifier(member.Name)},");
                continue;
            }

            var value = (i == 0) ? "0" : $"1 << {i - 1}";

            writer.WriteLine($"{GeneratorUtils.GetGeneratorIdentifier(member.Name)} = {value},");
        }

        writer.Indentation--;
        writer.WriteLine("}");

        writer.WriteLine();

        writer.WriteLine(
            $$"""
            public static partial class {{enumTypeName}}Extensions
            {
            """);
        writer.Indentation++;

        writer.WriteLine(
            $$"""
            public static string ToZydisString(this {{enumTypeName}} value, bool includePrefix = true)
            {
                return (includePrefix ? "ZYDIS_{{GeneratorUtils.GetZydisIdentifier(definition.Name, true)}}_" : string.Empty) + value switch
                {
            """);
        writer.Indentation += 2;

        foreach (var member in definition.Members)
        {
            writer.WriteLine(
                $"{enumTypeName}.{GeneratorUtils.GetGeneratorIdentifier(member.Name)} => " +
                $"\"{GeneratorUtils.GetZydisIdentifier(member.Name, true)}\",");
        }

        writer.WriteLine("_ => throw new System.ArgumentOutOfRangeException(nameof(value), value, null)");

        writer.Indentation -= 2;
        writer.WriteLine(
            """
                };
            }
            """);

        writer.Indentation--;
        writer.WriteLine("}");

        GeneratorUtils.EndFormatSourceFile(writer);

        context.AddSource(
            $"SharedEnum.{GeneratorUtils.GetNativeIdentifier(definition.Name).ToPascalCase()}.g.cs",
            SourceText.From(writer.ToString(), Encoding.UTF8)
        );
    }
}

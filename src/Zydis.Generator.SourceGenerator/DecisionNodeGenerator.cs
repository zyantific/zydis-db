using System;
using System.IO;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Zydis.Generator.SourceGenerator.CodeGeneration;
using Zydis.Generator.SourceGenerator.Helpers;

namespace Zydis.Generator.SourceGenerator;

internal sealed class DecisionNodeGenerator
{
    private SourceProductionContext Context { get; }
    private DecisionNodeDefinition Definition { get; }
    private string TypeName { get; }
    public bool HasNamedSlots { get; }

    private DecisionNodeGenerator(SourceProductionContext context, DecisionNodeDefinition definition)
    {
        Context = context;
        Definition = definition;
        TypeName = GeneratorUtils.GetGeneratorIdentifier(definition.Name) + "Node";
        HasNamedSlots = definition.NamedSlots?.Length > 0;
    }

    public static void AddSource(SourceProductionContext context, DecisionNodeDefinition definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        var generator = new DecisionNodeGenerator(context, definition);
        var source = generator.GenerateSource();

        context.AddSource($"DecisionNode.{GeneratorUtils.GetNativeIdentifier(definition.Name).ToPascalCase()}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private string GenerateSource()
    {
        var writer = new SourceWriter();

        GeneratorUtils.StartFormatSourceFile(writer, GeneratorConstants.NamespaceNodes, null);
        GenerateNodeClass(writer);
        GeneratorUtils.EndFormatSourceFile(writer);

        return writer.ToString();
    }

    private void GenerateNodeClass(SourceWriter writer)
    {
        if (!string.IsNullOrEmpty(Definition.Description))
        {
            writer.WriteLine(
                $"""
                 /// <summary>
                 /// {Definition.Description}
                 /// </summary>
                 """);
        }

        writer.WriteLine(
            $$"""
              public sealed partial class {{TypeName}} :
                  DecisionNode{{(HasNamedSlots ? $"<{TypeName}.{GeneratorConstants.SlotNamesEnumTypeName}>" : string.Empty)}}
              {
              """
        );
        writer.Indentation++;

        GenerateConstructor(writer);
        GenerateDefinitionClass(writer);

        if (HasNamedSlots)
        {
            GenerateSlotsEnum(writer);
        }

        writer.Indentation--;
        writer.WriteLine("}");
    }

    private void GenerateConstructor(SourceWriter writer)
    {
        writer.WriteLine(
            $$"""
              public {{TypeName}}() :
                  base({{GeneratorConstants.DefinitionTypeName}}.Instance)
              {
              }
              """
        );
    }

    private void GenerateDefinitionClass(SourceWriter writer)
    {
        var numberOfSlots = Definition.NumberOfSlots ?? Definition.NamedSlots?.Length ?? throw new InvalidDataException(Definition.ToString()); // TODO: Diagnostic.

        writer.WriteLine(
            $$"""

              public sealed partial class {{GeneratorConstants.DefinitionTypeName}} :
                  DecisionNodeDefinition{{(HasNamedSlots ? $"<{GeneratorConstants.SlotNamesEnumTypeName}>" : string.Empty)}}
              {
              """
        );
        writer.Indentation++;

        // TODO: Handle flags for IsSynthetic.

        writer.WriteLine(
            $$"""
              public static {{GeneratorConstants.DefinitionTypeName}} Instance { get; } = new();

              /// <inheritdoc/>
              public override string Name => "{{GeneratorUtils.GetNativeIdentifier(Definition.Name)}}";

              /// <inheritdoc/>
              public override int EncodedSize => 1 + NumberOfSlots;

              /// <inheritdoc/>
              public override int NumberOfSlots => {{numberOfSlots}};

              /// <inheritdoc/>
              public override bool IsSynthetic => false;

              private {{GeneratorConstants.DefinitionTypeName}}()
              {
                  // This constructor is private to prevent instantiation.
              }
              """
        );

        GenerateCreateMethod(writer);

        if (HasNamedSlots)
        {
            GenerateSlotNameToIndexMethod(writer);
            GenerateIndexToSlotNameMethod(writer);
            GenerateEnumIndexToIndexMethod(writer);
        }

        writer.Indentation--;
        writer.WriteLine("}");
    }

    private void GenerateCreateMethod(SourceWriter writer)
    {
        writer.WriteLine(
            """

            /// <inheritdoc/>
            [System.Diagnostics.Contracts.Pure]
            internal override DecisionNode Create(params string[]? arguments)
            {
            """
        );
        writer.Indentation++;
        writer.WriteLine($"return new {TypeName}();");
        writer.Indentation--;
        writer.WriteLine("}");
    }

    private void GenerateSlotNameToIndexMethod(SourceWriter writer)
    {
        writer.WriteLine(
            """

            /// <inheritdoc/>
            [System.Diagnostics.Contracts.Pure]
            protected override int SlotNameToIndex(string value)
            {
            """
        );
        writer.Indentation++;

        writer.WriteLine(
            """
            return value switch
            {
            """
        );
        writer.Indentation++;

        var i = 0;
        foreach (var slotName in Definition.NamedSlots!)
        {
            writer.WriteLine($"\"{GeneratorUtils.GetNativeIdentifier(slotName)}\" => {i++},");
        }

        writer.WriteLine("_ => -1");

        writer.Indentation--;
        writer.WriteLine("};");

        writer.Indentation--;
        writer.WriteLine("}");
    }

    private void GenerateIndexToSlotNameMethod(SourceWriter writer)
    {
        writer.WriteLine(
            """

            /// <inheritdoc/>
            [System.Diagnostics.Contracts.Pure]
            protected override string? IndexToSlotName(int index)
            {
            """
        );
        writer.Indentation++;

        writer.WriteLine(
            """
            return index switch
            {
            """
        );
        writer.Indentation++;

        var i = 0;
        foreach (var slotName in Definition.NamedSlots!)
        {
            writer.WriteLine($"{i++} => \"{GeneratorUtils.GetNativeIdentifier(slotName)}\",");
        }

        writer.WriteLine("_ => null");

        writer.Indentation--;
        writer.WriteLine("};");

        writer.Indentation--;
        writer.WriteLine("}");
    }

    private void GenerateEnumIndexToIndexMethod(SourceWriter writer)
    {
        writer.WriteLine(
            $$"""

            /// <inheritdoc/>
            [System.Diagnostics.Contracts.Pure]
            protected override int EnumIndexToIndex({{GeneratorConstants.SlotNamesEnumTypeName}} index)
            {
            """
        );
        writer.Indentation++;

        writer.WriteLine(
            """
            return index switch
            {
            """
        );
        writer.Indentation++;

        var i = 0;
        foreach (var slotName in Definition.NamedSlots!)
        {
            writer.WriteLine($"{GeneratorConstants.SlotNamesEnumTypeName}.{GeneratorUtils.GetGeneratorIdentifier(slotName)} => {i++},");
        }

        writer.WriteLine("_ => -1");

        writer.Indentation--;
        writer.WriteLine("};");

        writer.Indentation--;
        writer.WriteLine("}");
    }

    private void GenerateSlotsEnum(SourceWriter writer)
    {
        writer.WriteLine(
            $$"""

              public enum {{GeneratorConstants.SlotNamesEnumTypeName}}
              {
              """
        );
        writer.Indentation++;

        foreach (var slotName in Definition.NamedSlots!)
        {
            writer.WriteLine($"{GeneratorUtils.GetGeneratorIdentifier(slotName)},");
        }

        writer.Indentation--;
        writer.WriteLine("}");
    }
}

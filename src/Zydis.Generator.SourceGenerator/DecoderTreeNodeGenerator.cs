using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Zydis.Generator.SourceGenerator.CodeGeneration;
using Zydis.Generator.SourceGenerator.Helpers;

namespace Zydis.Generator.SourceGenerator;

[Generator]
internal sealed class DecoderTreeNodeGenerator :
    IIncrementalGenerator
{
    private const string FileDecoderTreeNodes = "decoder_tree_nodes.json";

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

        var decoderTreeNodes = context.AdditionalTextsProvider
            .Where(static file => Path.GetFullPath(file.Path).EndsWith(FileDecoderTreeNodes, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => file.GetText(cancellationToken)?.ToString())
            .Where(static content => content is not null)
            .SelectMany((content, _) => JsonSerializer.Deserialize<DecoderTreeNodeDefinition[]>(content!, options)!);

        context.RegisterSourceOutput(decoderTreeNodes, GenerateTreeNode);

        var decisionNodes = decoderTreeNodes.Where(x => x is DecisionNodeDefinition).Select((x, _) => (DecisionNodeDefinition)x).Collect();

        context.RegisterSourceOutput(decisionNodes, GenerateTreeNodeLookup);
    }

    private static void GenerateTreeNode(SourceProductionContext context, DecoderTreeNodeDefinition definition)
    {
        switch (definition)
        {
            case DecisionNodeDefinition dn:
                DecisionNodeGenerator.AddSource(context, dn);
                return;

            case TerminalNodeDefinition tn:
                GenerateTerminalNode(context, tn);
                break;

            default:
                // TODO: Diagnostic.
                throw new InvalidOperationException($"Unknown decoder tree node definition type: {definition.GetType().Name}");
        }
    }

    // TODO: Refactor.

    private void GenerateTreeNodeLookup(SourceProductionContext context, ImmutableArray<DecisionNodeDefinition> definitions)
    {
        var writer = new SourceWriter();
        GeneratorUtils.StartFormatSourceFile(writer, GeneratorConstants.NamespaceNodes, ["internal static partial class DecisionNodes"]);

        writer.WriteLine(
            """
            private static readonly System.Collections.Generic.IReadOnlyDictionary<string, DecisionNodeDefinition> ByName = new System.Collections.Generic.Dictionary<string, DecisionNodeDefinition>
            {
            """
        );
        writer.Indentation++;

        foreach (var definition in definitions)
        {
            var typeName = GeneratorUtils.GetGeneratorIdentifier(definition.Name);

            writer.WriteLine($"[{typeName}Node.{GeneratorConstants.DefinitionTypeName}.Instance.Name] = {typeName}Node.{GeneratorConstants.DefinitionTypeName}.Instance,");
        }

        writer.Indentation--;
        writer.WriteLine("};");

        GeneratorUtils.EndFormatSourceFile(writer);

        context.AddSource("DecisionNodeLookup.g.cs", SourceText.From(writer.ToString(), Encoding.UTF8));
    }

    private static void GenerateTerminalNode(SourceProductionContext context, TerminalNodeDefinition definition)
    {
        var typeName = GeneratorUtils.GetGeneratorIdentifier(definition.Name) + "Node";

        context.AddSource($"TerminalNode.{GeneratorUtils.GetNativeIdentifier(definition.Name).ToPascalCase()}.g.cs",
            SourceText.From(
                $$"""
                  namespace Zydis.Generator.Core.DecoderTree;

                  public sealed partial class {{typeName}} :
                      TerminalNode
                  {
                      public sealed partial class {{GeneratorConstants.DefinitionTypeName}} :
                          TerminalNodeDefinition
                      {
                          public static {{GeneratorConstants.DefinitionTypeName}} Instance { get; } = new();

                          public override string Name => "{{definition.Name}}";
                      }
                  }
                  """,
                Encoding.UTF8
            )
        );
    }
}

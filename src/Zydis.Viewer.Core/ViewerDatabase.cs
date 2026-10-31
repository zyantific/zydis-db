using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Zydis.Generator.Core.Definitions;
using Zydis.Generator.Core.Serialization;
using Zydis.Viewer.Core.Model;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core;

/// <summary>
/// A loaded instruction database: its forms, the raw JSON of each form, the view tree and the form ids.
/// </summary>
public sealed class ViewerDatabase :
    IDisposable
{
    private ViewerDatabase(IReadOnlyList<InstructionDefinition> definitions, JsonDocument document,
        IReadOnlyList<ViewNode> roots, IReadOnlyList<string> formIds, IReadOnlyList<string> paths,
        string? formIdError, string schema)
    {
        Definitions = definitions;
        Document = document;
        Roots = roots;
        FormIds = formIds;
        Paths = paths;
        FormIdError = formIdError;
        Schema = schema;
    }

    /// <summary>
    /// The forms in file order.
    /// </summary>
    public IReadOnlyList<InstructionDefinition> Definitions { get; }

    /// <summary>
    /// The parsed file. The root array holds the raw forms in the order of <see cref="Definitions"/>.
    /// </summary>
    public JsonDocument Document { get; }

    /// <summary>
    /// The root rows of the view tree.
    /// </summary>
    public IReadOnlyList<ViewNode> Roots { get; }

    /// <summary>
    /// The form id of each form, by position in <see cref="Definitions"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="FormIdError"/> is set, each id is the mnemonic and the position of the form, for example
    /// <c>add#12</c>.
    /// </remarks>
    public IReadOnlyList<string> FormIds { get; }

    /// <summary>
    /// The path of the row that holds each form in the view tree, by position in <see cref="Definitions"/>.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>
    /// The message of the error that kept <see cref="FormId.AssignIds"/> from assigning ids, or
    /// <see langword="null"/> when it succeeded.
    /// </summary>
    public string? FormIdError { get; }

    /// <summary>
    /// The form of the <c>filters</c> member in the file, taken from the first form that has one: <c>array</c>,
    /// <c>object</c>, or <c>none</c> when no form has one or its value is neither.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Loads the database in <paramref name="json"/>, builds its view tree and assigns its form ids.
    /// </summary>
    /// <param name="json">The content of an <c>instructions.json</c> file.</param>
    /// <returns>The loaded database. The caller disposes it.</returns>
    /// <exception cref="JsonException">Thrown if <paramref name="json"/> is not a JSON array of forms.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown if a form uses a filter or a filter value the generator does not know.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown in the cases <see cref="ViewTreeBuilder.Insert"/> names.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if a form belongs to no opcode table of the generator.
    /// </exception>
    public static ViewerDatabase Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var utf8 = Encoding.UTF8.GetBytes(json);
        var definitions = ReadDefinitions(utf8);
        var document = JsonDocument.Parse(utf8);

        try
        {
            var builder = new ViewTreeBuilder();
            for (var i = 0; i < definitions.Count; i++)
            {
                builder.Insert(definitions[i], i);
            }

            var roots = builder.Build();

            var paths = new string?[definitions.Count];
            foreach (var root in roots)
            {
                Describe(root, paths);
            }

            var (formIds, formIdError) = AssignFormIds(definitions);

            return new ViewerDatabase(
                definitions,
                document,
                roots,
                formIds,
                [.. paths.Select((x, i) => x ?? throw new UnreachableException($"Form {i} has no row in the tree."))],
                formIdError,
                DetectSchema(document.RootElement));
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns the <see cref="ViewModel"/> of this database as JSON.
    /// </summary>
    public string ToViewModelJson()
    {
        return JsonSerializer.Serialize(ViewModelBuilder.Build(this), ViewerSerializerContext.Default.ViewModel);
    }

    /// <summary>
    /// Releases the parsed file.
    /// </summary>
    public void Dispose()
    {
        Document.Dispose();
    }

    private static List<InstructionDefinition> ReadDefinitions(byte[] utf8)
    {
        using var stream = new MemoryStream(utf8, false);
        var reading = ReadAllAsync(stream);

        // A memory stream completes each read at once, so the reader has finished here. Waiting on an unfinished task
        // throws in the single-threaded browser runtime.
        Debug.Assert(reading.IsCompleted);

        return reading.GetAwaiter().GetResult();
    }

    private static async Task<List<InstructionDefinition>> ReadAllAsync(Stream stream)
    {
        var definitions = new List<InstructionDefinition>();

        var items = DefinitionReader.ReadAsync<InstructionDefinition>(stream).ConfigureAwait(false);

        await foreach (var definition in items)
        {
            definitions.Add(definition);
        }

        return definitions;
    }

    /// <summary>
    /// Sets the detail text of the table and definition rows in the subtree of <paramref name="node"/> and records the
    /// path of each definition row in <paramref name="paths"/>.
    /// </summary>
    private static void Describe(ViewNode node, string?[] paths)
    {
        switch (node)
        {
            case { Table: { } table }:
                node.Detail = ViewModelBuilder.TableDetail(table);
                break;

            case { Definition: { } definition }:
                node.Detail = ViewModelBuilder.OperandSummary(definition);
                paths[node.DefinitionIndex] = node.Path;
                break;
        }

        foreach (var child in node.Children)
        {
            Describe(child, paths);
        }
    }

    private static (IReadOnlyList<string> FormIds, string? Error) AssignFormIds(
        IReadOnlyList<InstructionDefinition> definitions)
    {
        try
        {
            var ids = FormId.AssignIds(definitions);

            return ([.. definitions.Select(x => ids[x])], null);
        }
        // Thrown for two forms the id axes cannot tell apart and for a filter the axes do not know. The viewer still
        // shows such a database, with positional ids.
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            var ids = definitions
                .Select((x, i) => string.Create(CultureInfo.InvariantCulture, $"{x.Mnemonic}#{i}"))
                .ToList();

            return (ids, exception.Message);
        }
    }

    private static string DetectSchema(JsonElement root)
    {
        foreach (var form in root.EnumerateArray())
        {
            if (form.TryGetProperty("filters", out var filters))
            {
                return filters.ValueKind switch
                {
                    JsonValueKind.Array => "array",
                    JsonValueKind.Object => "object",
                    _ => "none"
                };
            }
        }

        return "none";
    }
}

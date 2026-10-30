using System.Collections.Generic;
using System.Linq;

using Xunit;

using Zydis.Generator.Core.Definitions;
using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Tests;

/// <summary>
/// Builds minimal forms and view trees for the tree builder tests.
/// </summary>
internal static class TestDefinitions
{
    /// <summary>
    /// Builds a form of the primary table whose recorded filter entries are <paramref name="filters"/>.
    /// </summary>
    public static InstructionDefinition Make(
        string mnemonic, byte opcode, params (string Filter, string Value)[] filters)
    {
        return new InstructionDefinition
        {
            Mnemonic = mnemonic,
            Opcode = opcode,
            MetaInfo = new InstructionMetaInfo
            {
                Category = "BINARY",
                IsaExtension = "BASE",
                IsaSet = "I86"
            },
            Pattern = [.. filters.Select(x => new FilterEntry(x.Filter, x.Value))]
        };
    }

    /// <summary>
    /// Inserts <paramref name="definitions"/> in order, each with its position as the definition index, and returns
    /// the primary table node.
    /// </summary>
    public static ViewNode Build(params InstructionDefinition[] definitions)
    {
        return BuildRoots(definitions)[0];
    }

    /// <summary>
    /// Inserts <paramref name="definitions"/> like <see cref="Build"/> and returns the root list.
    /// </summary>
    public static IReadOnlyList<ViewNode> BuildRoots(params InstructionDefinition[] definitions)
    {
        var builder = new ViewTreeBuilder();

        for (var i = 0; i < definitions.Length; i++)
        {
            builder.Insert(definitions[i], i);
        }

        return builder.Build();
    }

    /// <summary>
    /// Follows the '/' separated child labels of <paramref name="path"/> down from <paramref name="root"/>.
    /// </summary>
    public static ViewNode Find(ViewNode root, string path)
    {
        var node = root;

        foreach (var label in path.Split('/'))
        {
            var matches = node.Children.Where(x => x.Label == label).ToList();

            Assert.True(matches.Count == 1,
                $"'{node.Path}' has {matches.Count} children labelled '{label}', " +
                $"children: {string.Join(", ", node.Children.Select(x => x.Label))}.");

            node = matches[0];
        }

        return node;
    }

    /// <summary>
    /// Returns the definition row that is the single child of the row at <paramref name="path"/>.
    /// </summary>
    public static ViewNode DefinitionAt(ViewNode root, string path)
    {
        var row = Assert.Single(Find(root, path).Children);
        Assert.Equal(ViewNodeKind.Definition, row.Kind);

        return row;
    }
}

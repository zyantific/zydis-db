using System.Collections.Generic;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Viewer.Core.Tree;

/// <summary>
/// The kind of a row in the view tree.
/// </summary>
public enum ViewNodeKind
{
    /// <summary>
    /// An opcode table. Its children are the opcode rows.
    /// </summary>
    Table,

    /// <summary>
    /// One opcode of a table. Its child is the row the opcode slot holds.
    /// </summary>
    Opcode,

    /// <summary>
    /// An opcode table switch. Its children are the named slot rows.
    /// </summary>
    Switch,

    /// <summary>
    /// A filter test. Its children are the slot rows.
    /// </summary>
    Decision,

    /// <summary>
    /// One slot of a decision node. Its child is the row placed in the slot.
    /// </summary>
    Slot,

    /// <summary>
    /// One form of the database.
    /// </summary>
    Definition,

    /// <summary>
    /// Two or more forms that select the same region. Its children are the definition rows of those forms.
    /// </summary>
    Conflict,

    /// <summary>
    /// A row that leads to an opcode table another row owns.
    /// </summary>
    Reference
}

/// <summary>
/// A row of the view tree.
/// </summary>
public sealed class ViewNode
{
    private const string DefinitionLabel = "Definition";

    private readonly List<ViewNode> _children = [];

    private ViewNode(ViewNodeKind kind, string label, ViewNode? parent, string? filter,
        InstructionDefinition? definition, int definitionIndex)
    {
        Kind = kind;
        Label = label;
        Parent = parent;
        Filter = filter;
        Definition = definition;
        DefinitionIndex = definitionIndex;

        Path = parent switch
        {
            null => label,
            _ when kind is ViewNodeKind.Definition => parent.Path,
            _ => $"{parent.Path}/{label}"
        };
    }

    /// <summary>
    /// The kind of this row.
    /// </summary>
    public ViewNodeKind Kind { get; }

    /// <summary>
    /// The text of the row in the item column.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Secondary text shown next to the label, or <see langword="null"/> when the row has none.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// The filter name of a decision row, or <see langword="null"/> for any other row.
    /// </summary>
    public string? Filter { get; }

    /// <summary>
    /// The row this row sits under, or <see langword="null"/> for a root.
    /// </summary>
    public ViewNode? Parent { get; }

    /// <summary>
    /// The child rows in display order.
    /// </summary>
    public IReadOnlyList<ViewNode> Children => _children;

    /// <summary>
    /// The form of a definition row, or <see langword="null"/> for any other row.
    /// </summary>
    public InstructionDefinition? Definition { get; }

    /// <summary>
    /// The position of the form in the database for a definition row, or <c>-1</c> for any other row.
    /// </summary>
    public int DefinitionIndex { get; }

    /// <summary>
    /// Signals whether this row is a conflict row or has one below it.
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// The number of forms in the subtree of this row.
    /// </summary>
    public int FormCount { get; set; }

    /// <summary>
    /// The labels from the root down to this row, joined by <c>/</c>. A definition row has the path of the row that
    /// holds it, so the path of a form is its position in the tree.
    /// </summary>
    public string Path { get; }

    internal static ViewNode CreateRoot(ViewNodeKind kind, string label)
    {
        return new ViewNode(kind, label, null, null, null, -1);
    }

    internal ViewNode AddChild(ViewNodeKind kind, string label, string? filter = null)
    {
        var child = new ViewNode(kind, label, this, filter, null, -1);
        _children.Add(child);

        return child;
    }

    internal ViewNode AddDefinition(InstructionDefinition definition, int definitionIndex)
    {
        var child = new ViewNode(ViewNodeKind.Definition, DefinitionLabel, this, null, definition, definitionIndex);
        _children.Add(child);

        return child;
    }
}

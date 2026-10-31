using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Model;

/// <summary>
/// The document the web app renders for one loaded database.
/// </summary>
/// <param name="Schema">
/// The form of the <c>filters</c> member in the file, taken from the first form that has one: <c>array</c>,
/// <c>object</c>, or <c>none</c> when no form has one or its value is neither.
/// </param>
/// <param name="Stats">The counts shown in the status bar.</param>
/// <param name="FormIdError">
/// The reason the form ids are positional fallbacks, or <see langword="null"/> when every form has its own id.
/// </param>
/// <param name="RootIds">The ids of the root rows in display order.</param>
/// <param name="Nodes">The rows of the view tree, indexed by id.</param>
/// <param name="Definitions">The forms of the database in file order.</param>
/// <param name="FilterSlots">
/// The complete slot name list of each filter that occurs as a decision row, by filter name.
/// </param>
public sealed record ViewModel(
    string Schema,
    ViewStats Stats,
    string? FormIdError,
    IReadOnlyList<int> RootIds,
    IReadOnlyList<ViewNodeDto> Nodes,
    IReadOnlyList<ViewDefinitionDto> Definitions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FilterSlots);

/// <summary>
/// The counts of a <see cref="ViewModel"/>.
/// </summary>
/// <param name="Definitions">The number of forms.</param>
/// <param name="Nodes">The number of rows in the view tree.</param>
/// <param name="Conflicts">The number of conflict rows.</param>
public sealed record ViewStats(int Definitions, int Nodes, int Conflicts);

/// <summary>
/// A row of the view tree.
/// </summary>
/// <param name="Id">The position of the row in a depth-first preorder walk over the root rows.</param>
/// <param name="ParentId">The id of the row this row sits under, or <see langword="null"/> for a root.</param>
/// <param name="Kind">The kind of the row.</param>
/// <param name="Label">The text of the row in the item column.</param>
/// <param name="Detail">The secondary text of the row, or <see langword="null"/> when the row has none.</param>
/// <param name="Filter">The filter name of a decision row, or <see langword="null"/> for any other row.</param>
/// <param name="ChildIds">The ids of the child rows in display order.</param>
/// <param name="DefinitionIndex">
/// The position of the form in <see cref="ViewModel.Definitions"/> for a definition row, or
/// <see langword="null"/> for any other row.
/// </param>
/// <param name="Conflict">Signals whether this row is a conflict row or has one below it.</param>
/// <param name="FormCount">The number of forms in the subtree of this row.</param>
public sealed record ViewNodeDto(
    int Id,
    // Written for a root as well, so the web app reads null rather than a missing member when it walks up the tree.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ParentId,
    ViewNodeKind Kind,
    string Label,
    string? Detail,
    string? Filter,
    IReadOnlyList<int> ChildIds,
    int? DefinitionIndex,
    bool Conflict,
    int FormCount);

/// <summary>
/// A form of the database with the values the grid and the inspector show.
/// </summary>
/// <param name="Index">The position of the form in the file.</param>
/// <param name="FormId">The form id, or a positional fallback when <see cref="ViewModel.FormIdError"/> is set.</param>
/// <param name="Mnemonic">The mnemonic of the form.</param>
/// <param name="OperandSummary">
/// The JSON names of the visible operands, joined by <c>", "</c>. An implicit register operand shows its register.
/// </param>
/// <param name="CommentText">
/// The <c>comment</c> of the form decoded from base64, or <see langword="null"/> when it is absent or not base64.
/// </param>
/// <param name="VisibleOperandCount">The number of operands the decoder reports as visible.</param>
/// <param name="Path">The path of the row that holds the form in the view tree.</param>
/// <param name="Raw">The form as it appears in the file.</param>
public sealed record ViewDefinitionDto(
    int Index,
    string FormId,
    string Mnemonic,
    string OperandSummary,
    string? CommentText,
    int VisibleOperandCount,
    string Path,
    JsonElement Raw);

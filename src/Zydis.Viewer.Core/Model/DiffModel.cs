using System.Collections.Generic;
using System.Text.Json.Serialization;

using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Model;

/// <summary>
/// The status of a form, or of a row of the merged tree, in a <see cref="DiffModel"/>.
/// </summary>
public enum DiffStatus
{
    /// <summary>
    /// Both sides have the form at the same path, with equal raw JSON.
    /// </summary>
    Unchanged,

    /// <summary>
    /// Only the head has the form.
    /// </summary>
    Added,

    /// <summary>
    /// Only the base has the form.
    /// </summary>
    Removed,

    /// <summary>
    /// Both sides have the form at the same path, and its raw JSON differs.
    /// </summary>
    Changed,

    /// <summary>
    /// The two sides have the form at different paths. Its raw JSON can differ as well.
    /// </summary>
    Moved
}

/// <summary>
/// The document the web app renders for the comparison of two loaded databases.
/// </summary>
/// <remarks>
/// The rows refer to the forms by their position in the <see cref="ViewModel.Definitions"/> of each side, which the
/// web app already holds, so the document does not repeat them.
/// </remarks>
/// <param name="Stats">The number of pairs of each status.</param>
/// <param name="FormIdError">
/// The reason the form ids of at least one side are positional fallbacks, or <see langword="null"/> when both sides
/// have their own ids. When both sides have a reason, it is the one of the head. When it is set, the pairing skips the
/// form ids.
/// </param>
/// <param name="RootIds">The ids of the root rows of the merged tree in display order.</param>
/// <param name="Nodes">The rows of the merged tree, indexed by id.</param>
/// <param name="Pairs">
/// The pairs in head file order, followed by the removed forms in base file order. The pair at position <c>i</c>
/// below the number of head forms is the pair of head form <c>i</c>.
/// </param>
public sealed record DiffModel(
    DiffStats Stats,
    string? FormIdError,
    IReadOnlyList<int> RootIds,
    IReadOnlyList<DiffNodeDto> Nodes,
    IReadOnlyList<DiffPairDto> Pairs);

/// <summary>
/// The number of pairs of each status in a <see cref="DiffModel"/>. A pair that moved and changed counts as moved.
/// </summary>
/// <param name="Added">The number of forms only the head has.</param>
/// <param name="Removed">The number of forms only the base has.</param>
/// <param name="Changed">The number of pairs whose raw JSON differs at the same path.</param>
/// <param name="Moved">The number of pairs whose path differs.</param>
/// <param name="Unchanged">The number of pairs with equal raw JSON at the same path.</param>
public sealed record DiffStats(int Added, int Removed, int Changed, int Moved, int Unchanged);

/// <summary>
/// The number of definition rows of each status in the subtree of a merged tree row.
/// </summary>
/// <remarks>
/// The row a moved form leaves at its base path counts in none of the four, so each form counts once and the counts
/// of a root row add up to the <see cref="DiffStats"/> of the forms below it.
/// </remarks>
/// <param name="Added">The number of rows of added forms.</param>
/// <param name="Removed">The number of rows of removed forms.</param>
/// <param name="Changed">The number of rows of changed forms.</param>
/// <param name="Moved">The number of rows of moved forms at their head path.</param>
public sealed record DiffCounts(int Added, int Removed, int Changed, int Moved);

/// <summary>
/// A row of the merged tree: a <see cref="ViewNodeDto"/> with the status of the row and the forms it stands for.
/// </summary>
/// <param name="Id">The position of the row in a depth-first preorder walk over the root rows.</param>
/// <param name="ParentId">The id of the row this row sits under, or <see langword="null"/> for a root.</param>
/// <param name="Kind">The kind of the row.</param>
/// <param name="Label">The text of the row in the item column.</param>
/// <param name="Detail">The secondary text of the row, or <see langword="null"/> when the row has none.</param>
/// <param name="Filter">The filter name of a decision row, or <see langword="null"/> for any other row.</param>
/// <param name="ChildIds">The ids of the child rows in display order.</param>
/// <param name="DefinitionIndex">
/// The position of the form in the definitions of the side the row stands for, the head form for a row at a head path
/// and the base form for a row at a base path, or <see langword="null"/> for any other row.
/// </param>
/// <param name="Conflict">Signals whether this row is a conflict row or has one below it.</param>
/// <param name="FormCount">The number of definition rows in the subtree of this row.</param>
/// <param name="Status">
/// The status of the pair for a definition row. A definition row at a base path is <see cref="DiffStatus.Removed"/>
/// for a removed form and <see cref="DiffStatus.Moved"/> for the old row of a moved form, which differs from the row
/// at the head path by its missing <paramref name="HeadDefinitionIndex"/>. Any other row is
/// <see cref="DiffStatus.Removed"/> when only the base tree has it, <see cref="DiffStatus.Added"/> when only the head
/// tree has it, and <see cref="DiffStatus.Unchanged"/> when both trees have it.
/// </param>
/// <param name="Counts">
/// The statuses of the definition rows in the subtree of this row, the row itself included.
/// </param>
/// <param name="BaseDefinitionIndex">
/// The position of the base form of the pair in the base file for a definition row, or <see langword="null"/> for
/// any other row and for an added form.
/// </param>
/// <param name="HeadDefinitionIndex">
/// The position of the head form in the head file for a definition row at a head path, or <see langword="null"/> for
/// any other row.
/// </param>
public sealed record DiffNodeDto(
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
    int FormCount,
    DiffStatus Status,
    DiffCounts Counts,
    int? BaseDefinitionIndex,
    int? HeadDefinitionIndex);

/// <summary>
/// A base form and the head form it pairs with, or a form that only one side has.
/// </summary>
/// <param name="BaseIndex">The position of the base form, or <see langword="null"/> for an added form.</param>
/// <param name="HeadIndex">The position of the head form, or <see langword="null"/> for a removed form.</param>
/// <param name="Status">
/// The status of the pair. A pair that moved and changed is <see cref="DiffStatus.Moved"/>.
/// </param>
/// <param name="BasePath">The path of the base form, or <see langword="null"/> for an added form.</param>
/// <param name="HeadPath">The path of the head form, or <see langword="null"/> for a removed form.</param>
/// <param name="ChangedPaths">
/// The paths of the JSON values that differ between the two forms, for example <c>operands[1].action</c>. Empty when
/// the raw JSON is equal or the pair has one form.
/// </param>
public sealed record DiffPairDto(
    int? BaseIndex,
    int? HeadIndex,
    DiffStatus Status,
    string? BasePath,
    string? HeadPath,
    IReadOnlyList<string> ChangedPaths);

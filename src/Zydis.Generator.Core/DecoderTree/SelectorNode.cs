using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Zydis.Generator.Core.DecoderTree;

/// <summary>
/// Represents a decoder table tree node that contains a switch table.
/// <para>
///     Selector nodes are branch nodes that are used to select a decoder path based on a part of the encoded
///     instruction.
///     The most basic use case is to select a decoder path based on the instruction's opcode.
///     Other use cases include selecting a decoder path based on certain bits of the instruction's ModRM byte,
///     address-size override prefix, operand-size override prefix, etc.
/// </para>
/// <para>
///     Selector nodes might as well be used to dynamically select a decoder path based on Zydis decoder runtime
///     settings like e.g. the state of a feature flag.
/// </para>
/// <para>
///     Advanced selectors can use parameters to support reusability. For example, instead of having a separate filter
///     type for each runtime setting, it is instead possible to implement the filter only once and use a parameter
///     to specify the actual runtime setting to be evaluated.
/// </para>
/// </summary>
public class SelectorNode :
    NonTerminalNode
{
    private readonly DecoderTreeNode?[] _entries;
    private readonly DecoderTreeNode?[] _negatedEntries;

    // 0 = [15..8] = ARG0, [7..0] = TYPE
    // 1 = ARG1
    // 2 = ARG2
    // ...
    // A = VALUE1
    // B = VALUE2
    // ...
    public override int EncodedSize => 1 + (Arguments.Count > 1 ? Arguments.Count - 1 : 0) + _entries.Length;

    /// <summary>
    /// The underlying <see cref="SelectorDefinition"/> for the selector node.
    /// </summary>
    public SelectorDefinition Definition { get; }

    /// <summary>
    /// The list of arguments passed to the selector node.
    /// </summary>
    public IReadOnlyList<DataNode> Arguments { get; }

    /// <summary>
    /// The collection of entries in the switch-table.
    /// </summary>
    public IReadOnlyList<DecoderTreeNode?> Entries => _entries;

    /// <summary>
    /// The collection of negated entries in the switch-table.
    /// </summary>
    public IReadOnlyList<DecoderTreeNode?> NegatedEntries => _negatedEntries;

    /// <summary>
    /// Gets a value indicating whether the table contains any non-zero entries.
    /// </summary>
    public bool HasNonZeroEntries => _entries.Any(x => x is not null) || _negatedEntries.Any(x => x is not null);

#pragma warning disable CA1043

    /// <summary>
    /// Gets or sets the <see cref="DecoderTreeNode"/> associated with the specified <see cref="SelectorTableIndex"/>.
    /// </summary>
    /// <param name="index">
    /// The <see cref="SelectorTableIndex"/> used to access the corresponding <see cref="DecoderTreeNode"/>.
    /// If <see cref="SelectorTableIndex.IsNegated"/> is <see langword="true"/>, the negated entry is accessed or modified.</param>
    /// <returns>The <see cref="DecoderTreeNode"/> associated with the specified <see cref="SelectorTableIndex"/>.</returns>
    public DecoderTreeNode? this[SelectorTableIndex index]
    {
        get
        {
            if (index.IsNegated)
            {
                return _negatedEntries[index.Index];
            }

            return _entries[index.Index];
        }
        set
        {
            if (index.IsNegated)
            {
                _negatedEntries[index.Index] = value;
                return;
            }

            _entries[index.Index] = value;
        }
    }

#pragma warning restore CA1043

    /// <summary>
    /// Constructs a new <see cref="SelectorNode"/> using the given <paramref name="definition"/>.
    /// </summary>
    /// <param name="definition">The filter definition to be used to create the filter instance.</param>
    /// <param name="arguments">A list of optional arguments for the filter.</param>
    /// <exception cref="ArgumentException">
    /// If the number of <paramref name="arguments"/> does not match the expected number of parameters for the given
    /// <paramref name="definition"/>.
    /// </exception>
    public SelectorNode(SelectorDefinition definition, IEnumerable<string>? arguments)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _entries = new DecoderTreeNode?[definition.NumberOfEntries];
        _negatedEntries = new DecoderTreeNode?[definition.NumberOfEntries];

        Arguments = arguments?.Select(x => new DataNode(x)).ToArray() ?? [];

        if (Arguments.Count != definition.NumberOfParameters)
        {
            throw new ArgumentException(
                "The number of arguments passed to the selector node must be equal to the number of parameters in " +
                "the underlying switch-table definition.",
                nameof(arguments));
        }

        Definition = definition;
    }

    /// <inheritdoc/>
    [Pure]
    public override int CalcEncodedSizeRecursive()
    {
        return EncodedSize +
               _entries.Where(x => x is not null).Sum(entry => entry!.CalcEncodedSizeRecursive()) +
               _negatedEntries.Where(x => x is not null).Sum(entry => entry!.CalcEncodedSizeRecursive());
    }

    /// <summary>
    /// Determines whether the current selector is constructed from the specified definition and arguments.
    /// </summary>
    /// <param name="definition">The <see cref="SelectorDefinition"/> to compare against the current selector's definition.</param>
    /// <param name="arguments">An array of strings representing the arguments to compare against the current selector's arguments.</param>
    /// <returns>
    /// <see langword="true"/> if the current selector's definition matches the specified <paramref name="definition"/>
    /// and its arguments match the specified <paramref name="arguments"/> in order; otherwise, <see langword="false"/>.
    /// </returns>
    [Pure]
    public bool IsConstructedFrom(SelectorDefinition definition, string[] arguments)
    {
        return ReferenceEquals(Definition, definition) && Arguments.Select(x => x.Data).SequenceEqual(arguments);
    }

    /// <summary>
    /// Returns an enumeration of the effective entries in the selector node switch-table, taking into account any negated entries.
    /// </summary>
    /// <remarks>
    /// This method iterates through all entries in the selector node switch-table and determines whether to return
    /// the original entry or a negated entry based on the presence of a negated entry index. If no negated entry index
    /// is found, the original entries are returned as-is. If a negated entry index is present, the negated entry is
    /// returned for all indices except the negated entry index itself, which returns the original entry.
    /// </remarks>
    /// <returns>An enumeration of <see cref="DecoderTreeNode"/> objects representing the effective switch-table entries.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if multiple negated entries are found in the table or of if a non-null entry is found in a slot that is already covered
    /// by a negated entry.
    /// </exception>
    [Pure]
    public IEnumerable<DecoderTreeNode?> GetEffectiveEntries()
    {
        var negatedEntryIndex = FindNegatedEntryIndex();

        for (var i = 0; i < Definition.NumberOfEntries; i++)
        {
            if (negatedEntryIndex is null)
            {
                // If there is no negated entry, we can return the entry as is.
                yield return _entries[i];
                continue;
            }

            if (i == negatedEntryIndex)
            {
                // If the current index is the negated entry index, we return the normal entry.
                yield return _entries[i];
                continue;
            }

            if (_entries[i] is not null)
            {
                throw new InvalidOperationException(
                    $"The selector node switch-table contains a non-null entry in slot '{Definition.Slots[i]}' that is already " +
                    $"covered by a negated entry in slot '{Definition.Slots[negatedEntryIndex.Value]}'.");
            }

            // Return the negated entry otherwise.
            yield return _negatedEntries[negatedEntryIndex.Value];
        }
    }

    /// <summary>
    /// Finds the index of the single negated entry in the table.
    /// </summary>
    /// <returns>The index of the single negated entry if found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if multiple negated entries are found in the table.</exception>
    [Pure]
    private int? FindNegatedEntryIndex()
    {
        var negated = _negatedEntries.Index().Where(x => x.Item is not null).ToArray();

        return negated switch
        {
            { Length: 0 } => null,
            { Length: 1 } => negated[0].Index,
            _ => throw new InvalidOperationException("Multiple negated entries found in selector node.")
        };
    }

    #region Debugging

    public override string ToString()
    {
        var result = $"{Definition.Name}";
        if (Arguments.Count > 0)
        {
            result += $"[{string.Join(", ", Arguments)}]";
        }

        return result;
    }

    #endregion Debugging
}

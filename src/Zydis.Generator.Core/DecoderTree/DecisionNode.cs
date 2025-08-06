using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Zydis.Generator.Core.DecoderTree;

public abstract class DecisionNodeDefinition :
    NonTerminalNodeDefinition
{
    /// <summary>
    /// Signals whether this decision node is synthetic.
    /// </summary>
    /// <remarks>
    /// Synthetic decision nodes can not directly be used inside the pattern of an instruction definition since they
    /// require specific handling by the generator.
    /// </remarks>
    public abstract bool IsSynthetic { get; }

    /// <summary>
    /// Creates a new <see cref="DecisionNode"/> using the current definition.
    /// </summary>
    /// <param name="arguments">Additional arguments for the decision node.</param>
    /// <returns>A new <see cref="DecisionNode"/> instance.</returns>
    [Pure]
    internal abstract DecisionNode Create(params string[]? arguments);

    /// <summary>
    /// Parses a slot expression and returns the corresponding <see cref="DecisionNodeIndex"/>.
    /// </summary>
    /// <param name="slotExpression">
    /// The slot expression to parse. This must be a non-empty string representing the name of a slot. If the
    /// expression starts with an exclamation mark (<c>!</c>), the resulting index will be marked as negated.
    /// </param>
    /// <returns>
    /// A <see cref="DecisionNodeIndex"/> representing the index of the slot and its negation state.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the slot specified in <paramref name="slotExpression"/> does not exist.
    /// </exception>
    [Pure]
    internal DecisionNodeIndex ParseSlotIndex(string slotExpression)
    {
        ArgumentException.ThrowIfNullOrEmpty(slotExpression);

        var isNegated = slotExpression.StartsWith('!');
        if (isNegated)
        {
            slotExpression = slotExpression[1..];
        }

        var index = SlotNameToIndex(slotExpression);
        if ((index >= 0) && (index < NumberOfSlots))
        {
            return DecisionNodeIndex.Create(index, isNegated);
        }

        throw new ArgumentException(
            $"Unknown slot name '{slotExpression}' for decision node type '{Name}'.", nameof(slotExpression));
    }

    /// <summary>
    /// Returns the name of the slot at the specified <paramref name="index"/> as a string.
    /// </summary>
    /// <returns>The name of the slot at the specified <paramref name="index"/> as a string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the slot index is out of range.</exception>
    [Pure]
    internal string GetSlotName(int index)
    {
        var name = IndexToSlotName(index);

        if (name is null)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index must be between 0 and {NumberOfSlots - 1} for decision node type '{Name}'.");
        }

        return name;
    }

    /// <summary>
    /// Returns the index for the given slot <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the slot.</param>
    /// <returns>
    /// The index of the slot corresponding to the specified <paramref name="name"/> or <c>-1</c>, if the provided name
    /// is invalid.
    /// </returns>
    /// <remarks>To be implemented by the source generator.</remarks>
    [Pure]
    protected abstract int SlotNameToIndex(string name);

    /// <summary>
    /// Returns the slot name for the given <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The slot index.</param>
    /// <returns>
    /// The name of the slot corresponding to the specified <paramref name="index"/> or <see langword="null"/>, if the
    /// provided index is out of range.
    /// </returns>
    /// <remarks>To be implemented by the source generator.</remarks>
    [Pure]
    protected abstract string? IndexToSlotName(int index);
}

public abstract class DecisionNodeDefinition<TIndexEnum> :
    DecisionNodeDefinition
    where TIndexEnum : struct, Enum
{
    /// <summary>
    /// Returns the numeric index of the slot corresponding to the specified <paramref name="value"/> of the
    /// <typeparamref name="TIndexEnum"/> enum.
    /// </summary>
    /// <param name="value">
    /// The <typeparamref name="TIndexEnum"/> enum value to get the corresponding slot index for.
    /// </param>
    /// <returns>
    /// The numeric index of the slot corresponding to the specified <typeparamref name="TIndexEnum"/> enum value.
    /// </returns>
    [Pure]
    internal int GetSlotIndex(TIndexEnum value)
    {
        var index = EnumIndexToIndex(value);

        if ((index < 0) || (index >= NumberOfSlots))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Invalid index enum value for decision node type '{Name}'.");
        }

        return index;
    }

    /// <summary>
    /// Returns the numeric index for the given <paramref name="index"/> enum value.
    /// </summary>
    /// <param name="index">The slot index enum value.</param>
    /// <returns>
    /// The numeric index of the slot corresponding to the specified <paramref name="index"/> enum value or <c>-1</c>,
    /// if the provided index is out of range.
    /// </returns>
    /// <remarks>To be implemented by the source generator.</remarks>
    [Pure]
    protected abstract int EnumIndexToIndex(TIndexEnum index);
}

/// <summary>
/// Represents a decision node in the decoder table tree.
/// </summary>
/// <remarks>
/// Decision nodes are specialized <see cref="NonTerminalNode"/>s that are used to select a path based on a part of
/// the encoded instruction (e.g. the opcode, specific bits of the <c>ModRM</c> byte, etc.) or based on a runtime
/// setting of the Zydis decoder (e.g. a feature flag).
/// </remarks>
public class DecisionNode :
    NonTerminalNode
{
    private readonly DecoderTreeNode?[] _entries;
    private readonly DecoderTreeNode?[] _negatedEntries;

    /// <inheritdoc cref="DecoderTreeNode.Definition"/>
    public new DecisionNodeDefinition Definition => (DecisionNodeDefinition)base.Definition;

#pragma warning disable CA1043

    /// <summary>
    /// Gets or sets the <see cref="DecoderTreeNode"/> associated with the specified <see cref="DecisionNodeIndex"/>.
    /// </summary>
    /// <param name="index">
    /// The <see cref="DecisionNodeIndex"/> used to access the corresponding <see cref="DecoderTreeNode"/>.
    /// If <see cref="DecisionNodeIndex.IsNegated"/> is <see langword="true"/>, the negated entry is accessed or modified.</param>
    /// <returns>The <see cref="DecoderTreeNode"/> associated with the specified <see cref="DecisionNodeIndex"/>.</returns>
    public DecoderTreeNode? this[DecisionNodeIndex index]
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

    /// <inheritdoc/>
    protected DecisionNode(DecisionNodeDefinition definition) :
        base(definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Debug.Assert(definition.NumberOfSlots >= 2);

        _entries = new DecoderTreeNode?[definition.NumberOfSlots];
        _negatedEntries = new DecoderTreeNode?[definition.NumberOfSlots];
    }

    /// <summary>
    /// Determines whether this decision node instance is constructed from the specified <paramref name="definition"/>
    /// and <paramref name="arguments"/>.
    /// </summary>
    /// <param name="definition">The <see cref="DecisionNodeDefinition"/> to compare against this node's definition.</param>
    /// <param name="arguments">The arguments to compare against the current node's arguments.</param>
    /// <returns>
    /// <see langword="true"/> if the current node's definition matches the specified <paramref name="definition"/>
    /// and its arguments match the specified <paramref name="arguments"/> in order; otherwise, <see langword="false"/>.
    /// </returns>
    [Pure]
    public bool IsConstructedFrom(DecisionNodeDefinition definition, params string[]? arguments)
    {
        _ = arguments;
        return ReferenceEquals(Definition, definition) /* TODO: && Arguments.Select(x => x.Data).SequenceEqual(arguments) */;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This method iterates through all slots of the decision node and determines whether to return the original entry
    /// or a negated entry. If no negated entry is present, the original entries are returned as-is. If a negated entry
    /// is present, the negated entry is returned for all indices except the negated entry index itself, which returns
    /// the original entry.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if multiple negated entries are found in the table or of if a non-<see langword="null"/> entry is found
    /// in a slot that is already covered by a negated entry.
    /// </exception>
    [Pure]
    public override IEnumerable<DecoderTreeNode?> EnumerateSlots()
    {
        var negatedEntryIndex = FindNegatedEntryIndex();

        for (var i = 0; i < Definition.NumberOfSlots; i++)
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
                    $"The decision node contains a non-null entry in slot '{Definition.GetSlotName(i)}' that is " +
                    $"already covered by a negated entry in slot '{Definition.GetSlotName(negatedEntryIndex.Value)}'.");
            }

            // Return the negated entry otherwise.
            yield return _negatedEntries[negatedEntryIndex.Value];
        }
    }

    /// <summary>
    /// Enumerates all virtual slots of the decision node.
    /// </summary>
    /// <returns>An enumeration of <see cref="DecisionNodeIndex"/> and <see cref="DecoderTreeNode"/> pairs.</returns>
    /// <remarks>
    /// Since <see cref="EnumerateSlots"/> returns the effective slots of the decision node, merging the negated and
    /// regular slots, this method provides a way to explicitly enumerate all virtual slots.
    /// </remarks>
    [Pure]
    public IEnumerable<KeyValuePair<DecisionNodeIndex, DecoderTreeNode?>> EnumerateVirtualSlots()
    {
        for (var i = 0; i < Definition.NumberOfSlots; i++)
        {
            var index = DecisionNodeIndex.Create(i, false);

            yield return new KeyValuePair<DecisionNodeIndex, DecoderTreeNode?>(index, this[index]);
        }

        for (var i = 0; i < Definition.NumberOfSlots; i++)
        {
            var index = DecisionNodeIndex.Create(i, true);

            yield return new KeyValuePair<DecisionNodeIndex, DecoderTreeNode?>(index, this[index]);
        }
    }

    /// <summary>
    /// Finds the index of the single negated entry in the table.
    /// </summary>
    /// <returns>The index of the single negated entry if found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if multiple negated entries are found in the table.
    /// </exception>
    [Pure]
    private int? FindNegatedEntryIndex()
    {
        var negated = _negatedEntries.Index().Where(x => x.Item is not null).ToArray();

        return negated switch
        {
            { Length: 0 } => null,
            { Length: 1 } => negated[0].Index,
            _ => throw new InvalidOperationException("Multiple negated entries found in decision node.")
        };
    }
}

/// <inheritdoc cref="DecisionNode{TDefinition}"/>
/// <typeparam name="TIndexEnum">The enumeration type used to index the entries within the node.</typeparam>
public class DecisionNode<TIndexEnum> :
    DecisionNode
    where TIndexEnum : struct, Enum
{
    /// <inheritdoc cref="DecoderTreeNode.Definition"/>
    public new DecisionNodeDefinition<TIndexEnum> Definition => (DecisionNodeDefinition<TIndexEnum>)base.Definition;

#pragma warning disable CA1043

    /// <inheritdoc cref="DecisionNode.this"/>
    public DecoderTreeNode? this[DecisionNodeIndex<TIndexEnum> index]
    {
        get => base[DecisionNodeIndex.Create(Definition.GetSlotIndex(index.Index), index.IsNegated)];
        set => base[DecisionNodeIndex.Create(Definition.GetSlotIndex(index.Index), index.IsNegated)] = value;
    }

#pragma warning restore CA1043

    protected DecisionNode(DecisionNodeDefinition<TIndexEnum> definition) :
        base(definition)
    {
    }

    /// <inheritdoc cref="DecisionNode.EnumerateVirtualSlots"/>
    [Pure]
    public new IEnumerable<KeyValuePair<DecisionNodeIndex<TIndexEnum>, DecoderTreeNode?>> EnumerateVirtualSlots()
    {
        foreach (var index in Enum.GetValues<TIndexEnum>())
        {
            var i = DecisionNodeIndex<TIndexEnum>.Create(index, false);
            yield return new KeyValuePair<DecisionNodeIndex<TIndexEnum>, DecoderTreeNode?>(i, this[i]);
        }

        foreach (var index in Enum.GetValues<TIndexEnum>())
        {
            var i = DecisionNodeIndex<TIndexEnum>.Create(index, true);
            yield return new KeyValuePair<DecisionNodeIndex<TIndexEnum>, DecoderTreeNode?>(i, this[i]);
        }
    }
}

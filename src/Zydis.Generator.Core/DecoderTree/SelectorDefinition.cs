using System;
using System.Collections.Generic;
using System.IO;

namespace Zydis.Generator.Core.DecoderTree;

public sealed record SelectorDefinition
{
    /// <summary>
    /// The selector type name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A list that contains the name of each required parameter.
    /// </summary>
    public required IReadOnlyList<string> Parameters { get; init; }

    /// <summary>
    /// A list that contains the name of each slot.
    /// </summary>
    public required IReadOnlyList<string> Slots { get; init; }

    /// <summary>
    /// The number of parameters for this selector.
    /// </summary>
    public int NumberOfParameters => Parameters.Count;

    /// <summary>
    /// The number of the switch-table values supported by this selector.
    /// </summary>
    public int NumberOfEntries => Slots.Count;

    /// <summary>
    /// A human-readable description of the filter.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Parses a slot expression and returns the corresponding <see cref="SelectorTableIndex"/>.
    /// </summary>
    /// <param name="slotExpression">
    /// The slot expression to parse. This must be a non-empty string representing the name of a slot. If the expression
    /// starts with an exclamation mark ('!'), the resulting index will be marked as negated.
    /// </param>
    /// <returns>A <see cref="SelectorTableIndex"/> representing the index of the slot in the table and its negation state.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the slot name specified in <paramref name="slotExpression"/> does not exist in the table.
    /// </exception>
    public SelectorTableIndex ParseIndex(string slotExpression)
    {
        if (!TryParseIndex(slotExpression, out var index))
        {
            throw new ArgumentException($"Unknown slot name '{slotExpression}' for selector type '{this}'.", nameof(slotExpression));
        }

        return index;
    }

    /// <summary>
    /// Attempts to parse the specified slot expression and retrieve the corresponding index.
    /// </summary>
    /// <param name="slotExpression">
    /// The slot expression to parse. This must be a non-empty string representing the name of a slot. If the expression
    /// starts with an exclamation mark ('!'), the resulting index will be marked as negated.
    /// </param>
    /// <param name="index">
    /// Receives the parsed <see cref="SelectorTableIndex"/> representing the index of the slot in the table and its negation state.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the slot expression was successfully parsed and a matching index was found; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool TryParseIndex(string slotExpression, out SelectorTableIndex index)
    {
        ArgumentException.ThrowIfNullOrEmpty(slotExpression);

        var isNegated = slotExpression.StartsWith('!');
        if (isNegated)
        {
            slotExpression = slotExpression[1..];
        }

        for (var i = 0; i < Slots.Count; ++i)
        {
            if (!string.Equals(Slots[i], slotExpression, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            index = SelectorTableIndex.Create(i, isNegated);
            return true;
        }

        index = default;
        return false;
    }

    public override string ToString()
    {
        if (Parameters.Count is 0)
        {
            return Name;
        }

        return $"{Name}[{string.Join(',', Parameters)}]";
    }
}

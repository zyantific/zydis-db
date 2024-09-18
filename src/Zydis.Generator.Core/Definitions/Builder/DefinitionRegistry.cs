using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions.Builder;

/// <summary>
/// Maintains a collection of instruction definitions per encoding while ensuring a deterministic order.
/// Deduplicates definitions during insertion.
/// </summary>
internal sealed class DefinitionRegistry
{
    private static readonly SortedDictionary<InstructionDefinition, int> Empty = new(InstructionDefinitionTableComparer.Instance);

    private readonly Dictionary<InstructionEncoding, SortedDictionary<InstructionDefinition, int>> _instructions = new();

    public IEnumerable<InstructionDefinition> this[InstructionEncoding encoding] => _instructions.GetValueOrDefault(encoding, Empty).Keys;

    /// <summary>
    /// Retrieves the unique identifier for the specified instruction definition.
    /// </summary>
    /// <param name="definition">The instruction definition for which to retrieve the identifier.</param>
    /// <returns>The unique identifier associated with the specified instruction definition.</returns>
    /// <exception cref="ArgumentException">Thrown if the instruction encoding or definition is unknown.</exception>
    public int GetDefinitionId(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_instructions.TryGetValue(definition.Encoding, out var instructions))
        {
            throw new ArgumentException("Unknown instruction encoding.", nameof(definition));
        }

        if (!instructions.TryGetValue(definition, out var id))
        {
            throw new ArgumentException("Unknown instruction definition.", nameof(definition));
        }

        return instructions.Index().First(x => InstructionDefinitionTableComparer.Instance.Compare(x.Item.Key, definition) is 0).Index;
    }

    /// <summary>
    /// Inserts a new instruction definition into the registry.
    /// </summary>
    /// <param name="definition">The instruction definition to insert.</param>
    /// <remarks>
    /// If the encoding of the provided definition does not exist in the collection, a new entry is created. The definition is then added
    /// to a sorted dictionary associated with its encoding.
    /// </remarks>
    public void InsertDefinition(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_instructions.TryGetValue(definition.Encoding, out var instructions))
        {
            instructions = new SortedDictionary<InstructionDefinition, int>(InstructionDefinitionTableComparer.Instance);
            _instructions.Add(definition.Encoding, instructions);
        }

        if (instructions.ContainsKey(definition))
        {
            return;
        }

        instructions.Add(definition, instructions.Count);
    }
}

/// <summary>
/// Provides a mechanism to compare <see cref="InstructionDefinition"/> objects, ignoring all properties that are irrelevant for building
/// the instruction definition tables (e.g. all pattern related properties).
/// </summary>
internal sealed class InstructionDefinitionTableComparer :
    IComparer<InstructionDefinition>
{
    public static readonly InstructionDefinitionTableComparer Instance = new();

    public int Compare(InstructionDefinition? x, InstructionDefinition? y)
    {
        return FluentComparer.Compare(x, y,
            x => x.Compare(x => x.Mnemonic),
            x => x.Compare(x => x.MetaInfo),
            x => x.Compare(x => x.AffectedFlags),
            x => x.Compare(x => x.Vex),
            x => x.Compare(x => x.Evex),
            x => x.Compare(x => x.Mvex),
            x => x.Compare(x => x.OpsizeMap),
            x => x.Compare(x => x.AdsizeMap),
            x => x.CompareSequence(x => x.Operands),
            x => x.Compare(x => x.PrefixFlags),
            x => x.Compare(x => x.ExceptionClass),
            x => x.Compare(x => x.Flags),
            x => x.Compare(x => x.Cpl)
        );
    }
}

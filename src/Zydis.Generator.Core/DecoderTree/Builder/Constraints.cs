using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Identifies a single filter kind inside an instruction definition's pattern (e.g. <c>modrm_reg</c>).
/// </summary>
/// <param name="Name">The raw filter key, exactly as it appears in the pattern.</param>
internal readonly record struct FilterKey(string Name) :
    IComparable<FilterKey>
{
    /// <inheritdoc/>
    public int CompareTo(FilterKey other)
    {
        return string.CompareOrdinal(Name, other.Name);
    }
}

/// <summary>
/// A bitmask over the slots of a decision node, used to represent the set of slot values a filter constrains.
/// </summary>
/// <param name="Bits">The raw bitmask; bit <c>n</c> corresponds to slot index <c>n</c>.</param>
internal readonly record struct SlotMask(ulong Bits)
{
    /// <summary>
    /// The maximum number of slots a <see cref="SlotMask"/> can represent.
    /// </summary>
    public const int MaxSlotCount = sizeof(ulong) * 8;

    /// <summary>
    /// Creates a mask that covers every slot in a node with <paramref name="slotCount"/> slots.
    /// </summary>
    public static SlotMask All(int slotCount)
    {
        Debug.Assert(slotCount is > 0 and <= MaxSlotCount, "Slot count must fit into a 64-bit mask.");

        return new SlotMask(slotCount == MaxSlotCount ? ulong.MaxValue : (1UL << slotCount) - 1);
    }

    /// <summary>
    /// Creates a mask that covers only the slot at <paramref name="index"/>.
    /// </summary>
    public static SlotMask Single(int index)
    {
        Debug.Assert(index is >= 0 and < MaxSlotCount, "Slot index must fit into a 64-bit mask.");

        return new SlotMask(1UL << index);
    }

    /// <summary>
    /// Creates a mask that covers every slot in a node with <paramref name="slotCount"/> slots except the slot at
    /// <paramref name="index"/>. Used for negated filter values (e.g. <c>!64</c>).
    /// </summary>
    public static SlotMask AllExcept(int slotCount, int index)
    {
        return All(slotCount).Subtract(Single(index));
    }

    /// <summary>
    /// Determines whether this mask and <paramref name="other"/> share at least one slot.
    /// </summary>
    public bool Intersects(SlotMask other)
    {
        return (Bits & other.Bits) != 0;
    }

    /// <summary>
    /// Determines whether every slot in this mask is also present in <paramref name="other"/>.
    /// </summary>
    public bool IsSubsetOf(SlotMask other)
    {
        return (Bits & ~other.Bits) == 0;
    }

    /// <summary>
    /// Returns the slots shared between this mask and <paramref name="other"/>.
    /// </summary>
    public SlotMask Intersect(SlotMask other)
    {
        return new SlotMask(Bits & other.Bits);
    }

    /// <summary>
    /// Returns the slots of this mask that are not present in <paramref name="other"/>.
    /// </summary>
    public SlotMask Subtract(SlotMask other)
    {
        return new SlotMask(Bits & ~other.Bits);
    }

    /// <summary>
    /// Gets a value indicating whether this mask covers no slots at all.
    /// </summary>
    public bool IsEmpty => Bits == 0;
}

/// <summary>
/// A single resolved filter constraint parsed from an instruction definition's pattern.
/// </summary>
/// <param name="Key">The filter this constraint applies to.</param>
/// <param name="NodeDefinition">The decision node type the filter resolves to.</param>
/// <param name="NodeArguments">Parsed arguments from the filter's type expression (e.g. for <c>feature[amd]</c>).</param>
/// <param name="Slots">The set of slot values this definition matches for the filter.</param>
/// <param name="Index">The original parsed index, including negation, for tree building.</param>
internal sealed record FilterConstraint(
    FilterKey Key,
    DecisionNodeDefinition NodeDefinition,
    string[] NodeArguments,
    SlotMask Slots,
    DecisionNodeIndex Index);

/// <summary>
/// The canonical set of filter constraints parsed from an instruction definition's pattern.
/// </summary>
internal sealed class ConstraintSet
{
    // Informational flags present in the instruction data with no decision-node counterpart. The reference model
    // never sees them either, since it only consumes keys listed in `FixedFilterOrder`.
    private static readonly FrozenSet<string> SkippedFilterKeys =
        new[] { "force_modrm_reg", "force_modrm_rm" }.ToFrozenSet(StringComparer.Ordinal);

    private ConstraintSet(IReadOnlyDictionary<FilterKey, FilterConstraint> constraints)
    {
        Constraints = constraints;
    }

    /// <summary>
    /// Gets the resolved constraints, keyed by filter.
    /// </summary>
    public IReadOnlyDictionary<FilterKey, FilterConstraint> Constraints { get; }

    /// <summary>
    /// Attempts to get the constraint for the given <paramref name="key"/>.
    /// </summary>
    public bool TryGet(FilterKey key, out FilterConstraint constraint)
    {
        return Constraints.TryGetValue(key, out constraint!);
    }

    /// <summary>
    /// Returns a copy of this set with the constraint for <paramref name="key"/> removed, or the same instance when
    /// the key is absent.
    /// </summary>
    public ConstraintSet Without(FilterKey key)
    {
        if (!Constraints.ContainsKey(key))
        {
            return this;
        }

        var remaining = Constraints
            .Where(entry => !entry.Key.Equals(key))
            .ToFrozenDictionary(entry => entry.Key, entry => entry.Value);

        return new ConstraintSet(remaining);
    }

    /// <summary>
    /// Parses the <see cref="InstructionDefinition.Pattern"/> of <paramref name="definition"/> into a canonical set
    /// of filter constraints.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown if a filter key does not resolve to a known decision node type, or if its value is not a string.
    /// </exception>
    public static ConstraintSet Parse(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var constraints = new Dictionary<FilterKey, FilterConstraint>();

        if (definition.Pattern is not null)
        {
            // Sorted so that any future diagnostics/debugging output built from this set is deterministic.
            foreach (var (key, value) in definition.Pattern.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (SkippedFilterKeys.Contains(key))
                {
                    continue;
                }

                var (nodeDefinition, arguments) = DecisionNodes.ParseDecisionNodeType(key);

                Debug.Assert(nodeDefinition.NumberOfSlots <= SlotMask.MaxSlotCount,
                    $"Decision node '{nodeDefinition.Name}' has more slots than a {SlotMask.MaxSlotCount}-bit mask can represent.");

                if (value.ValueKind is not JsonValueKind.String)
                {
                    throw new NotSupportedException($"Filter '{key}' must have a string value.");
                }

                var rawValue = value.GetString()!;

                // "ignore" is a transitional alias meaning "no constraint" for the mandatory prefix filter.
                if (key is "mandatory_prefix" && rawValue is "ignore")
                {
                    continue;
                }

                var index = nodeDefinition.ParseSlotIndex(rawValue);
                var slots = index.IsNegated
                    ? SlotMask.AllExcept(nodeDefinition.NumberOfSlots, index.Index)
                    : SlotMask.Single(index.Index);

                var filterKey = new FilterKey(key);
                constraints[filterKey] = new FilterConstraint(filterKey, nodeDefinition, arguments, slots, index);
            }
        }

        return new ConstraintSet(constraints.ToFrozenDictionary());
    }
}

/// <summary>
/// Describes how the regions described by two <see cref="ConstraintSet"/>s relate to one another.
/// </summary>
internal enum RegionRelation
{
    /// <summary>The regions share no instruction encodings.</summary>
    Disjoint,

    /// <summary>The regions describe exactly the same set of instruction encodings.</summary>
    Equal,

    /// <summary>The first region fully contains the second.</summary>
    FirstContainsSecond,

    /// <summary>The second region fully contains the first.</summary>
    SecondContainsFirst,

    /// <summary>The regions overlap, but neither fully contains the other.</summary>
    IncomparableOverlap
}

/// <summary>
/// Computes containment and disjointness relations between the regions described by two <see cref="ConstraintSet"/>s.
/// </summary>
internal static class RegionAlgebra
{
    /// <summary>
    /// Determines how the regions described by <paramref name="a"/> and <paramref name="b"/> relate to one another.
    /// </summary>
    public static RegionRelation Relate(ConstraintSet a, ConstraintSet b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        foreach (var (key, constraint) in a.Constraints)
        {
            if (b.TryGet(key, out var other) && !constraint.Slots.Intersects(other.Slots))
            {
                return RegionRelation.Disjoint;
            }
        }

        var aContainsB = Contains(a, b);
        var bContainsA = Contains(b, a);

        return (aContainsB, bContainsA) switch
        {
            (true, true) => RegionRelation.Equal,
            (true, false) => RegionRelation.FirstContainsSecond,
            (false, true) => RegionRelation.SecondContainsFirst,
            (false, false) => RegionRelation.IncomparableOverlap
        };

        // A contains B iff every filter A constrains is also constrained by B, within A's slots.
        static bool Contains(ConstraintSet outer, ConstraintSet inner)
        {
            foreach (var (key, outerConstraint) in outer.Constraints)
            {
                if (!inner.TryGet(key, out var innerConstraint) || !innerConstraint.Slots.IsSubsetOf(outerConstraint.Slots))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

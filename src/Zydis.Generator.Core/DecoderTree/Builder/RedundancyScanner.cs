using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// Finds group members whose region is a strict subset of a sibling's region and whose definition is otherwise
/// identical to that sibling's - i.e. rows that add no decoding value and exist only because an earlier fixed
/// filter order needed a separate entry to reach the same outcome.
/// </summary>
internal static class RedundancyScanner
{
    public static IReadOnlyList<InstructionDefinition> FindRedundant(IReadOnlyList<GroupMember> members)
    {
        var redundant = new List<InstructionDefinition>();

        for (var i = 0; i < members.Count; i++)
        {
            for (var j = 0; j < members.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var narrower = members[i];
                var broader = members[j];

                var relation = RegionAlgebra.Relate(broader.Constraints, narrower.Constraints);
                if (relation is not RegionRelation.FirstContainsSecond)
                {
                    continue;
                }

                if (IsOtherwiseIdentical(narrower.Definition, broader.Definition))
                {
                    redundant.Add(narrower.Definition);
                }
            }
        }

        return redundant;
    }

    // "Otherwise identical" = every decoding-relevant field except Pattern (the filters themselves, which is
    // exactly what differs and is why the relation is a strict containment rather than Equal). Comment is also
    // excluded: it carries free-form provenance text copied from the original XED import (it has no [Emittable]
    // attribute, so it never reaches generated output) and legitimately differs between two rows that otherwise
    // describe the same decoding outcome. Every other field - including MetaInfo, whose IsaSet is emitted and
    // read back through the public API - must still match exactly, or the narrower row is a distinct instruction
    // form rather than a redundant duplicate.
    //
    // InstructionDefinition is a record, so `with` + `==` gives structural equality on scalar/record-typed fields
    // for free. Two fields hold their content in a reference-typed collection, though, and the compiler-generated
    // `==` compares those by reference rather than by content - two independently parsed definitions never share
    // the same collection instance even when their elements all compare equal - so both are compared explicitly
    // before falling back to `==` for everything else: Operands (IReadOnlyList<InstructionOperand>) via element
    // equality, and AffectedFlags.Flags (IReadOnlyDictionary<InstructionFlag, InstructionFlagOperation>, nested
    // inside the InstructionFlags record) via InstructionFlags' own IComparable, which already does a proper
    // key/value comparison.
    private static bool IsOtherwiseIdentical(InstructionDefinition narrower, InstructionDefinition broader)
    {
        if (!OperandsEqual(narrower.Operands, broader.Operands))
        {
            return false;
        }

        if (!AffectedFlagsEqual(narrower.AffectedFlags, broader.AffectedFlags))
        {
            return false;
        }

        return narrower with
        {
            Pattern = broader.Pattern,
            Operands = broader.Operands,
            Comment = broader.Comment,
            AffectedFlags = broader.AffectedFlags
        } == broader;
    }

    private static bool OperandsEqual(IReadOnlyList<InstructionOperand>? a, IReadOnlyList<InstructionOperand>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.SequenceEqual(b);
    }

    private static bool AffectedFlagsEqual(InstructionFlags? a, InstructionFlags? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.CompareTo(b) == 0;
    }
}

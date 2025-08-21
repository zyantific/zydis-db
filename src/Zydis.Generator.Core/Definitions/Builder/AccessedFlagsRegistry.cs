using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class AccessedFlagsRegistry
{
    private readonly SortedSet<DefinitionAccessedFlags> _accessedFlags = new();
    private readonly ConditionalWeakTable<InstructionDefinition, DefinitionAccessedFlags> _lut = new();

    public IEnumerable<DefinitionAccessedFlags> AccessedFlags => _accessedFlags;

    public void Initialize(IEnumerable<InstructionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
        {
            var flags = GetAccessedFlags(definition);

            _accessedFlags.Add(flags);
            _lut.Add(definition, flags);
        }
    }

    public int GetAccessedFlagsId(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var encoding = _lut.GetValue(definition, GetAccessedFlags);

        var id = _accessedFlags.Index().Where(x => Equals(x.Item, encoding)).Select(x => (int?)x.Index).FirstOrDefault();
        if (id is null)
        {
            throw new ArgumentException("Unknown instruction definition.", nameof(definition));
        }

        return id.Value;
    }

    private static readonly DefinitionAccessedFlags Empty = new() { CpuFlags = new(), FpuFlags = new() };

    private static DefinitionAccessedFlags GetAccessedFlags(InstructionDefinition definition)
    {
        if ((definition.AffectedFlags is null) ||
            (definition.AffectedFlags.Flags.All(x => x.Value is InstructionFlagOperation.None)))
        {
            return Empty;
        }

        var cpu = GetAccessedFlagsMasks(definition.AffectedFlags, GetCpuFlagsMask);
        var fpu = GetAccessedFlagsMasks(definition.AffectedFlags, GetFpuFlagsMask);

        return new DefinitionAccessedFlags { CpuFlags = cpu, FpuFlags = fpu };
    }

    private static AccessedFlags GetAccessedFlagsMasks(InstructionFlags affectedFlags, Func<InstructionFlags, InstructionFlagOperation, uint> getMaskFunc)
    {
        var tested = getMaskFunc(affectedFlags, InstructionFlagOperation.Tested);
        var testedModified = getMaskFunc(affectedFlags, InstructionFlagOperation.TestedModified);
        var modified = getMaskFunc(affectedFlags, InstructionFlagOperation.Modified);
        var set0 = getMaskFunc(affectedFlags, InstructionFlagOperation.Set0);
        var set1 = getMaskFunc(affectedFlags, InstructionFlagOperation.Set1);
        var undefined = getMaskFunc(affectedFlags, InstructionFlagOperation.Undefined);
        var allTested = tested | testedModified;
        var allModified = modified | testedModified;

        return new AccessedFlags
        {
            Tested = allTested,
            Modified = allModified,
            Set0 = set0,
            Set1 = set1,
            Undefined = undefined
        };
    }

    private static uint GetCpuFlagsMask(InstructionFlags flags, InstructionFlagOperation operation)
    {
        uint result = 0;

        foreach (var flag in Enum.GetValues<InstructionFlag>().Where(x => x.IsCpuFlag()))
        {
            if (!flags.Flags.TryGetValue(flag, out var flagOperation) || (flagOperation != operation))
            {
                continue;
            }

            var bitIndex = GetCpuFlagsBitIndex(flag);

            result |= (1u << bitIndex);
        }

        return result;

        static int GetCpuFlagsBitIndex(InstructionFlag flag)
        {
            return flag switch
            {
                InstructionFlag.CF => 0,
                InstructionFlag.PF => 2,
                InstructionFlag.AF => 4,
                InstructionFlag.ZF => 6,
                InstructionFlag.SF => 7,
                InstructionFlag.TF => 8,
                InstructionFlag.IF => 9,
                InstructionFlag.DF => 10,
                InstructionFlag.OF => 11,
                InstructionFlag.IOPL => 12,
                InstructionFlag.NT => 14,
                InstructionFlag.RF => 16,
                InstructionFlag.VM => 17,
                InstructionFlag.AC => 18,
                InstructionFlag.VIF => 19,
                InstructionFlag.VIP => 20,
                InstructionFlag.ID => 21,
                _ => throw new ArgumentOutOfRangeException(nameof(flag))
            };
        }
    }

    private static uint GetFpuFlagsMask(InstructionFlags flags, InstructionFlagOperation operation)
    {
        uint result = 0;

        foreach (var flag in Enum.GetValues<InstructionFlag>().Where(x => x.IsFpuFlag()))
        {
            if (!flags.Flags.TryGetValue(flag, out var flagOperation) || (flagOperation != operation))
            {
                continue;
            }

            var bitIndex = GetFpuFlagsBitIndex(flag);

            result |= (1u << bitIndex);
        }

        return result;

        static int GetFpuFlagsBitIndex(InstructionFlag flag)
        {
            return flag switch
            {
                InstructionFlag.C0 => 0,
                InstructionFlag.C1 => 1,
                InstructionFlag.C2 => 2,
                InstructionFlag.C3 => 3,
                _ => throw new ArgumentOutOfRangeException(nameof(flag))
            };
        }
    }
}

public sealed class DefinitionAccessedFlags :
    IComparable<DefinitionAccessedFlags>,
    IComparable,
    IEquatable<DefinitionAccessedFlags>
{
    [Emittable(0)]
    public required AccessedFlags CpuFlags { get; init; }

    [Emittable(1)]
    public required AccessedFlags FpuFlags { get; init; }

    public int CompareTo(DefinitionAccessedFlags? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.CpuFlags),
            x => x.Compare(x => x.FpuFlags)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as DefinitionAccessedFlags);
    }

    public bool Equals(DefinitionAccessedFlags? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return CpuFlags.Equals(other.CpuFlags) &&
               FpuFlags.Equals(other.FpuFlags);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is DefinitionAccessedFlags other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CpuFlags, FpuFlags);
    }
}

public sealed class AccessedFlags :
    IComparable<AccessedFlags>,
    IComparable,
    IEquatable<AccessedFlags>
{
    [Emittable(0)]
    public uint Tested { get; init; }

    [Emittable(1)]
    public uint Modified { get; init; }

    [Emittable(2, "set_0")]
    public uint Set0 { get; init; }

    [Emittable(3, "set_1")]
    public uint Set1 { get; init; }

    [Emittable(4)]
    public uint Undefined { get; init; }

    public int CompareTo(AccessedFlags? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Tested),
            x => x.Compare(x => x.Modified),
            x => x.Compare(x => x.Set0),
            x => x.Compare(x => x.Set1),
            x => x.Compare(x => x.Undefined)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as AccessedFlags);
    }

    public bool Equals(AccessedFlags? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (Tested == other.Tested) &&
               (Modified == other.Modified) &&
               (Set0 == other.Set0) &&
               (Set1 == other.Set1) &&
               (Undefined == other.Undefined);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is AccessedFlags other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Tested, Modified, Set0, Set1, Undefined);
    }
}

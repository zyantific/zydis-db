using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Helpers;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Definitions;

// TODO: Create specialized types and deserialize on base of the `operand_type` discriminator.

#pragma warning disable CA1036

public sealed class InstructionOperand :
    IComparable<InstructionOperand>,
    IComparable,
    IEquatable<InstructionOperand>

#pragma warning restore CA1036
{
    [JsonPropertyName("operand_type")]
    public OperandType Type { get; init; }

    [JsonPropertyName("action")]
    public OperandAccess Access { get; init; }

    public OperandEncoding Encoding { get; init; }
    public ElementType ElementType { get; init; }
    public ScaleFactor ScaleFactor { get; init; }
    public int Width16 { get; init; }
    public int Width32 { get; init; }
    public int Width64 { get; init; }

    [JsonPropertyName("visible")]
    public bool? IsVisible { get; init; }

    [JsonPropertyName("is_multisource4")]
    public bool IsMultiSource4 { get; init; }

    [JsonPropertyName("ignore_seg_override")]
    public bool IgnoreSegmentOverride { get; init; }

    public Register Register { get; init; }

    [JsonPropertyName("mem_segment")]
    public SegmentRegister? MemorySegment { get; init; }

    [JsonPropertyName("mem_base")]
    public BaseRegister? MemoryBase { get; init; }

    [JsonIgnore]
    public OperandVisibility Visibility => Type switch
    {
        OperandType.ImplicitReg or
        OperandType.ImplicitMem or
        OperandType.ImplicitImm1 => (IsVisible ?? true) ? OperandVisibility.Implicit : OperandVisibility.Hidden,
        _ => OperandVisibility.Explicit
    };

    public int CompareTo(InstructionOperand? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Type),
            x => x.Compare(x => x.Access),
            x => x.Compare(x => x.Encoding),
            x => x.Compare(x => x.ElementType),
            x => x.Compare(x => x.ScaleFactor),
            x => x.Compare(x => x.Width16),
            x => x.Compare(x => x.Width32),
            x => x.Compare(x => x.Width64),
            x => x.Compare(x => x.IsVisible),
            x => x.Compare(x => x.IsMultiSource4),
            x => x.Compare(x => x.IgnoreSegmentOverride),
            x => x.Compare(x => x.Register),
            x => x.Compare(x => x.MemorySegment),
            x => x.Compare(x => x.MemoryBase)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as InstructionOperand);
    }

    public bool Equals(InstructionOperand? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (Type == other.Type) &&
               (Access == other.Access) &&
               (Encoding == other.Encoding) &&
               (ElementType == other.ElementType) &&
               (ScaleFactor == other.ScaleFactor) &&
               (Width16 == other.Width16) &&
               (Width32 == other.Width32) &&
               (Width64 == other.Width64) &&
               (IsVisible == other.IsVisible) &&
               (IsMultiSource4 == other.IsMultiSource4) &&
               (IgnoreSegmentOverride == other.IgnoreSegmentOverride) &&
               (Register == other.Register) &&
               (MemorySegment == other.MemorySegment) &&
               (MemoryBase == other.MemoryBase);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is InstructionOperand other) && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add((int)Type);
        hashCode.Add((int)Access);
        hashCode.Add((int)Encoding);
        hashCode.Add((int)ElementType);
        hashCode.Add((int)ScaleFactor);
        hashCode.Add(Width16);
        hashCode.Add(Width32);
        hashCode.Add(Width64);
        hashCode.Add(IsVisible);
        hashCode.Add(IsMultiSource4);
        hashCode.Add(IgnoreSegmentOverride);
        hashCode.Add((int?)Register);
        hashCode.Add((int?)MemorySegment);
        hashCode.Add((int?)MemoryBase);
        return hashCode.ToHashCode();
    }
}

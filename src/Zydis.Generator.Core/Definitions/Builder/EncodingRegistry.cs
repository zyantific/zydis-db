using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class EncodingRegistry
{
    private readonly SortedSet<PhysicalInstructionEncoding> _encodings = new();
    private readonly ConditionalWeakTable<InstructionDefinition, PhysicalInstructionEncoding> _lut = new();

    public IEnumerable<PhysicalInstructionEncoding> Encodings => _encodings;

    public void Initialize(IEnumerable<InstructionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
        {
            var encoding = GetPhysicalInstructionEncoding(definition);

            _encodings.Add(encoding);
            _lut.Add(definition, encoding);
        }
    }

    public int GetEncodingId(InstructionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var encoding = _lut.GetValue(definition, GetPhysicalInstructionEncoding);

        var id = _encodings.Index().Where(x => Equals(x.Item, encoding)).Select(x => (int?)x.Index).FirstOrDefault();
        if (id is null)
        {
            throw new ArgumentException("Unknown instruction definition.", nameof(definition));
        }

        return id.Value;
    }

    private static PhysicalInstructionEncoding GetPhysicalInstructionEncoding(InstructionDefinition definition)
    {
        var hasModrm = (definition.GetDecisionNodeIndex(ModrmModNode.NodeDefinition.Instance) is not null) ||
                       (definition.GetDecisionNodeIndex(ModrmRegNode.NodeDefinition.Instance) is not null) ||
                       (definition.GetDecisionNodeIndex(ModrmRmNode.NodeDefinition.Instance) is not null);

        var hasIS4 = false;
        PhysicalInstructionEncodingDisp? displacement = null;
        List<PhysicalInstructionEncodingImm>? immediates = null;

        foreach (var operand in definition.Operands ?? [])
        {
            if (operand.Type is OperandType.PTR)
            {
                immediates =
                [
                    new() { Width16 = 16, Width32 = 32, Width64 = 32, IsAddress = true },
                    new() { Width16 = 16, Width32 = 16, Width64 = 16, IsAddress = true }
                ];
                break;
            }

            switch (operand.Encoding)
            {
                case OperandEncoding.ModrmReg:
                case OperandEncoding.ModrmRm:
                    hasModrm = true;
                    break;

                case OperandEncoding.IS4:
                    if (!hasIS4)
                    {
                        hasIS4 = true;
                        AddImmediate(new() { Width16 = 8, Width32 = 8, Width64 = 8 });
                    }

                    break;

                case OperandEncoding.Disp8:
                    displacement = new() { Width16 = 8, Width32 = 8, Width64 = 8 };
                    break;

                case OperandEncoding.Disp16:
                    displacement = new() { Width16 = 16, Width32 = 16, Width64 = 16 };
                    break;

                case OperandEncoding.Disp32:
                    displacement = new() { Width16 = 32, Width32 = 32, Width64 = 32 };
                    break;

                case OperandEncoding.Disp64:
                    displacement = new() { Width16 = 64, Width32 = 64, Width64 = 64 };
                    break;

                case OperandEncoding.Disp16_32_64:
                    displacement = new() { Width16 = 16, Width32 = 32, Width64 = 64 };
                    break;

                case OperandEncoding.Disp32_32_64:
                    displacement = new() { Width16 = 32, Width32 = 32, Width64 = 648 };
                    break;

                case OperandEncoding.Disp16_32_32:
                    displacement = new() { Width16 = 16, Width32 = 32, Width64 = 32 };
                    break;

                case OperandEncoding.Uimm8:
                    AddImmediate(new() { Width16 = 8, Width32 = 8, Width64 = 8 });
                    break;

                case OperandEncoding.Uimm16:
                    AddImmediate(new() { Width16 = 16, Width32 = 16, Width64 = 16 });
                    break;

                case OperandEncoding.Uimm32:
                    AddImmediate(new() { Width16 = 32, Width32 = 32, Width64 = 32 });
                    break;

                case OperandEncoding.Uimm64:
                    AddImmediate(new() { Width16 = 64, Width32 = 64, Width64 = 64 });
                    break;

                case OperandEncoding.Uimm16_32_64:
                    AddImmediate(new() { Width16 = 16, Width32 = 32, Width64 = 64 });
                    break;

                case OperandEncoding.Uimm32_32_64:
                    AddImmediate(new() { Width16 = 32, Width32 = 32, Width64 = 64 });
                    break;

                case OperandEncoding.Uimm16_32_32:
                    AddImmediate(new() { Width16 = 16, Width32 = 32, Width64 = 64 });
                    break;

                case OperandEncoding.Simm8:
                    AddImmediate(new() { Width16 = 8, Width32 = 8, Width64 = 8, IsSigned = true });
                    break;

                case OperandEncoding.Simm16:
                    AddImmediate(new() { Width16 = 16, Width32 = 16, Width64 = 16, IsSigned = true });
                    break;

                case OperandEncoding.Simm32:
                    AddImmediate(new() { Width16 = 32, Width32 = 32, Width64 = 32, IsSigned = true });
                    break;

                case OperandEncoding.Simm64:
                    AddImmediate(new() { Width16 = 64, Width32 = 64, Width64 = 64, IsSigned = true });
                    break;

                case OperandEncoding.Simm16_32_64:
                    AddImmediate(new() { Width16 = 16, Width32 = 32, Width64 = 64, IsSigned = true });
                    break;

                case OperandEncoding.Simm32_32_64:
                    AddImmediate(new() { Width16 = 32, Width32 = 328, Width64 = 64, IsSigned = true });
                    break;

                case OperandEncoding.Simm16_32_32:
                    AddImmediate(new() { Width16 = 16, Width32 = 32, Width64 = 32, IsSigned = true });
                    break;

                case OperandEncoding.Jimm8:
                    AddImmediate(new() { Width16 = 8, Width32 = 8, Width64 = 8, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                case OperandEncoding.Jimm16:
                    AddImmediate(new() { Width16 = 16, Width32 = 16, Width64 = 16, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                case OperandEncoding.Jimm32:
                    AddImmediate(new() { Width16 = 32, Width32 = 32, Width64 = 32, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                case OperandEncoding.Jimm64:
                    AddImmediate(new() { Width16 = 64, Width32 = 64, Width64 = 64, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                case OperandEncoding.Jimm16_32_64:
                    AddImmediate(new() { Width16 = 16, Width32 = 32, Width64 = 64, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                case OperandEncoding.Jimm32_32_64:
                    AddImmediate(new() { Width16 = 32, Width32 = 32, Width64 = 64, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                case OperandEncoding.Jimm16_32_32:
                    AddImmediate(new() { Width16 = 16, Width32 = 32, Width64 = 32, IsSigned = (operand.Type is OperandType.REL), IsAddress = true, IsRelative = (operand.Type is OperandType.REL) });
                    break;

                default:
                    break;
            }
        }

        return new PhysicalInstructionEncoding
        {
            HasModrm = hasModrm,
            Displacement = displacement,
            Immediate0 = immediates?.FirstOrDefault(),
            Immediate1 = immediates?.Skip(1).FirstOrDefault(),
            ForceRegForm = definition.Flags.HasFlag(InstructionFlagsEnum.ForceRegForm)
        };

        void AddImmediate(PhysicalInstructionEncodingImm immediate)
        {
            immediates ??= new List<PhysicalInstructionEncodingImm>();
            immediates.Add(immediate);
        }
    }
}

#pragma warning disable CA1036

public sealed class PhysicalInstructionEncoding :
    IComparable<PhysicalInstructionEncoding>,
    IComparable,
    IEquatable<PhysicalInstructionEncoding>

#pragma warning restore CA1036
{
    public bool HasModrm { get; init; }
    public PhysicalInstructionEncodingDisp? Displacement { get; init; }
    public PhysicalInstructionEncodingImm? Immediate0 { get; init; }
    public PhysicalInstructionEncodingImm? Immediate1 { get; init; }
    public bool ForceRegForm { get; init; }

    public int CompareTo(PhysicalInstructionEncoding? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.HasModrm),
            x => x.Compare(x => x.Displacement),
            x => x.Compare(x => x.Immediate0),
            x => x.Compare(x => x.Immediate1),
            x => x.Compare(x => x.ForceRegForm)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as PhysicalInstructionEncoding);
    }

    public bool Equals(PhysicalInstructionEncoding? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (HasModrm == other.HasModrm) &&
               Equals(Displacement, other.Displacement) &&
               Equals(Immediate0, other.Immediate0) &&
               Equals(Immediate1, other.Immediate1) &&
               (ForceRegForm == other.ForceRegForm);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is PhysicalInstructionEncoding other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(HasModrm, Displacement, Immediate0, Immediate1, ForceRegForm);
    }
}

#pragma warning disable CA1036

public sealed class PhysicalInstructionEncodingDisp :
    IComparable<PhysicalInstructionEncodingDisp>,
    IComparable,
    IEquatable<PhysicalInstructionEncodingDisp>

#pragma warning restore CA1036
{
    public int Width16 { get; init; }
    public int Width32 { get; init; }
    public int Width64 { get; init; }

    public int CompareTo(PhysicalInstructionEncodingDisp? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Width16),
            x => x.Compare(x => x.Width32),
            x => x.Compare(x => x.Width64)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as PhysicalInstructionEncodingDisp);
    }

    public bool Equals(PhysicalInstructionEncodingDisp? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (Width16 == other.Width16) &&
               (Width32 == other.Width32) &&
               (Width64 == other.Width64);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is PhysicalInstructionEncodingDisp other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width16, Width32, Width64);
    }
}

#pragma warning disable CA1036

[Emittable(0, "size")]
public sealed class PhysicalInstructionEncodingImm :
    IComparable<PhysicalInstructionEncodingImm>,
    IComparable,
    IEquatable<PhysicalInstructionEncodingImm>

#pragma warning restore CA1036
{
    public int Width16 { get; init; }
    public int Width32 { get; init; }
    public int Width64 { get; init; }

    [Emittable(1)]
    public bool IsSigned { get; init; }

    [Emittable(2)]
    public bool IsAddress { get; init; }

    [Emittable(3)]
    public bool IsRelative { get; init; }

    public int CompareTo(PhysicalInstructionEncodingImm? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Width16),
            x => x.Compare(x => x.Width32),
            x => x.Compare(x => x.Width64),
            x => x.Compare(x => x.IsSigned),
            x => x.Compare(x => x.IsAddress),
            x => x.Compare(x => x.IsRelative)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as PhysicalInstructionEncodingImm);
    }

    public bool Equals(PhysicalInstructionEncodingImm? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (Width16 == other.Width16) &&
               (Width32 == other.Width32) &&
               (Width64 == other.Width64) &&
               (IsSigned == other.IsSigned) &&
               (IsAddress == other.IsAddress) &&
               (IsRelative == other.IsRelative);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is PhysicalInstructionEncodingImm other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width16, Width32, Width64, IsSigned, IsAddress, IsRelative);
    }
}

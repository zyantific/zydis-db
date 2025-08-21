using System;
using System.Collections.Generic;
using System.Linq;

namespace Zydis.Generator.Core.Definitions.Builder;

internal sealed class RelativeInfoRegistry
{
    public readonly List<RelInfo> Infos = new();

    public readonly List<List<string>> Mnemonics = new();

    internal sealed class RelInfo : IEquatable<RelInfo>
    {
        public int[,] Size { get; private set; }
        public SizeHint SizeHint { get; init; }
        public bool AcceptsBranchHints { get; init; }
        public bool AcceptsBound { get; private set; }

        public RelInfo(EncodableDefinition definition)
        {
            Size = new int[3, 3];
            SizeHint = definition.GetSizeHint();
            AcceptsBranchHints = definition.Instruction.PrefixFlags.HasFlag(PrefixFlags.AcceptsBranchHints);
            AcceptsBound = definition.Instruction.PrefixFlags.HasFlag(PrefixFlags.AcceptsBOUND);
        }

        public void Merge(EncodableDefinition definition, InstructionOperand relativeOperand)
        {
            AcceptsBound |= definition.Instruction.PrefixFlags.HasFlag(PrefixFlags.AcceptsBOUND);
            List<int> modes = definition.Modes switch
            {
                WidthFlag.Width64 => [2],
                WidthFlag.Width16 | WidthFlag.Width32 => [0, 1],
                WidthFlag.Width16 | WidthFlag.Width32 | WidthFlag.Width64 => [0, 1, 2],
                _ => throw new NotSupportedException("Unsupposted operand size combination"),
            };
            var scalingType = ScaleFactor.ScaleOSZ;
            var addressSize = 0;
            if (definition.AddressSizes is WidthFlag.Width16 or WidthFlag.Width32 or WidthFlag.Width64)
            {
                scalingType = ScaleFactor.ScaleASZ;
                addressSize = definition.AddressSizes switch
                {
                    WidthFlag.Width16 => 0,
                    WidthFlag.Width32 => 1,
                    WidthFlag.Width64 => 2,
                    _ => throw new NotImplementedException(),
                };
            }
            foreach (var mode in modes)
            {
                switch (relativeOperand.Encoding)
                {
                    case OperandEncoding.Jimm8:
                        Size[mode, 0] = scalingType == ScaleFactor.ScaleASZ && addressSize != mode ? definition.MinSize + 1 : definition.MinSize;
                        break;
                    case OperandEncoding.Jimm32:
                        Size[mode, 2] = scalingType == ScaleFactor.ScaleOSZ && mode == 0 ? definition.MinSize + 1 : definition.MinSize;
                        break;
                    case OperandEncoding.Jimm16_32_32:
                        Size[mode, 1] = scalingType == ScaleFactor.ScaleOSZ && mode != 0 ? definition.MinSize + 1 : definition.MinSize;
                        Size[mode, 2] = scalingType == ScaleFactor.ScaleOSZ && mode == 0 ? definition.MaxSize + 1 : definition.MaxSize;
                        break;
                    default:
                        throw new NotSupportedException("Unsupported encoding");
                }
            }
        }

        public override bool Equals(object? obj) => Equals(obj as RelInfo);

        public bool Equals(RelInfo? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Size.Cast<int>().SequenceEqual(other.Size.Cast<int>()) &&
                SizeHint == other.SizeHint &&
                AcceptsBranchHints == other.AcceptsBranchHints &&
                AcceptsBound == other.AcceptsBound;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Size, SizeHint, AcceptsBranchHints, AcceptsBound);
        }
    }

    public void Initialize(List<EncodableDefinition> definitions)
    {
        var relativeInstructions = new Dictionary<string, RelInfo>();
        foreach (var definition in definitions)
        {
            var hasRelativeOperand = false;
            var hasImmediateOperand = false;
            foreach (var operand in definition.GetVisibleOperands())
            {
                if (operand.Type == OperandType.IMM)
                {
                    hasImmediateOperand = true;
                }
                if (operand.Type != OperandType.REL)
                {
                    continue;
                }
                if (hasRelativeOperand)
                {
                    throw new NotSupportedException("Invalid relative instruction definition");
                }
                hasRelativeOperand = true;
                if (!relativeInstructions.TryGetValue(definition.Instruction.Mnemonic, out var info))
                {
                    info = new RelInfo(definition);
                    relativeInstructions.Add(definition.Instruction.Mnemonic, info);
                }
                info.Merge(definition, operand);
            }
            if (hasRelativeOperand && hasImmediateOperand)
            {
                throw new NotSupportedException("Invalid relative instruction definition");
            }
        }

        foreach (var (mnemonic, info) in relativeInstructions)
        {
            var index = Infos.IndexOf(info);
            if (index != -1)
            {
                Mnemonics[index].Add(mnemonic);
            }
            else
            {
                Infos.Add(info);
                Mnemonics.Add([mnemonic]);
            }
        }
    }
}

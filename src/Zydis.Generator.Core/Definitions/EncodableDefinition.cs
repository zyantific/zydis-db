using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Extensions;
using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1036

public sealed class EncodableDefinition :
    IComparable<EncodableDefinition>

#pragma warning restore CA1036
{
    public InstructionDefinition Instruction { get; init; }

    public bool IsEncodable { get; set; }

    public bool IsSwappable { get => (SwappableIndex & 1) != 0; }

    public WidthFlag Modes { get; set; }

    public WidthFlag AddressSizes { get; set; }

    public WidthFlag OperandSizes { get; set; }

    public Rex2Type Rex2 { get; set; }

    public MandatoryPrefix MandatoryPrefix { get; set; }

    public int Modrm { get; set; }

    public int MinSize { get; set; }

    public int MaxSize { get; set; }

    public bool RexW { get; set; }

    public bool EvexNd { get; set; }

    public bool EvexNf { get; set; }

    public VectorLength VectorLength { get; set; }

    public bool ApxOsz { get; set; }

    public int SwappableIndex { get; set; }

    private bool _forcedSizeHint;

    public EncodableDefinition(InstructionDefinition instruction)
    {
        Instruction = instruction;
        IsEncodable = !GetBoolFilter(SelectorDefinitions.FeatureAmd);
        if (Instruction.Mnemonic == "nop" && IsEncodable)
        {
            if (!GetBoolFilter(SelectorDefinitions.FeatureMpx, true) ||
                !GetBoolFilter(SelectorDefinitions.FeatureCldemote, true) ||
                !GetBoolFilter(SelectorDefinitions.FeatureCet, true) ||
                !GetBoolFilter(SelectorDefinitions.FeatureKnc, true) ||
                (GetFilter(SelectorDefinitions.ModrmReg)?.StartsWith('!') ?? false))
            {
                IsEncodable = false;
            }
        }
        AddressSizes = GetWidthFilter(SelectorDefinitions.AddressSize);
        OperandSizes = GetWidthFilter(SelectorDefinitions.OperandSize);
        RexW = GetBoolFilter(SelectorDefinitions.RexW);
        EvexNd = GetBoolFilter(SelectorDefinitions.EvexNd);
        EvexNf = GetBoolFilter(SelectorDefinitions.EvexNf);
        SetAllowedModes();
        SetRex2();
        SetMandatoryPrefix();
        SetModrm();
        SetVectorLength();
        EstimateSize();
    }

    private void EstimateSize()
    {
        MinSize = 1;
        var rexW = GetBoolFilter(SelectorDefinitions.RexW);
        if (Instruction.Encoding is not (InstructionEncoding.XOP or InstructionEncoding.VEX or InstructionEncoding.EVEX or InstructionEncoding.MVEX) &&
            MandatoryPrefix is not (MandatoryPrefix.None or MandatoryPrefix.Ignore))
        {
            MinSize += 1;
        }
        switch (Instruction.Encoding)
        {
            case InstructionEncoding.Default or InstructionEncoding.AMD3DNOW:
                MinSize += Instruction.OpcodeMap switch
                {
                    OpcodeMap.MAP0 => 0,
                    OpcodeMap.M0F => 1,
                    OpcodeMap.M0F0F or OpcodeMap.M0F38 or OpcodeMap.M0F3A => 2,
                    _ => throw new NotSupportedException($"Invalid opcode map {Instruction.OpcodeMap}")
                };
                if (rexW)
                {
                    MinSize += 1;
                }
                if (Rex2 == Rex2Type.Mandatory)
                {
                    MinSize += 2;
                }
                break;
            case InstructionEncoding.XOP:
                MinSize += 3;
                break;
            case InstructionEncoding.VEX:
                MinSize += 2;
                if (rexW || Instruction.OpcodeMap != OpcodeMap.M0F)
                {
                    MinSize += 1;
                }
                break;
            case InstructionEncoding.EVEX or InstructionEncoding.MVEX:
                MinSize += 4;
                break;
            default:
                throw new NotSupportedException($"Invalid encoding {Instruction.Encoding}");
        }

        var overhead = 0;
        var hasSib = false;
        var hasMem = false;
        var hasModrm = false;
        foreach (var operand in GetVisibleOperands())
        {
            if (operand.Encoding is OperandEncoding.ModrmReg or OperandEncoding.ModrmRm)
            {
                hasModrm = true;
            }
            overhead += operand.Encoding switch
            {
                OperandEncoding.Uimm16_32_32 or OperandEncoding.Simm16_32_32 or OperandEncoding.Jimm16_32_32 => 2,
                OperandEncoding.Uimm16_32_64 or OperandEncoding.Simm16_32_64 or OperandEncoding.Jimm16_32_64 => 6,
                _ => 0,
            };
            MinSize += operand.Encoding switch
            {
                OperandEncoding.Uimm8 or OperandEncoding.Simm8 or OperandEncoding.Jimm8 => 1,
                OperandEncoding.Uimm16 or OperandEncoding.Simm16 or OperandEncoding.Jimm16 or
                OperandEncoding.Uimm16_32_64 or OperandEncoding.Uimm16_32_32 or
                OperandEncoding.Simm16_32_64 or OperandEncoding.Simm16_32_32 or
                OperandEncoding.Jimm16_32_64 or OperandEncoding.Jimm16_32_32 => 2,
                OperandEncoding.Uimm32 or OperandEncoding.Simm32 or OperandEncoding.Jimm32 => 4,
                OperandEncoding.Uimm64 or OperandEncoding.Simm64 or OperandEncoding.Jimm64 => 8,
                _ => 0,
            };
            if (operand.Type is OperandType.ImplicitMem or OperandType.MEM or OperandType.AGEN or OperandType.AGENNoRel)
            {
                hasMem = true;
            }
            else if (operand.Type is OperandType.MEMVSIBX or OperandType.MEMVSIBY or OperandType.MEMVSIBZ or OperandType.MIB)
            {
                hasMem = true;
                hasSib = true;
            }
        }
        if (hasModrm || Modrm != 0)
        {
            MinSize += 1;
        }
        if (hasSib || (hasMem && GetIntFilter(SelectorDefinitions.ModrmRm) == 4))
        {
            MinSize += 1;
        }
        MaxSize = MinSize + overhead;
    }

    private void SetVectorLength()
    {
        var vectorLength = GetVectorLengthFilter(SelectorDefinitions.VectorLength);
        var evexVectorLength = Instruction.Evex?.VectorLength ?? VectorLength.Default;
        if (vectorLength != evexVectorLength)
        {
            if (vectorLength == VectorLength.Default)
            {
                vectorLength = evexVectorLength;
            }
            else if (evexVectorLength != VectorLength.Default)
            {
                throw new NotSupportedException($"Vector length conflict between {vectorLength} and {evexVectorLength}");
            }
        }
        VectorLength = vectorLength;
    }

    private void SetModrm()
    {
        Modrm |= GetFilter(SelectorDefinitions.ModrmMod) switch
        {
            null or "!3" => 0,
            "3" => 3 << 6,
            _ => throw new NotSupportedException("Invalid value for ModrmMod selector")
        };
        Modrm |= (GetIntFilter(SelectorDefinitions.ModrmReg) << 3) | GetIntFilter(SelectorDefinitions.ModrmRm);
    }

    private void SetMandatoryPrefix()
    {
        MandatoryPrefix = GetFilter(SelectorDefinitions.MandatoryPrefix) switch
        {
            null or "none" => MandatoryPrefix.None,
            "ignore" => MandatoryPrefix.Ignore,
            "66" => MandatoryPrefix.P66,
            "f2" => MandatoryPrefix.PF2,
            "f3" => MandatoryPrefix.PF3,
            _ => throw new NotSupportedException("Invalid value for MandatoryPrefix selector")
        };
    }

    private void SetRex2()
    {
        Rex2 = GetFilter(SelectorDefinitions.Rex2) switch
        {
            null => Rex2Type.Allowed,
            "no_rex2" => Rex2Type.Forbidden,
            "rex2" => Rex2Type.Mandatory,
            _ => throw new NotSupportedException("Invalid value for Rex2 selector")
        };
        if (Instruction.Encoding != InstructionEncoding.Default || Instruction.OpcodeMap is not (OpcodeMap.MAP0 or OpcodeMap.M0F))
        {
            if (Rex2 == Rex2Type.Mandatory)
            {
                throw new NotSupportedException("Forcing REX2 is impossible for this definition");
            }
            Rex2 = Rex2Type.Forbidden;
        }
    }

    private void SetAllowedModes()
    {
        Modes = GetWidthFilter(SelectorDefinitions.Mode);
        var rexW = GetBoolFilter(SelectorDefinitions.RexW);
        var force64 = rexW && Instruction.Encoding is not (InstructionEncoding.VEX or InstructionEncoding.EVEX or InstructionEncoding.XOP);
        switch (Modes)
        {
            case WidthFlag.Width16 | WidthFlag.Width32:
                if (force64)
                {
                    throw new NotSupportedException();
                }
                if (AddressSizes == WidthFlag.Width64 || OperandSizes == WidthFlag.Width64)
                {
                    throw new NotSupportedException();
                }
                break;
            case WidthFlag.Width64:
                if (AddressSizes == WidthFlag.Width16)
                {
                    throw new NotSupportedException();
                }
                break;
            case WidthFlag.Width16 | WidthFlag.Width32 | WidthFlag.Width64:
                if (force64 || AddressSizes == WidthFlag.Width64 || OperandSizes == WidthFlag.Width64)
                {
                    Modes = WidthFlag.Width64;
                }
                break;
            default:
                throw new NotSupportedException($"Invalid mode {Modes.ToZydisString()}");
        }
    }

    public WidthFlag GetEffectiveAddressSize()
    {
        var mask = WidthFlag.Invalid;
        if (Modes.HasFlag(WidthFlag.Width16) || Modes.HasFlag(WidthFlag.Width32))
        {
            mask |= WidthFlag.Width16 | WidthFlag.Width32;
        }
        if (Modes.HasFlag(WidthFlag.Width64))
        {
            mask |= WidthFlag.Width32 | WidthFlag.Width64;
        }
        return AddressSizes & mask;
    }

    public int GetOperandMask()
    {
        var mask = 0;
        var bitOffset = 3;
        foreach (var operand in GetVisibleOperands())
        {
            var type = operand.Type switch
            {
                OperandType.ImplicitReg or OperandType.GPR8 or OperandType.GPR16 or OperandType.GPR32 or OperandType.GPR64 or
                OperandType.GPR16_32_64 or OperandType.GPR32_32_64 or OperandType.GPR16_32_32 or OperandType.GPRASZ or
                OperandType.FPR or OperandType.MMX or OperandType.XMM or OperandType.YMM or OperandType.ZMM or
                OperandType.TMM or OperandType.BND or OperandType.SREG or OperandType.CR or OperandType.DR or
                OperandType.MASK => 0,
                OperandType.ImplicitMem or OperandType.MEM or OperandType.MEMVSIBX or OperandType.MEMVSIBY or OperandType.MEMVSIBZ or
                OperandType.AGEN or OperandType.AGENNoRel or OperandType.MIB or OperandType.MOFFS => 1,
                OperandType.PTR => 2,
                OperandType.ImplicitImm1 or OperandType.IMM or OperandType.REL or OperandType.ABS => 3,
                _ => throw new NotSupportedException($"Invalid operand type {operand.Type}")
            };
            mask |= type << bitOffset;
            bitOffset += 2;
        }
        mask |= (bitOffset - 3) / 2;
        return mask;
    }

    private MandatoryPrefix GetRepeatPrefix()
    {
        if (MandatoryPrefix == MandatoryPrefix.PF2 && Instruction.PrefixFlags.HasFlag(PrefixFlags.AcceptsREPNEREPNZ))
        {
            return MandatoryPrefix.PF2;
        }
        else if (MandatoryPrefix == MandatoryPrefix.PF3 && (Instruction.PrefixFlags.HasFlag(PrefixFlags.AcceptsREP) ||
            Instruction.PrefixFlags.HasFlag(PrefixFlags.AcceptsREPEREPZ)))
        {
            return MandatoryPrefix.PF3;
        }
        else if (MandatoryPrefix == MandatoryPrefix.Ignore)
        {
            return MandatoryPrefix.Ignore;
        }
        return MandatoryPrefix.None;
    }

    private static (bool, bool) GetOperandScalings(IEnumerable<InstructionOperand> operands)
    {
        bool aszScale = false, oszScale = false;
        foreach (var operand in operands)
        {
            if (operand.Register.GetRegisterClass() == RegisterClass.GPRASZ ||
                operand.Type == OperandType.GPRASZ ||
                operand.ScaleFactor == ScaleFactor.ScaleASZ ||
                operand.MemoryBase is not (null or BaseRegister.SSP or BaseRegister.SBP))
            {
                aszScale = true;
            }
            else if (operand.Register.GetRegisterClass() == RegisterClass.GPROSZ ||
                operand.Type is OperandType.GPR16_32_32 or OperandType.GPR32_32_64 or OperandType.GPR16_32_64 ||
                (operand.ScaleFactor == ScaleFactor.ScaleOSZ && operand.Type != OperandType.IMM))
            {
                oszScale = true;
            }
        }
        return (aszScale, oszScale);
    }

    public SizeHint GetSizeHint()
    {
        var isBranching = Instruction.GetBranchType() != BranchType.None;
        var hasRelativeOperand = GetVisibleOperands().Any(x => x.Type == OperandType.REL);
        var (visibleAszScale, visibleOszScale) = GetOperandScalings(GetVisibleOperands());
        var (hiddenAszScale, hiddenOszScale) = GetOperandScalings(GetHiddenOperands());
        if (_forcedSizeHint)
        {
            if (visibleAszScale || visibleOszScale || hiddenAszScale || hiddenOszScale)
            {
                throw new NotSupportedException("Tried to force size hint on instruction with explicit scaling");
            }
            return SizeHint.OperandSize;
        }
        if (!visibleAszScale && hiddenAszScale && AddressSizes == (WidthFlag.Width16 | WidthFlag.Width32 | WidthFlag.Width64))
        {
            return SizeHint.AddressSize;
        }
        if (!isBranching)
        {
            if (hasRelativeOperand ||
                (!visibleOszScale && hiddenOszScale && OperandSizes == (WidthFlag.Width16 | WidthFlag.Width32 | WidthFlag.Width64)))
            {
                return SizeHint.OperandSize;
            }
        }
        return SizeHint.None;
    }

    public IEnumerable<InstructionOperand> GetVisibleOperands()
    {
        foreach (var operand in Instruction.Operands ?? Enumerable.Empty<InstructionOperand>())
        {
            if (operand.Visibility == OperandVisibility.Hidden)
            {
                yield break;
            }
            yield return operand;
        }
    }

    public IEnumerable<InstructionOperand> GetHiddenOperands()
    {
        foreach (var operand in Instruction.Operands ?? Enumerable.Empty<InstructionOperand>())
        {
            if (operand.Visibility == OperandVisibility.Hidden)
            {
                yield return operand;
            }
        }
    }

    public WidthFlag GetWidthFilter(SelectorDefinition selector)
    {
        return GetFilter(selector) switch
        {
            null => WidthFlag.Width16 | WidthFlag.Width32 | WidthFlag.Width64,
            "16" => WidthFlag.Width16,
            "32" => WidthFlag.Width32,
            "64" => WidthFlag.Width64,
            "!16" => WidthFlag.Width32 | WidthFlag.Width64,
            "!32" => WidthFlag.Width16 | WidthFlag.Width64,
            "!64" => WidthFlag.Width16 | WidthFlag.Width32,
            _ => throw new ArgumentOutOfRangeException(nameof(selector), $"Invalid width value for selector ${selector.Name}")
        };
    }

    public VectorLength GetVectorLengthFilter(SelectorDefinition selector)
    {
        return GetFilter(selector) switch
        {
            null => VectorLength.Default,
            "128" => VectorLength.V128,
            "256" => VectorLength.V256,
            "512" => VectorLength.V512,
            _ => throw new ArgumentOutOfRangeException(nameof(selector), $"Invalid vector length value for selector ${selector.Name}")
        };
    }

    public int GetIntFilter(SelectorDefinition selector, int defaultValue = 0)
    {
        return GetFilter(selector) switch
        {
            null => defaultValue,
            var value => !value.StartsWith('!') ? int.Parse(value) : defaultValue
        };
    }

    public bool GetBoolFilter(SelectorDefinition selector, bool defaultValue = false)
    {
        return GetFilter(selector) switch
        {
            null => defaultValue,
            "0" => false,
            "1" => true,
            _ => throw new ArgumentOutOfRangeException(nameof(selector), $"Invalid boolean value for selector ${selector.Name}")
        };
    }

    public string? GetFilter(SelectorDefinition selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!(Instruction.SelectorValues?.TryGetValue(selector.Name, out var value) ?? false))
        {
            return null;
        }
        return value.ValueKind is JsonValueKind.String ? value.ToString() : throw new InvalidDataException();
    }

    public bool Merge(EncodableDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!IsSignatureCompatible(definition))
        {
            return false;
        }
        Modes |= definition.Modes;
        OperandSizes |= definition.OperandSizes;
        if (Rex2 != definition.Rex2)
            Rex2 = Rex2Type.AlwaysAllowed; // TODO: For now Forbidden + Mantatory => Allowed
        EstimateSize();
        return true;
    }

    private bool IsApxOszScaling()
    {
        return Instruction.MetaInfo.IsaExtension.StartsWith("APX") &&
            Instruction.Encoding == InstructionEncoding.EVEX &&
            GetVisibleOperands().Any(x => x.Type == OperandType.MEM && x.ScaleFactor == ScaleFactor.ScaleOSZ);
    }

    public Dictionary<string, string> GetSelectors()
    {
        return Instruction.SelectorValues?.Select(x => (x.Key, x.Value.ToString())).ToDictionary() ?? [];
    }

    public Dictionary<string, string> GetSelectors(params string[] ignoreList)
    {
        ArgumentNullException.ThrowIfNull(ignoreList);

        var selectors = GetSelectors();
        foreach (var ignored in ignoreList)
        {
            selectors.Remove(ignored);
        }
        return selectors;
    }

    private static bool CompareOperandsForHiddenScaling(IEnumerable<InstructionOperand> operands1, IEnumerable<InstructionOperand> operands2)
    {
        var operandsCount = operands1.Count();
        if (operandsCount == 0 || operandsCount != operands2.Count())
        {
            return false;
        }
        var hasHiddenScaling = false;
        foreach (var (op1, op2) in operands1.Zip(operands2))
        {
            if (op1.Equals(op2))
            {
                continue;
            }
            if (op1.Register.GetRegisterKind() != RegisterKind.GPR ||
                op2.Register.GetRegisterKind() != RegisterKind.GPR)
            {
                return false;
            }
            if (op1.Register.GetRegisterId() != op2.Register.GetRegisterId())
            {
                throw new NotSupportedException("Unexpected hidden scaling condition");
            }
            hasHiddenScaling = true;
        }
        return hasHiddenScaling;
    }

    private bool IsSignatureCompatible(EncodableDefinition definition)
    {
        // TODO: Rework whole ruleset
        if (FluentComparer.Compare(this, definition,
            x => x.Compare(x => x.Instruction.Mnemonic),
            x => x.CompareSequence(x => x.GetVisibleOperands()),
            x => x.Compare(x => AddressSizes),
            x => x.Compare(x => x.EvexNd),
            x => x.Compare(x => x.EvexNf),
            x => x.Compare(x => x.VectorLength),
            x => x.Compare(x => x.Instruction.MetaInfo),
            x => x.Compare(x => x.Instruction.Vex),
            x => x.Compare(x => x.Instruction.Evex),
            x => x.Compare(x => x.Instruction.Mvex),
            x => x.Compare(x => x.Instruction.OpsizeMap),
            x => x.Compare(x => x.Instruction.AdsizeMap)) != 0)
        {
            return false;
        }
        if (Rex2 != definition.Rex2 &&
            !(Rex2 == Rex2Type.Forbidden && definition.Rex2 == Rex2Type.Mandatory) &&
            !(Rex2 == Rex2Type.Mandatory && definition.Rex2 == Rex2Type.Forbidden) &&
            !(Rex2 == Rex2Type.AlwaysAllowed && definition.Rex2 == Rex2Type.Mandatory) &&
            !(Rex2 == Rex2Type.AlwaysAllowed && definition.Rex2 == Rex2Type.Forbidden))
        {
            return false;
        }
        if (Instruction.Comment == definition.Instruction.Comment &&
            FluentComparer.Compare(this, definition, x => x.CompareSequence(x => x.GetHiddenOperands())) == 0)
        {
            return true;
        }
        if (Modrm != definition.Modrm)
        {
            return false;
        }
        if (GetBoolFilter(SelectorDefinitions.PrefixGroup1) != definition.GetBoolFilter(SelectorDefinitions.PrefixGroup1))
        {
            return false;
        }
        if (IsApxOszScaling() && definition.IsApxOszScaling())
        {
            ApxOsz = true;
            return true;
        }
        var repeatPrefix = GetRepeatPrefix();
        if (repeatPrefix != definition.GetRepeatPrefix() ||
            (repeatPrefix == MandatoryPrefix.Ignore &&
            GetFilter(SelectorDefinitions.RexW) == definition.GetFilter(SelectorDefinitions.RexW)))
        {
            return true;
        }
        var myfilters = GetSelectors(SelectorDefinitions.RexW.Name);
        var otherFilters = definition.GetSelectors(SelectorDefinitions.RexW.Name);
        if (myfilters.GetValueOrDefault("operand_size", "") == "64" && otherFilters.GetValueOrDefault("operand_size", "") == "!64")
        {
            myfilters.Remove(SelectorDefinitions.OperandSize.Name);
            otherFilters.Remove(SelectorDefinitions.OperandSize.Name);
            _forcedSizeHint = true;
        }
        if (myfilters.Compare(otherFilters))
        {
            if (CompareOperandsForHiddenScaling(GetHiddenOperands(), definition.GetHiddenOperands()))
            {
                _forcedSizeHint = true;
            }
            return true;
        }
        return false;
    }

    public int CompareTo(EncodableDefinition? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Instruction.Mnemonic),
            x => x.Compare(x => !x.IsEncodable),
            x => x.Compare(x => x.MinSize),
            x => x.Compare(x => x.MaxSize),
            x => x.Compare(x => x.SwappableIndex),
            x => x.Compare(x => x.Instruction.GetBranchType())
        );
    }

    public override string ToString()
    {
        var operands = string.Join(", ", Instruction.Operands?.Select(x => x.Type.ToString().ToLower()) ?? []);
        if (operands.Length != 0)
        {
            operands = " " + operands;
        }
        var encoding = Instruction.Encoding.ToString("G").Replace("Default", "LEGACY");
        return $"{encoding}.MIN{MinSize:X2}.MAX{MaxSize:X2}.{Instruction.Opcode:X2}.{Instruction.Mnemonic}{operands}";
    }
}

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Corpus;

/// <summary>
/// The effective operand and address size of an instruction, by the rule of <c>ZydisSetEffectiveOperandWidth</c> and
/// <c>ZydisSetEffectiveAddressWidth</c> in Zydis <c>src/Decoder.c</c>.
/// </summary>
public static class EffectiveSizes
{
    // Rows in OperandSizeMap order, columns 16 __, 16 66, 32 __, 32 66, 64 __ W0, 64 66 W0, 64 __ W1, 64 66 W1.
    private static readonly int[][] OperandSizeTable =
    [
        [16, 32, 32, 16, 32, 16, 64, 64], // Default
        [16, 32, 32, 16, 32, 16, 64, 64], // ByteOperation, before the width is forced to 8
        [16, 16, 32, 32, 32, 32, 64, 64], // Ignore66
        [16, 32, 32, 16, 32, 16, 32, 32], // RexW32
        [16, 32, 32, 16, 64, 16, 64, 64], // Default64
        [16, 32, 32, 16, 64, 64, 64, 64], // Force64
        [32, 32, 32, 32, 32, 32, 64, 64], // Force32OrRexW
        [32, 32, 32, 32, 64, 64, 64, 64]  // Force32Or64
    ];

    // Rows in AddressSizeMap order, columns 16 __, 16 67, 32 __, 32 67, 64 __, 64 67.
    private static readonly int[][] AddressSizeTable =
    [
        [16, 32, 32, 16, 64, 32], // Default
        [16, 16, 32, 32, 64, 64], // Ignored
        [32, 32, 32, 32, 64, 64]  // Force32Or64
    ];

    /// <summary>
    /// Computes the effective operand and address size and the operand width of the variable-size immediates.
    /// </summary>
    /// <param name="mode">The machine mode.</param>
    /// <param name="operandSizePrefix">
    /// <see langword="true"/> if a <c>66</c> prefix acts as operand-size override, which excludes a <c>66</c> the
    /// definition consumes as its mandatory prefix.
    /// </param>
    /// <param name="addressSizePrefix"><see langword="true"/> if a <c>67</c> prefix is present.</param>
    /// <param name="rexW">The W bit of the REX, REX2, VEX, XOP, EVEX or MVEX prefix.</param>
    /// <param name="opsizeMap">The operand size map of the definition.</param>
    /// <param name="adsizeMap">The address size map of the definition.</param>
    /// <returns>
    /// The operand size, <c>8</c> for byte operations, the address size, and the operand width that selects the size
    /// of variable-size immediates. A byte operation keeps the width of the default map there, as the decoder derives
    /// its immediate size index before it forces the width to 8.
    /// </returns>
    public static (int Osz, int Asz, int OperandWidth) Compute(ModeNode.Slot mode, bool operandSizePrefix,
        bool addressSizePrefix, bool rexW, OperandSizeMap opsizeMap, AddressSizeMap adsizeMap)
    {
        var operandWidth = ComputeOperandWidth(mode, operandSizePrefix, rexW, opsizeMap);
        var osz = (opsizeMap is OperandSizeMap.ByteOperation) ? 8 : operandWidth;

        var asz = AddressSizeTable[(int)adsizeMap][((int)mode * 2) + (addressSizePrefix ? 1 : 0)];

        return (osz, asz, operandWidth);
    }

    private static int ComputeOperandWidth(ModeNode.Slot mode, bool operandSizePrefix, bool rexW,
        OperandSizeMap opsizeMap)
    {
        var index = ((int)mode * 2) + (operandSizePrefix ? 1 : 0);
        if ((mode is ModeNode.Slot.M64) && rexW)
        {
            index += 2;
        }

        return OperandSizeTable[(int)opsizeMap][index];
    }
}

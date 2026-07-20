using System.Collections.Frozen;
using System.Collections.Generic;

using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.DecoderTree.Builder;

/// <summary>
/// The fixed pre-variable-position per-encoding filter order. The frozen reference model inserts a definition's filters
/// in this order, and the variable-position builder reuses it as the tie-break priority between equal-cost candidates,
/// so both resolve otherwise ambiguous orderings the same way.
/// </summary>
internal static class FixedFilterOrder
{
    public static IReadOnlyDictionary<InstructionEncoding, string[]> ByEncoding { get; } =
        new Dictionary<InstructionEncoding, string[]>
        {
            [InstructionEncoding.Default] =
            [
                "rex_2",
                "feature_mpx",
                "feature_ud0_compat",
                "modrm_mod",
                "feature_cldemote",
                "prefix_group1",
                "mandatory_prefix",
                "modrm_reg",
                "modrm_rm",
                "mode",
                "address_size",
                "operand_size",
                "rex_w",
                "rex_b",
                "feature_amd",
                "feature_knc",
                "feature_cet",
                "feature_lzcnt",
                "feature_tzcnt",
                "feature_wbnoinvd",
                "feature_centaur",
                "feature_iprefetch"
            ],
            [InstructionEncoding.AMD3DNOW] =
            [
                "rex2",
                "modrm_mod",
                "mode_cldemote",
                "prefix_group1",
                "mandatory_prefix",
                "modrm_reg",
                "modrm_rm",
                "mode",
                "address_size",
                "operand_size",
                "rex_w",
                "rex_b",
            ],
            [InstructionEncoding.VEX] =
            [
                "modrm_reg",
                "modrm_rm",
                "vector_length",
                "mode",
                "modrm_mod",
                "rex_w",
                "operand_size",
                "address_size",
                "feature_knc"
            ],
            [InstructionEncoding.EVEX] =
            [
                "modrm_mod",
                "evex_u",
                "modrm_reg",
                "modrm_rm",
                "rex_w",
                "mode",
                "operand_size",
                "address_size",
                "evex_b",
                "vector_length",
                "evex_nd",
                "evex_nf",
                "evex_scc"
            ],
            [InstructionEncoding.MVEX] =
            [
                "modrm_mod",
                "modrm_reg",
                "modrm_rm",
                "rex_w",
                "mode",
                "operand_size",
                "address_size",
                "mvex_e",
                "vector_length"
            ],
            [InstructionEncoding.XOP] =
            [
                "modrm_reg",
                "modrm_rm",
                "vector_length",
                "mode",
                "modrm_mod",
                "rex_w",
                "operand_size",
                "address_size"
            ]
        }.ToFrozenDictionary();
}

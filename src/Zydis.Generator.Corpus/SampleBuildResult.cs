using System.Collections.Generic;

namespace Zydis.Generator.Corpus;

/// <summary>
/// The outcome of building the sample of one decoder-tree leaf in one machine mode.
/// </summary>
internal abstract record SampleBuildResult;

/// <summary>
/// The leaf decodes from <paramref name="Bytes"/> in the requested machine mode.
/// </summary>
/// <param name="Bytes">The instruction bytes.</param>
/// <param name="DecoderModes">The decoder modes the sample depends on.</param>
/// <param name="Osz">The effective operand size, <c>8</c> for byte operations.</param>
/// <param name="Asz">The effective address size.</param>
internal sealed record Built(IReadOnlyCollection<byte> Bytes, SampleBuilder.DecoderModes DecoderModes, int Osz, int Asz)
    : SampleBuildResult;

/// <summary>
/// No instruction can reach the leaf in the requested machine mode, for example a REX.W slot outside 64-bit mode.
/// </summary>
/// <param name="Reason">The path step that rules the machine mode out.</param>
internal sealed record Impossible(string Reason) : SampleBuildResult;

/// <summary>
/// The walk could not build bytes for the leaf, which leaves the corpus incomplete.
/// </summary>
/// <param name="Reason">The path steps or operands the builder could not encode.</param>
internal sealed record Unencodable(string Reason) : SampleBuildResult;

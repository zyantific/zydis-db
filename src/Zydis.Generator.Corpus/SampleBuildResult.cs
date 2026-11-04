namespace Zydis.Generator.Corpus;

/// <summary>
/// The outcome of building the sample of one decoder-tree leaf in one machine mode.
/// </summary>
public abstract record SampleBuildResult;

/// <summary>
/// The leaf decodes from <paramref name="Sample"/> in the requested machine mode.
/// </summary>
/// <param name="Sample">The sample.</param>
public sealed record Built(CorpusSample Sample) : SampleBuildResult;

/// <summary>
/// No instruction can reach the leaf in the requested machine mode, for example a REX.W slot outside 64-bit mode.
/// </summary>
/// <param name="Reason">The path step that rules the machine mode out.</param>
public sealed record Impossible(string Reason) : SampleBuildResult;

/// <summary>
/// The walk could not build bytes for the leaf, which leaves the corpus incomplete.
/// </summary>
/// <param name="Reason">The path steps or operands the builder could not encode.</param>
public sealed record Unencodable(string Reason) : SampleBuildResult;

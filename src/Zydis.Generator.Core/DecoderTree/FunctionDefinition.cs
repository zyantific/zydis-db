using System.Collections.Generic;

namespace Zydis.Generator.Core.DecoderTree;

public sealed record FunctionDefinition
{
    /// <summary>
    /// The function type name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A list that contains the name of each required parameter.
    /// </summary>
    public required IReadOnlyList<string> Parameters { get; init; }

    /// <summary>
    /// The number of parameters for this function.
    /// </summary>
    public int NumberOfParameters => Parameters.Count;

    /// <summary>
    /// A human-readable description of the function.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"{Name}({string.Join(',', Parameters)})";
    }
}

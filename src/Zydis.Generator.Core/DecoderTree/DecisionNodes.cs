using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Zydis.Generator.Core.DecoderTree;

internal static partial class DecisionNodes
{
    [GeneratedRegex(@"^(?<type>[a-z,A-Z,0-9,_]+)(?:\[(?<arguments>[a-z,A-Z,0-9,_,\,]+)\])*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectorTypeRegex();

    /// <summary>
    /// Parses a selector type expression and extracts the corresponding selector definition and arguments.
    /// </summary>
    /// <param name="typeExpression">The selector type expression to parse.</param>
    /// <returns>
    /// A tuple containing the parsed <see cref="DecisionNodeDefinition"/> and an array of arguments.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the input expression is not in a valid selector type format or if the selector type is unknown.
    /// </exception>
    public static (DecisionNodeDefinition Definition, string[] Arguments) ParseDecisionNodeType(string typeExpression)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeExpression);

        var match = SelectorTypeRegex().Match(typeExpression);
        if (!match.Success)
        {
            throw new NotSupportedException($"Invalid selector type '{typeExpression}'.");
        }

        var name = match.Groups["type"].Value;

        var definition = ByName.GetValueOrDefault(name);
        if (definition is null)
        {
            throw new NotSupportedException($"Unknown selector type '{name}'.");
        }

        var arguments = Array.Empty<string>();
        if (match.Groups["arguments"].Success)
        {
            arguments = match.Groups["arguments"].Value.Split(',');
        }

        // TODO:
        //if (arguments.Length != definition.NumberOfParameters)
        //{
        //    throw new NotSupportedException(
        //        $"Invalid number of arguments for selector type '{definition}'. " +
        //        $"Expected {definition.NumberOfParameters}, got {arguments.Length}.");
        //}

        return (definition, arguments);
    }
}

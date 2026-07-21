namespace Zydis.Generator.Core.Definitions;

/// <summary>
/// One decoder filter test of an instruction definition. Entry order in <see
/// cref="InstructionDefinition.Pattern"/> is the order the decoder tree tests the filters in, which is why the
/// JSON schema is an ordered array rather than an object (RFC 8259 leaves object member order insignificant).
/// </summary>
public sealed record FilterEntry(string Filter, string Value);

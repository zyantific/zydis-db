using Zydis.Generator.Core.Common;
using Zydis.Generator.Enums;

namespace Zydis.Viewer.Core.Tree;

/// <summary>
/// The values that select an opcode table of the generator.
/// </summary>
/// <param name="Encoding">The instruction encoding of the table.</param>
/// <param name="Map">The opcode map of the table.</param>
/// <param name="Prefix">
/// The refining prefix of the table, or <see langword="null"/> for the <see cref="InstructionEncoding.Default"/>
/// encoding.
/// </param>
public sealed record TableInfo(InstructionEncoding Encoding, OpcodeMap Map, RefiningPrefix? Prefix);

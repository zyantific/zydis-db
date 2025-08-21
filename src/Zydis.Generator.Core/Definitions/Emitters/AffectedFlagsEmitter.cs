using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Zydis.Generator.Core.CodeGeneration;
using Zydis.Generator.Core.Definitions.Builder;

namespace Zydis.Generator.Core.Definitions.Emitters;

internal sealed class AffectedFlagsEmitter
{
    public static async Task EmitAsync(StreamWriter writer, AccessedFlagsRegistry accessedFlagsRegistry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(accessedFlagsRegistry);

        // TODO: Emit masks of ZYDIS_CPUFLAG_CF, ...

        await writer.WriteLineAsync("#ifndef ZYDIS_MINIMAL_MODE").ConfigureAwait(false);

        var declarationWriter = DeclarationWriter.Create(writer);
        var flagsWriter = declarationWriter
            .BeginDeclaration("static const", "ZydisDefinitionAccessedFlags", "ACCESSED_FLAGS[]")
            .WriteInitializerList()
            .BeginList();
        var definitionAccessedFlagsDeclaration = new ObjectDeclaration<DefinitionAccessedFlags>();
        var accessedFlagsDeclaration = new ObjectDeclaration<AccessedFlags>();

        foreach (var accessedFlags in accessedFlagsRegistry.AccessedFlags)
        {
            var flagsEntry = flagsWriter.CreateObjectWriter(definitionAccessedFlagsDeclaration);
            var cpu = flagsEntry.CreateObjectWriter(accessedFlagsDeclaration)
                .WriteInteger("tested", accessedFlags.CpuFlags.Tested, 8, true)
                .WriteInteger("modified", accessedFlags.CpuFlags.Modified, 8, true)
                .WriteInteger("set_0", accessedFlags.CpuFlags.Set0, 8, true)
                .WriteInteger("set_1", accessedFlags.CpuFlags.Set1, 8, true)
                .WriteInteger("undefined", accessedFlags.CpuFlags.Undefined, 8, true);
            var fpu = flagsEntry.CreateObjectWriter(accessedFlagsDeclaration)
                .WriteInteger("tested", accessedFlags.FpuFlags.Tested, 8, true)
                .WriteInteger("modified", accessedFlags.FpuFlags.Modified, 8, true)
                .WriteInteger("set_0", accessedFlags.FpuFlags.Set0, 8, true)
                .WriteInteger("set_1", accessedFlags.FpuFlags.Set1, 8, true)
                .WriteInteger("undefined", accessedFlags.FpuFlags.Undefined, 8, true);
            flagsEntry
                .WriteObject("cpu_flags", cpu)
                .WriteObject("fpu_flags", fpu);
            flagsWriter.WriteObject(flagsEntry);
        }

        flagsWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("#endif").ConfigureAwait(false);
    }
}

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

        foreach (var accessedFlags in accessedFlagsRegistry.AccessedFlags)
        {
            var ilw = flagsWriter.WriteInitializerList(indent: Debugger.IsAttached).BeginList();
            var cpu = ilw.WriteFieldDesignation("cpu_flags").WriteInitializerList().BeginList();
            cpu.WriteFieldDesignation("tested").WriteInteger(accessedFlags.CpuFlags.Tested, 8, true);
            cpu.WriteFieldDesignation("modified").WriteInteger(accessedFlags.CpuFlags.Modified, 8, true);
            cpu.WriteFieldDesignation("set_0").WriteInteger(accessedFlags.CpuFlags.Set0, 8, true);
            cpu.WriteFieldDesignation("set_1").WriteInteger(accessedFlags.CpuFlags.Set1, 8, true);
            cpu.WriteFieldDesignation("undefined").WriteInteger(accessedFlags.CpuFlags.Undefined, 8, true);
            cpu.EndList();
            var fpu = ilw.WriteFieldDesignation("fpu_flags").WriteInitializerList().BeginList();
            fpu.WriteFieldDesignation("tested").WriteInteger(accessedFlags.FpuFlags.Tested, 8, true);
            fpu.WriteFieldDesignation("modified").WriteInteger(accessedFlags.FpuFlags.Modified, 8, true);
            fpu.WriteFieldDesignation("set_0").WriteInteger(accessedFlags.FpuFlags.Set0, 8, true);
            fpu.WriteFieldDesignation("set_1").WriteInteger(accessedFlags.FpuFlags.Set1, 8, true);
            fpu.WriteFieldDesignation("undefined").WriteInteger(accessedFlags.FpuFlags.Undefined, 8, true);
            fpu.EndList();
            ilw.EndList();
        }

        flagsWriter.EndList();
        declarationWriter.EndDeclaration();

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("#endif").ConfigureAwait(false);
    }
}

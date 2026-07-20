using System;
using System.Collections.Generic;
using System.Linq;

using Zydis.Generator.Core.DecoderTree.Builder;

namespace Zydis.Generator.Core.DecoderTree.Emitters;

/// <summary>
/// Measures the emitted size of every non-empty opcode table without producing any output. The layout pass is shared
/// with the real code emitter, so the reported sizes match what generation would write; this lets verify mode compare
/// two trees without touching the output directory.
/// </summary>
public static class DecoderTableEmissionMeasurer
{
    /// <summary>
    /// Lays out every non-empty table in <paramref name="tables"/> and returns its per-table emission measurements.
    /// </summary>
    public static IReadOnlyList<TableEmissionStatistics> Measure(OpcodeTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var result = new List<TableEmissionStatistics>();

        foreach (var table in tables.Tables)
        {
            if (table.EnumerateSlots().All(slot => slot is null))
            {
                continue; // Skip empty tables, matching what generation writes.
            }

            var emitter = new MeasuringEmitter();
            var size = emitter.Emit(table);

            result.Add(new TableEmissionStatistics(table.ToString()!, size, emitter.EmittedNodeCount, emitter.CloneCount));
        }

        return result;
    }

    private sealed class MeasuringEmitter :
        OpcodeTableEmitter
    {
        protected override void EmitDecisionNode(
            DecisionNode node,
            IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> targets)
        {
            // Draining the sequence drives the same edge resolution as a real emit, so the layout it depends on is
            // exercised identically; nothing is written.
            foreach (var _ in targets)
            {
            }
        }

        protected override void EmitDefinitionNode(DefinitionNode node)
        {
        }

        protected override void EmitOpcodeTableSwitchNode(OpcodeTableSwitchNode node)
        {
        }
    }
}

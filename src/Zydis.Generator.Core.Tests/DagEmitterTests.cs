using System.Collections.Generic;
using System.Linq;

using Xunit;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Emitters;
using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.Tests;

public class DagEmitterTests
{
    [Fact]
    public void SharedChild_ReferencedByTwoParents_IsEmittedOnceWithBothEdgesResolvingToSameAddress()
    {
        var shared = new DefinitionNode(CreateDefinition("SHARED"));

        var left = new ModrmModCompactNode();
        left[ModrmModCompactNode.Slot.Register] = shared;

        var right = new ModrmModCompactNode();
        right[ModrmModCompactNode.Slot.Register] = shared;

        var root = new ModrmModCompactNode();
        root[ModrmModCompactNode.Slot.Register] = left;
        root[ModrmModCompactNode.Slot.Memory] = right;

        var emitter = new RecordingEmitter();
        emitter.Emit(root);

        var sharedEmissions = emitter.Nodes.Where(x => ReferenceEquals(x.Node, shared)).ToArray();
        Assert.Single(sharedEmissions);

        var sharedAddress = sharedEmissions[0].Address;

        var leftEdge = EdgeTo(emitter, left, shared);
        var rightEdge = EdgeTo(emitter, right, shared);

        Assert.Equal(sharedAddress, leftEdge.TargetAddress);
        Assert.Equal(sharedAddress, rightEdge.TargetAddress);
        Assert.Equal(0, emitter.CloneCount);
    }

    [Fact]
    public void DiamondDag_EveryEdge_PointsForwardToAHigherAddress()
    {
        var shared = new DefinitionNode(CreateDefinition("SHARED"));

        var left = new ModrmModCompactNode();
        left[ModrmModCompactNode.Slot.Register] = shared;

        var right = new ModrmModCompactNode();
        right[ModrmModCompactNode.Slot.Register] = shared;

        var root = new ModrmModCompactNode();
        root[ModrmModCompactNode.Slot.Register] = left;
        root[ModrmModCompactNode.Slot.Memory] = right;

        var emitter = new RecordingEmitter();
        emitter.Emit(root);

        foreach (var emission in emitter.Nodes)
        {
            foreach (var edge in emission.Edges)
            {
                if (edge.Target is null)
                {
                    continue;
                }

                Assert.True(edge.Offset >= 1, "offsets must be forward-only");
                Assert.True(edge.TargetAddress > emission.Address, "child address must exceed parent address");
            }
        }
    }

    [Fact]
    public void SharedChildReachableAtDifferentDepths_ClonesTheFarReference_AndConvergesWithinTheOffsetLimit()
    {
        // The shared definition is discovered via the shallow reference (root) and pulled to a low address,
        // which leaves the deeper reference (through `mid`) pointing backwards. That edge is not encodable, so
        // the emitter must clone the subtree for the far parent until every edge fits the given offset limit.
        const int maximumOffset = 5;

        var shared = new DefinitionNode(CreateDefinition("SHARED"));

        var mid = new ModrmModCompactNode();
        mid[ModrmModCompactNode.Slot.Register] = shared;

        var root = new ModrmModCompactNode();
        root[ModrmModCompactNode.Slot.Register] = shared;
        root[ModrmModCompactNode.Slot.Memory] = mid;

        var emitter = new RecordingEmitter(maximumOffset);
        emitter.Emit(root);

        Assert.Equal(1, emitter.CloneCount);

        // The original definition plus its private clone are both emitted.
        Assert.Equal(2, emitter.Nodes.Count(x => x.Node is DefinitionNode));

        foreach (var emission in emitter.Nodes)
        {
            foreach (var edge in emission.Edges)
            {
                if (edge.Target is null)
                {
                    continue;
                }

                Assert.InRange(edge.Offset, 1, maximumOffset);
            }
        }
    }

    private static Edge EdgeTo(RecordingEmitter emitter, DecoderTreeNode parent, DecoderTreeNode target)
    {
        var emission = emitter.Nodes.Single(x => ReferenceEquals(x.Node, parent));
        return emission.Edges.Single(x => ReferenceEquals(x.Target, target));
    }

    private static InstructionDefinition CreateDefinition(string mnemonic)
    {
        return new InstructionDefinition
        {
            Mnemonic = mnemonic,
            Opcode = 0x00,
            MetaInfo = new InstructionMetaInfo()
        };
    }

    private readonly record struct Edge(int Index, DecoderTreeNode? Target, int TargetAddress, int Offset);

    private sealed record Emission(DecoderTreeNode Node, int Address, IReadOnlyList<Edge> Edges);

    private sealed class RecordingEmitter : OpcodeTableEmitter
    {
        public List<Emission> Nodes { get; } = [];

        public RecordingEmitter()
        {
        }

        public RecordingEmitter(int maximumOffset) :
            base(maximumOffset)
        {
        }

        protected override void EmitDecisionNode(DecisionNode node,
            IEnumerable<(int Index, DecoderTreeNode? TargetNode, int TargetAddress, int OffsetToTarget)> targets)
        {
            var edges = targets
                .Select(x => new Edge(x.Index, x.TargetNode, x.TargetAddress, x.OffsetToTarget))
                .ToArray();

            Nodes.Add(new Emission(node, CurrentAddress, edges));
        }

        protected override void EmitDefinitionNode(DefinitionNode node)
        {
            Nodes.Add(new Emission(node, CurrentAddress, []));
        }

        protected override void EmitOpcodeTableSwitchNode(OpcodeTableSwitchNode node)
        {
            Nodes.Add(new Emission(node, CurrentAddress, []));
        }
    }
}

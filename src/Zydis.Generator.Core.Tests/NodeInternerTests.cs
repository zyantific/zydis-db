using System;

using Xunit;

using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Core.DecoderTree.Builder;
using Zydis.Generator.Core.Definitions;

namespace Zydis.Generator.Core.Tests;

public class NodeInternerTests
{
    [Fact]
    public void Intern_TwoStructurallyIdenticalDecisionNodes_ReturnSameInstance()
    {
        var interner = new NodeInterner();
        var definition = CreateDefinition("AAA");

        var childA = interner.Intern(new DefinitionNode(definition));
        var childB = interner.Intern(new DefinitionNode(definition));

        var nodeA = new ModrmRegNode();
        nodeA[ModrmRegNode.Slot.R0] = childA;

        var nodeB = new ModrmRegNode();
        nodeB[ModrmRegNode.Slot.R0] = childB;

        var canonicalA = interner.Intern(nodeA);
        var canonicalB = interner.Intern(nodeB);

        Assert.Same(canonicalA, canonicalB);
        Assert.Same(nodeA, canonicalA);
        Assert.Equal(interner.GetId(canonicalA), interner.GetId(canonicalB));
    }

    [Fact]
    public void Intern_DecisionNodesDifferingInOneChild_ReturnDifferentInstances()
    {
        var interner = new NodeInterner();

        var childX = interner.Intern(new DefinitionNode(CreateDefinition("XXX")));
        var childY = interner.Intern(new DefinitionNode(CreateDefinition("YYY")));

        var nodeA = new ModrmRegNode();
        nodeA[ModrmRegNode.Slot.R0] = childX;

        var nodeB = new ModrmRegNode();
        nodeB[ModrmRegNode.Slot.R0] = childY;

        var canonicalA = interner.Intern(nodeA);
        var canonicalB = interner.Intern(nodeB);

        Assert.NotSame(canonicalA, canonicalB);
        Assert.NotEqual(interner.GetId(canonicalA), interner.GetId(canonicalB));
    }

    [Fact]
    public void Intern_ElseEntryParticipatesInIdentity()
    {
        var interner = new NodeInterner();

        var elseChildA = interner.Intern(new DefinitionNode(CreateDefinition("AAA")));
        var elseChildB = interner.Intern(new DefinitionNode(CreateDefinition("BBB")));

        var nodeA = new ModrmRegNode { ElseEntry = elseChildA };
        var nodeB = new ModrmRegNode { ElseEntry = elseChildB };

        var canonicalA = interner.Intern(nodeA);
        var canonicalB = interner.Intern(nodeB);

        Assert.NotSame(canonicalA, canonicalB);
    }

    [Fact]
    public void Intern_DefinitionNodesForSameInstructionDefinition_ReturnSameInstance()
    {
        var interner = new NodeInterner();
        var definition = CreateDefinition("AAA");

        var canonicalA = interner.Intern(new DefinitionNode(definition));
        var canonicalB = interner.Intern(new DefinitionNode(definition));

        Assert.Same(canonicalA, canonicalB);
        Assert.Equal(1, interner.Count);
    }

    [Fact]
    public void Intern_DefinitionNodesForValueEqualButDistinctInstructionDefinitions_ReturnDifferentInstances()
    {
        // InstructionDefinition is a record; interning must key on reference identity, not structural equality,
        // otherwise two distinct instructions that happen to share every field would collapse into one.
        var definitionA = CreateDefinition("AAA");
        var definitionB = CreateDefinition("AAA");

        Assert.Equal(definitionA, definitionB);
        Assert.NotSame(definitionA, definitionB);

        var interner = new NodeInterner();

        var canonicalA = interner.Intern(new DefinitionNode(definitionA));
        var canonicalB = interner.Intern(new DefinitionNode(definitionB));

        Assert.NotSame(canonicalA, canonicalB);
    }

    [Fact]
    public void Intern_AssignsStableDenseIdsInInternOrder()
    {
        var interner = new NodeInterner();
        var definitionA = CreateDefinition("AAA");
        var definitionB = CreateDefinition("BBB");

        var nodeA = interner.Intern(new DefinitionNode(definitionA));
        var nodeB = interner.Intern(new DefinitionNode(definitionB));
        var nodeARepeat = interner.Intern(new DefinitionNode(definitionA));

        Assert.Equal(0, interner.GetId(nodeA));
        Assert.Equal(1, interner.GetId(nodeB));
        Assert.Equal(0, interner.GetId(nodeARepeat));
        Assert.Equal(2, interner.Count);
    }

    [Fact]
    public void Intern_SameCanonicalInstanceTwice_ReturnsItUnchanged()
    {
        var interner = new NodeInterner();
        var node = new DefinitionNode(CreateDefinition("AAA"));

        var canonical = interner.Intern(node);
        var again = interner.Intern(canonical);

        Assert.Same(canonical, again);
        Assert.Equal(1, interner.Count);
    }

    [Fact]
    public void GetId_NodeNeverInterned_Throws()
    {
        var interner = new NodeInterner();
        var node = new DefinitionNode(CreateDefinition("AAA"));

        Assert.Throws<InvalidOperationException>(() => interner.GetId(node));
    }

    [Fact]
    public void Intern_ChildNotYetInterned_Throws()
    {
        // Bottom-up discipline: a decision node's children must be interned before the node itself.
        var interner = new NodeInterner();

        var node = new ModrmRegNode();
        node[ModrmRegNode.Slot.R0] = new DefinitionNode(CreateDefinition("AAA"));

        Assert.Throws<InvalidOperationException>(() => interner.Intern(node));
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
}

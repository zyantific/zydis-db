using System;
using System.Linq;

using Xunit;

using Zydis.Generator.Core.DecoderTree;

namespace Zydis.Generator.Core.Tests;

public class DecisionNodeElseTests
{
    [Fact]
    public void EnumerateSlots_ElseEntry_FillsSlotsWithoutExplicitEntry()
    {
        var node = new ModeNode();
        var explicitEntry = new OverflowNode();
        var elseEntry = new OverflowNode();

        node[ModeNode.Slot.M16] = explicitEntry;
        node.ElseEntry = elseEntry;

        var slots = node.EnumerateSlots().ToArray();

        Assert.Same(explicitEntry, slots[(int)ModeNode.Slot.M16]);
        Assert.Same(elseEntry, slots[(int)ModeNode.Slot.M32]);
        Assert.Same(elseEntry, slots[(int)ModeNode.Slot.M64]);
    }

    [Fact]
    public void EnumerateSlots_ExplicitEntry_TakesPrecedenceOverElseEntry()
    {
        var node = new ModeNode();
        var explicit16 = new OverflowNode();
        var explicit32 = new OverflowNode();
        var explicit64 = new OverflowNode();
        var elseEntry = new OverflowNode();

        node[ModeNode.Slot.M16] = explicit16;
        node[ModeNode.Slot.M32] = explicit32;
        node[ModeNode.Slot.M64] = explicit64;
        node.ElseEntry = elseEntry;

        var slots = node.EnumerateSlots().ToArray();

        Assert.Same(explicit16, slots[(int)ModeNode.Slot.M16]);
        Assert.Same(explicit32, slots[(int)ModeNode.Slot.M32]);
        Assert.Same(explicit64, slots[(int)ModeNode.Slot.M64]);
        Assert.DoesNotContain(elseEntry, slots);
    }

    [Fact]
    public void EnumerateSlots_NegatedEntryAndElseEntry_Throws()
    {
        var node = new ModeNode();

        node[DecisionNodeIndex.ForNegatedIndex(ModeNode.Slot.M64)] = new OverflowNode();
        node.ElseEntry = new OverflowNode();

        Assert.Throws<InvalidOperationException>(() => node.EnumerateSlots().ToArray());
    }

    [Fact]
    public void EnumerateSlots_OnlyElseEntry_YieldsElseEntryForEverySlot()
    {
        var node = new ModeNode();
        var elseEntry = new OverflowNode();

        node.ElseEntry = elseEntry;

        var slots = node.EnumerateSlots().ToArray();

        Assert.Equal(3, slots.Length);
        Assert.All(slots, slot => Assert.Same(elseEntry, slot));
    }
}

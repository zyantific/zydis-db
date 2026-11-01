using System;

using Xunit;

using Zydis.Generator.Core.Common;
using Zydis.Generator.Core.DecoderTree;
using Zydis.Generator.Enums;

namespace Zydis.Generator.Core.Tests;

public class OpcodeTablesTests
{
    [Fact]
    public void GetTableId_VexMap4_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => OpcodeTables.GetTableId(InstructionEncoding.VEX, OpcodeMap.MAP4, RefiningPrefix.PNP));
    }

    [Fact]
    public void GetTable_VexMap5_ReturnsTheMap5Table()
    {
        var tables = new OpcodeTables();

        var table = tables.GetTable(InstructionEncoding.VEX, OpcodeMap.MAP5, RefiningPrefix.PNP);

        Assert.Equal("VEX_NP_MAP5", table.ToString());
    }
}

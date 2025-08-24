using System;

using Zydis.Generator.Core.Helpers;

namespace Zydis.Generator.Core.Definitions;

#pragma warning disable CA1036

public sealed record SizeTable(int Width16, int Width32, int Width64) :
    IComparable<SizeTable>,
    IComparable

#pragma warning restore CA1036
{
    public int CompareTo(SizeTable? other)
    {
        return FluentComparer.Compare(this, other,
            x => x.Compare(x => x.Width16),
            x => x.Compare(x => x.Width32),
            x => x.Compare(x => x.Width64)
        );
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as SizeTable);
    }
}

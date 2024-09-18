using System;
using System.Text.Json.Serialization;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.Definitions;

// ReSharper disable InconsistentNaming

[JsonConverter(typeof(StringFlagsConverterFactory<PrefixFlags>))]
[Flags]
public enum PrefixFlags
{
    None               = 0,
    AcceptsLOCK        = 1 <<  1,
    AcceptsREP         = 1 <<  2,
    AcceptsREPEREPZ    = 1 <<  3,
    AcceptsREPNEREPNZ  = 1 <<  4,
    AcceptsBOUND       = 1 <<  5,
    AcceptsXACQUIRE    = 1 <<  6,
    AcceptsXRELEASE    = 1 <<  7,
    AcceptsNOTRACK     = 1 <<  8,
    AcceptsLocklessHLE = 1 <<  9,
    AcceptsBranchHints = 1 << 10
}

// ReSharper restore InconsistentNaming

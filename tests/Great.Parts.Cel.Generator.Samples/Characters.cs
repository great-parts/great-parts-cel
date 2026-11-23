using System;
using System.Collections.Generic;
using System.Numerics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Characters;

// char and the integers wider than 64 bits as members, elements, values and keys. A char is a string of one UTF-16 unit and a lone
// surrogate is InvalidArgument at the read; Int128 and BigInteger are int, UInt128 is uint, and a value outside the range of the CEL
// type is ConversionRange at the read. The member names are the camel-case names of the default policy.

public sealed class Letters
{
    public char Letter { get; set; } = 'A';

    public char? Mark { get; set; }

    public IReadOnlyList<char> Row { get; set; } = [];

    public char[] Chars { get; set; } = [];

    public Dictionary<char, long> Counts { get; set; } = [];

    public HashSet<char> Marks { get; set; } = [];
}

public sealed class BigNumbers
{
    public Int128 I { get; set; }

    public UInt128 U { get; set; }

    public BigInteger B { get; set; }

    public Int128? N { get; set; }

    public IReadOnlyList<Int128> Row { get; set; } = [];

    public Dictionary<string, UInt128> Table { get; set; } = [];

    public IReadOnlyDictionary<string, Int128> Ledger { get; set; } = new Dictionary<string, Int128>();
}

[CelObject(typeof(Letters), Name = "samples.Letters")]
[CelObject(typeof(BigNumbers), Name = "samples.BigNumbers")]
public sealed partial class CharactersContext : CelTypeContext
{
}

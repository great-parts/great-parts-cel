using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Literals;

// Types that object literals create: a class with set and init members, a positional record with a default, a required init member, a
// required field, members of collections, of other types of the context, of the integers and times of the type mapping, a structure
// with a mutable field, and a constructor that throws. Money, Price, Order, Tagged, Basket, Ticket, Wide, Point, Pair and Boom allow
// literals; Order does not, and Locked has members no literal can set. The CEL names are acme.<name>.

public sealed class Money
{
    public decimal Amount { get; set; }

    public string Currency { get; init; } = "RUB";

    public int Scale { get; set; } = 2;

    public string Display => Currency + ":" + Scale;
}

public sealed record Price(decimal Amount, string Currency = "RUB");

// An input type that does not allow literals.
public sealed class Order
{
    public required string Id { get; init; }

    public Money Total { get; set; } = new();
}

// A positional record with members beyond its parameters: an init member with an initializer and a set member.
public sealed record Tagged(string Name)
{
    public int Weight { get; init; } = 5;

    public string Note { get; set; } = "n";
}

public sealed class Basket
{
    public string Owner { get; set; } = "";

    public Money? Main { get; set; }

    public List<Money> Items { get; set; } = [];

    public Money[] Lines { get; set; } = [];

    public ImmutableArray<Money> Frozen { get; set; } = [];

    public Dictionary<string, Money> ByName { get; set; } = [];

    public IReadOnlyList<long> Numbers { get; set; } = [];

    public IReadOnlyDictionary<string, long> Counts { get; set; } = new Dictionary<string, long>();
}

// A required init member and a required field: every literal must set them. The field is public on purpose: a literal sets a mutable
// field as well as a property.
public sealed class Ticket
{
    public required string Id { get; init; }

    public required int Seat;

    public string Note { get; init; } = "none";
}

public sealed class Wide
{
    public char Letter { get; set; }

    public Int128 Big { get; set; }

    public UInt128 BigUnsigned { get; set; }

    public BigInteger Huge { get; set; }

    public DateOnly Day { get; set; }

    public TimeOnly At { get; set; }

    public Guid Key { get; set; }

    public byte Small { get; set; }

    public short Medium { get; set; }

    public uint Unsigned { get; set; }

    public float Ratio { get; set; }

    public DateTimeOffset When { get; set; }

    public TimeSpan Span { get; set; }

    public int? Maybe { get; set; }

    public byte[] Data { get; set; } = [];
}

// A structure: a mutable field, a set member and an init member.
public struct Point
{
    public long X;

    public long Y { get; set; }

    public long Z { get; init; }
}

// A class whose constructor has two parameters and a set member.
public sealed class Pair
{
    public Pair(string left, string right)
    {
        Left = left;
        Right = right;
    }

    public string Left { get; }

    public string Right { get; }

    public int Extra { get; set; }
}

public sealed class Boom
{
    public Boom()
    {
        throw new InvalidOperationException("boom");
    }

    public int Value { get; set; }
}

// Members that no literal can set: no set accessor, a private set, and a type without a creation row.
public sealed class Locked
{
    public string Name { get; set; } = "";

    public string ReadOnly { get; } = "r";

    public string PrivateSet { get; private set; } = "p";

    public object Anything { get; set; } = "";
}

[CelObject(typeof(Money), Name = "acme.Money", AllowObjectLiterals = true)]
[CelObject(typeof(Price), Name = "acme.Price", AllowObjectLiterals = true)]
[CelObject(typeof(Order), Name = "acme.Order")]
[CelObject(typeof(Tagged), Name = "acme.Tagged", AllowObjectLiterals = true)]
[CelObject(typeof(Basket), Name = "acme.Basket", AllowObjectLiterals = true)]
[CelObject(typeof(Ticket), Name = "acme.Ticket", AllowObjectLiterals = true)]
[CelObject(typeof(Wide), Name = "acme.Wide", AllowObjectLiterals = true)]
[CelObject(typeof(Point), Name = "acme.Point", AllowObjectLiterals = true)]
[CelObject(typeof(Pair), Name = "acme.Pair", AllowObjectLiterals = true)]
[CelObject(typeof(Boom), Name = "acme.Boom", AllowObjectLiterals = true)]
[CelObject(typeof(Locked), Name = "acme.Locked", AllowObjectLiterals = true)]
public sealed partial class ShopContext : CelTypeContext
{
}

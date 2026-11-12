using System.Collections.Generic;
using Google.Protobuf;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Kinds;

// The kinds of types that a context describes (the collection and object rows of the type mapping): a class, a record class, a structure, a record
// struct, an interface with a base interface, a closed generic type, a nested type and a type of another assembly. The type
// of another assembly is Google.Protobuf.ByteString: a class that is not a message and not of System, which implements only
// IEnumerable<byte> and is an object type read by its members; its property Span has a ref struct type and its indexer is an
// indexer, so neither is a member.

public sealed class ClassKind
{
    public long Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed record RecordKind(long Id, string Name);

public struct StructKind
{
    public long Id { get; set; }

    public string Name { get; set; }
}

public readonly record struct PointKind(long X, long Y);

public interface IEntity
{
    long Id { get; }
}

public interface IShape : IEntity
{
    string Label { get; }

    double Area { get; }
}

// A class that implements the interface and has a member the interface lacks: through the interface only Id, Label and Area.
public sealed class Circle : IShape
{
    public long Id { get; set; }

    public string Label { get; set; } = "";

    public double Area { get; set; }

    public double Radius { get; set; }
}

public sealed class Item
{
    public string Sku { get; set; } = "";
}

// The closed generic type Page<Item> has the default CEL name <namespace>.Page_<namespace of Item with _>_Item.
public sealed class Page<T>
{
    public IReadOnlyList<T> Rows { get; set; } = [];

    public long Total { get; set; }
}

public sealed class Outer
{
    // The nested type has the default CEL name <namespace>.Outer.Inner.
    public sealed class Inner
    {
        public long Depth { get; set; }
    }
}

public sealed class Holder
{
    public ClassKind Cls { get; set; } = new();

    public RecordKind Rec { get; set; } = new(0, "");

    public StructKind Str { get; set; }

    public PointKind Pnt { get; set; }

    public IShape Shape { get; set; } = new Circle();

    public Page<Item> Page { get; set; } = new();

    public Outer.Inner Nested { get; set; } = new();

    public ByteString Foreign { get; set; } = ByteString.Empty;

    public StructKind? OptStr { get; set; }

    public ClassKind? NoCls { get; set; }
}

[CelObject(typeof(Holder), Name = "samples.Holder")]
[CelObject(typeof(ByteString), Name = "samples.Bytes")]
public sealed partial class KindsContext : CelTypeContext
{
}

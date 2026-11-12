using System;
using System.Text.Json;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Rows;

// Every input row of the type mapping ("Input: a .NET type to a CEL type") as a member (the names are the camel-case names that the default policy gives).

public enum Status
{
    Draft = 0,
    Approved = 1,
}

[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
}

// A constant over long.MaxValue is not in the description of the enumeration; a member that holds it is ConversionRange.
public enum Wide : ulong
{
    Small = 1,
    Huge = 18446744073709551615UL,
}

public struct Point
{
    public long X { get; set; }

    public long Y { get; set; }
}

public sealed class Scalars
{
    public long Int64 { get; set; }

    public int Int32 { get; set; }

    public short Int16 { get; set; }

    public sbyte Sbyte { get; set; }

    public nint Nint { get; set; }

    public ulong Uint64 { get; set; }

    public uint Uint32 { get; set; }

    public ushort Uint16 { get; set; }

    public byte Byte { get; set; }

    public nuint Nuint { get; set; }

    public double Double { get; set; }

    public float Float { get; set; }

    public Half Half { get; set; }

    public bool Bool { get; set; }

    // string without ?: type string, null is UnexpectedNull; string?: wrapper(string), null is the null value.
    public string Text { get; set; } = "";

    public string? Note { get; set; }

    public Guid Id { get; set; }

    public Status Status { get; set; }

    public Permissions Permissions { get; set; }

    public Wide Wide { get; set; }

    public byte[] Blob { get; set; } = [];

    public byte[]? OptBlob { get; set; }

    public ReadOnlyMemory<byte> Memory { get; set; }

    public CelTimestamp Stamp { get; set; }

    public DateTimeOffset Moment { get; set; }

    // Kind other than Utc is InvalidArgument at the read.
    public DateTime Instant { get; set; }

    public CelDuration Lapse { get; set; }

    public TimeSpan Elapsed { get; set; }

    // Nullable<T> of a type that has a wrapper in CEL: wrapper type, null is the null value.
    public int? OptInt { get; set; }

    public Status? OptStatus { get; set; }

    public Guid? OptId { get; set; }

    // Nullable<T> of a type without a wrapper: the type of T, null is UnexpectedNull and has() is false.
    public DateTimeOffset? OptMoment { get; set; }

    public TimeSpan? OptElapsed { get; set; }

    // Nullable<T> of a structure of the context: the object type, null is the null value.
    public Point? OptPoint { get; set; }

    public object? Any { get; set; }

    public object Always { get; set; } = "";

    public CelValue Value { get; set; }

    public CelList? Items { get; set; }

    public CelMap? Table { get; set; }

    public JsonElement Json { get; set; }
}

// The project of the application may have the nullable context off: a reference type without an annotation is read as nullable.
#nullable disable // the sample of the project without the nullable context
public sealed class Oblivious
{
    public string Text { get; set; }

    public byte[] Blob { get; set; }

    public object Any { get; set; }

    public Scalars Inner { get; set; }
}
#nullable enable

[CelObject(typeof(Scalars), Name = "samples.Scalars")]
[CelObject(typeof(Oblivious), Name = "samples.Oblivious")]
public sealed partial class ScalarContext : CelTypeContext
{
}

#if NET11_0_OR_GREATER
// The two kinds of decimal members: the CEL type decimal, which needs the extension set decimal in the environment.
public sealed class DecimalValues
{
    public System.Numerics.Decimal128 Exact { get; set; }

    public decimal Money { get; set; }

    public decimal? OptMoney { get; set; }
}

[CelObject(typeof(DecimalValues), Name = "samples.DecimalValues")]
public sealed partial class DecimalContext : CelTypeContext
{
}
#endif

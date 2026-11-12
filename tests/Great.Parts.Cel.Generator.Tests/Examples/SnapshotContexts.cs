using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples;

// The contexts that the snapshots of the created code are made for (the specification of the tests, "Snapshots"): the smallest
// context of the page of the created code, every input row of the type mapping, the collection families, the kinds of object
// types, the naming policies and the name rules, the excluded members, a member that is a message of protobuf, Decimal128 and
// the project without the nullable context. The developer writes the files of the snapshots from the created text, each after
// a read of it against the page of the created code; the sources are not changed. A context that is not compiled holds a stub
// of what the process lacks (Decimal128), and its created text is a snapshot only.
internal sealed record SnapshotContext(
    string Name,
    string Source,
    NullableContextOptions Nullable = NullableContextOptions.Enable,
    string? Stub = null,
    string? Foreign = null,
    bool Compiled = true);

internal static class SnapshotContexts
{
    internal const string Smallest = """
        using System.Collections.Generic;
        using Great.Parts.Cel.Values;
        namespace Acme.Sales;
        [CelObject(typeof(Order))]
        public partial class OrderContext : CelTypeContext { }
        public enum Status { Draft, Approved }
        public class OrderLine { public string Sku { get; set; } = ""; public long Qty { get; set; } }
        public class Order
        {
            public string Currency { get; set; } = "";
            public List<OrderLine> Lines { get; set; } = new();
            public Status Status { get; set; }
        }
        """;

    private const string Header = "using System;\nusing System.Collections.Generic;\nusing System.Collections.Immutable;\nusing System.Text.Json;\nusing Great.Parts.Cel.Values;\nnamespace Acme;\n";

    internal static IReadOnlyList<SnapshotContext> All { get; } =
    [
        new("Smallest", Smallest),

        new("Scalars", Header + """
            [CelObject(typeof(Row))] public partial class RowContext : CelTypeContext { }
            public enum Color { Red, Green = 5 }
            [Flags] public enum Bits { None = 0, One = 1, Two = 2 }
            public enum Big : ulong { Small = 1, Huge = ulong.MaxValue }
            public struct Pt { public long X { get; set; } }
            public class Row
            {
                public long I64 { get; set; } public int I32 { get; set; } public short I16 { get; set; } public sbyte I8 { get; set; } public nint Native { get; set; }
                public ulong U64 { get; set; } public uint U32 { get; set; } public ushort U16 { get; set; } public byte U8 { get; set; } public nuint NativeU { get; set; }
                public double D { get; set; } public float F { get; set; } public Half H { get; set; } public bool B { get; set; }
                public string Text { get; set; } = ""; public string? Note { get; set; } public Guid Id { get; set; }
                public Color Color { get; set; } public Bits Bits { get; set; } public Big Big { get; set; }
                public byte[] Blob { get; set; } = []; public byte[]? OptBlob { get; set; } public ReadOnlyMemory<byte> Memory { get; set; }
                public CelTimestamp Stamp { get; set; } public DateTimeOffset Moment { get; set; } public DateTime Instant { get; set; }
                public CelDuration Lapse { get; set; } public TimeSpan Elapsed { get; set; }
                public int? OptInt { get; set; } public Color? OptColor { get; set; } public Guid? OptId { get; set; }
                public DateTimeOffset? OptMoment { get; set; } public TimeSpan? OptElapsed { get; set; } public Pt? OptPt { get; set; }
                public object? Any { get; set; } public object Must { get; set; } = ""; public CelValue Value { get; set; }
                public CelList? Items { get; set; } public CelMap? Table { get; set; } public JsonElement Json { get; set; }
            }
            """),

        new("Collections", Header + """
            [CelObject(typeof(Bag))] public partial class BagContext : CelTypeContext { }
            public enum Color { Red, Green }
            public class Line { public long Qty { get; set; } }
            public readonly struct Triple : IReadOnlyList<long>
            {
                public int Count => 3;
                public long this[int index] => index;
                public IEnumerator<long> GetEnumerator() { yield return 0; yield return 1; yield return 2; }
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            }
            public class Bag
            {
                public long[] Array { get; set; } = []; public IReadOnlyList<string> ReadOnly { get; set; } = []; public List<long> Mutable { get; set; } = [];
                public ImmutableArray<long> Immutable { get; set; } public ImmutableArray<long>? OptImmutable { get; set; } public Triple Fixed { get; set; } public IList<long> AsIList { get; set; } = new List<long>();
                public HashSet<long> Hash { get; set; } = []; public ISet<string> AsSet { get; set; } = new HashSet<string>(); public IReadOnlySet<long> ReadOnlySet { get; set; } = new HashSet<long>();
                public Queue<long> Queue { get; set; } = new(); public LinkedList<long> Linked { get; set; } = new();
                public Dictionary<string, long> Index { get; set; } = []; public IDictionary<string, long> AsIDictionary { get; set; } = new Dictionary<string, long>();
                public IReadOnlyDictionary<long, string> ReadOnlyIndex { get; set; } = new Dictionary<long, string>();
                public Dictionary<Guid, long> ByGuid { get; set; } = []; public Dictionary<Color, long> ByColor { get; set; } = [];
                public List<List<long>> Nested { get; set; } = []; public List<List<long>?> Holey { get; set; } = []; public Dictionary<string, List<Line>> Groups { get; set; } = [];
                public List<Line> Lines { get; set; } = []; public List<string?> Texts { get; set; } = []; public List<long?> Maybe { get; set; } = [];
                public List<long>? NoList { get; set; } public List<long> MustList { get; set; } = null!;
            }
            """),

        new("Kinds", Header + """
            [CelObject(typeof(Holder))] public partial class HolderContext : CelTypeContext { }
            public class PlainClass { public long A { get; set; } }
            public record PlainRecord(long A, string B);
            public struct PlainStruct { public long A { get; set; } }
            public readonly record struct PlainRecordStruct(long A);
            public interface IBase { long Id { get; } }
            public interface IDerived : IBase { string Label { get; } }
            public sealed class Page<T> { public IReadOnlyList<T> Rows { get; set; } = []; }
            public class Outer { public class Inner { public long Depth { get; set; } } }
            public abstract class Shape { public double Area { get; set; } }
            public class Holder
            {
                public PlainClass C { get; set; } = new(); public PlainRecord R { get; set; } = new(0, ""); public PlainStruct S { get; set; }
                public PlainRecordStruct Rs { get; set; } public IDerived I { get; set; } = null!; public Page<PlainClass> P { get; set; } = new();
                public Outer.Inner N { get; set; } = new(); public Shape Abstract { get; set; } = null!; public PlainStruct? OptS { get; set; }
                public Other.Foreign F { get; set; } = new();
            }
            """, Foreign: "namespace Other { public class Foreign { public long Size { get; set; } public string Name { get; set; } = \"\"; } }"),

        new("NamesCamel", NamesSource("")),
        new("NamesAsIs", NamesSource("[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.AsIs)]")),
        new("NamesSnake", NamesSource("[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.SnakeCaseLower)]")),
        new("NamesPlain", NamesSource("[CelTypeContextOptions(IgnoreJsonAttributes = true)]")),

        new("Excluded", Header + """
            [CelObject(typeof(Holder))] public partial class HolderContext : CelTypeContext { }
            [Obsolete] public class OldType { public long A { get; set; } }
            public class Holder
            {
                public long Kept { get; set; } public System.IO.Stream? Body { get; set; } [Obsolete] public long Old { get; set; } public char Letter { get; set; }
                public OldType Aged { get; set; } = new(); public int[,] Grid { get; set; } = new int[0, 0]; public Dictionary<double, long> Weights { get; set; } = [];
                public HashSet<object> Mixed { get; set; } = []; public List<System.IO.Stream> Streams { get; set; } = []; public Uri? Link { get; set; }
                [CelIgnore] public System.IO.Stream? Hidden { get; set; }
            }
            """),

        new("Message", Header + """
            [CelObject(typeof(Shipment))] public partial class ShipmentContext : CelTypeContext { }
            public class Shipment
            {
                public Acme.Pb.Msg Message { get; set; } = new(); public Acme.Pb.Msg? NoMessage { get; set; }
                public IReadOnlyList<Acme.Pb.Msg> Messages { get; set; } = []; public Dictionary<string, Acme.Pb.Msg> ByLabel { get; set; } = [];
            }
            """, Stub: GeneratorExample.ProtobufStub, Compiled: false),

        new("Decimal", Header + """
            [CelObject(typeof(Money))] public partial class MoneyContext : CelTypeContext { }
            public class Money { public decimal Amount { get; set; } public decimal? Rebate { get; set; } public System.Numerics.Decimal128 Exact { get; set; } }
            """, Stub: GeneratorExample.Decimal128Stub, Compiled: false),

        new("NullableOff", Header + """
            [CelObject(typeof(Record))] public partial class RecordContext : CelTypeContext { }
            public class Item { public long V { get; set; } }
            public class Record
            {
                public string Text { get; set; } public byte[] Blob { get; set; } public object Any { get; set; } public Item Item { get; set; }
                public List<string> Texts { get; set; } public List<Item> Items { get; set; } public Dictionary<string, Item> ByName { get; set; }
            }
            """, NullableContextOptions.Disable),

        new("Nested", "using Great.Parts.Cel.Values;\nnamespace Acme.Sales\n{\n    public partial class Outer\n    {\n        [CelObject(typeof(Thing))]\n        public partial class ThingContext : CelTypeContext { }\n    }\n    public class Thing { public long V { get; set; } }\n}\n[CelObject(typeof(Acme.Sales.Thing))]\npublic partial class GlobalContext : CelTypeContext { }\n"),
    ];

    public static TheoryData<string> Names
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (SnapshotContext context in All)
            {
                data.Add(context.Name);
            }

            return data;
        }
    }

    internal static SnapshotContext Get(string name) => All.Single(context => context.Name == name);

    // The same class under each naming rule: the attribute on the context says which.
    private static string NamesSource(string options) =>
        Header + options + "\n[CelObject(typeof(Names))] public partial class NamesContext : CelTypeContext { }\n" + """
            public class Names
            {
                [CelName("renamed")] public long Original { get; set; }
                [CelIgnore] public long Hidden { get; set; }
                [CelName("fromCel")] [System.Text.Json.Serialization.JsonPropertyName("fromJson")] public long Both { get; set; }
                [System.Text.Json.Serialization.JsonPropertyName("wire_name")] public long Wire { get; set; }
                [System.Text.Json.Serialization.JsonIgnore] public long Skipped { get; set; }
                [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Conditional { get; set; }
                public long TotalAmount { get; set; } public long URLPath { get; set; } public long HTTPServer { get; set; } public long Order2Line { get; set; }
                public long ID { get; set; } public long In { get; set; } public long As { get; set; }
            }
            """;
}

// Each context of the catalog is a valid input of the generator: one file, no error of the generator, and the file compiles
// with its sources (except where it holds a stub).
public sealed class SnapshotContextTests
{
    public static TheoryData<string> Names => SnapshotContexts.Names;

    [Theory]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    [MemberData(nameof(Names))]
    public void Context_GivesOneFileAndNoError(string name)
    {
        SnapshotContext context = SnapshotContexts.Get(name);
        CSharpCompilation compilation = GeneratorExample.Compile(
            context.Source,
            nullable: context.Nullable,
            more: context.Stub is null ? null : [context.Stub],
            references: context.Foreign is null ? null : [GeneratorExample.Library(context.Foreign)]);

        GeneratorDriverRunResult result = GeneratorExample.Run(compilation);

        Assert.Empty(GeneratorExample.Reported(result).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.NotEmpty(result.GeneratedTrees);
        if (context.Compiled)
        {
            Assert.Empty(compilation.AddSyntaxTrees(result.GeneratedTrees).GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }
    }

    // The warnings of the context that holds excluded members are the ones of GPCEL002 and nothing else.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ExcludedContext_ReportsOneWarningForEachExcludedMember()
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(SnapshotContexts.Get("Excluded").Source));

        string[] members = [.. GeneratorExample.Reported(result).Select(diagnostic => GeneratorExample.Place(diagnostic))];
        string[] excluded = ["Body", "Old", "Letter", "Aged", "Grid", "Weights", "Mixed", "Streams", "Link"];
        Assert.Equal(excluded.Order(System.StringComparer.Ordinal), members.Order(System.StringComparer.Ordinal));
        Assert.All(GeneratorExample.Reported(result), diagnostic => Assert.Equal("GPCEL002", diagnostic.Id));
    }
}

using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Diagnostics;

// The reasons and the places of the diagnostics that the examples of the specification do not name: the key of a dictionary, an
// element that the cost cannot charge, a type of the library, a member of an interface, the order of the checks after GPCEL008 and
// the second attribute of one type.
public sealed class ReasonTests
{
    private const string Copied = " is copied at each read of an element, which the cost does not charge; use a list, an array or a dictionary";

    private static string Source(string members, string extra = "") =>
        GeneratorExample.Preamble
        + "[CelObject(typeof(Holder))] public partial class Ctx : CelTypeContext { }\n"
        + "public class Holder { public long Kept { get; set; } " + members + " }\n"
        + extra;

    private static Diagnostic Warning(string members, string extra = "")
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(members, extra)));
        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        return diagnostic;
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL002_KeyOfADictionary_NamesTheKeyTypeAndItsReason()
    {
        Diagnostic diagnostic = Warning("public Dictionary<System.IO.Stream, long> Index { get; set; } = new();");

        Assert.Equal(
            "Member 'Index' of type 'Acme.Holder' is excluded from the CEL type: the key type System.IO.Stream cannot be read: streams are not data",
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("Index", GeneratorExample.Place(diagnostic));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL002_ATypeThatImplementsAFamilyForTwoElementTypes_IsExcluded()
    {
        const string Extra = """
            public sealed class Both : IReadOnlyList<long>, IReadOnlyList<string>
            {
                public int Count => 0;
                long IReadOnlyList<long>.this[int index] => 0;
                string IReadOnlyList<string>.this[int index] => "";
                IEnumerator<long> IEnumerable<long>.GetEnumerator() { yield break; }
                IEnumerator<string> IEnumerable<string>.GetEnumerator() { yield break; }
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { yield break; }
            }
            """;

        Diagnostic diagnostic = Warning("public Both Pair { get; set; } = new();", Extra);

        Assert.EndsWith("for more than one element type", diagnostic.GetMessage(CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
        Assert.Equal("Pair", GeneratorExample.Place(diagnostic));
    }

    // A set, a collection without an index and a structure that is a collection (other than ImmutableArray<T>) are copied or boxed at
    // each read of an element, where the limit of cost has no charge: such a type is refused under a collection, at any depth, before
    // the reasons that the type has below it (a set in a set is this reason and not the one of the elements of a set).
    [Theory]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "F-2")]
    [InlineData("public List<Queue<long>> Queues { get; set; } = new();", "the element type System.Collections.Generic.Queue<long> cannot be read: System.Collections.Generic.Queue<long>")]
    [InlineData("public Dictionary<HashSet<long>, long> Keys { get; set; } = new();", "the key type System.Collections.Generic.HashSet<long> cannot be read: System.Collections.Generic.HashSet<long>")]
    [InlineData("public HashSet<HashSet<long>> InSet { get; set; } = new();", "the element type System.Collections.Generic.HashSet<long> cannot be read: System.Collections.Generic.HashSet<long>")]
    [InlineData("public long[][] Rows { get; set; } = [];", null)]
    public static void GPCEL002_ACollectionThatIsCopiedAtEachRead_IsRefusedUnderACollection(string member, string? start)
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(member)));

        string[] messages = [.. GeneratorExample.Reported(result).Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))];
        if (start is null)
        {
            Assert.Empty(messages);
            return;
        }

        string message = Assert.Single(messages);
        Assert.Contains("is excluded from the CEL type: " + start, message, System.StringComparison.Ordinal);
        Assert.EndsWith(Copied, message, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "R-3")]
    public void GPCEL002_ACopiedElementAtADepth_NamesEachLevelOfTheComponents()
    {
        Diagnostic diagnostic = Warning("public List<List<HashSet<long>>> Deep { get; set; } = new();");

        Assert.Equal(
            "Member 'Deep' of type 'Acme.Holder' is excluded from the CEL type: "
            + "the element type System.Collections.Generic.List<System.Collections.Generic.HashSet<long>> cannot be read: "
            + "the element type System.Collections.Generic.HashSet<long> cannot be read: System.Collections.Generic.HashSet<long>" + Copied,
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    // The structure that is a collection and a structure that is an object: the first is refused as an element, the second is boxed at each
    // read of an element for the unit of the index and is allowed; ImmutableArray<T> is read without a copy and is allowed too.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void GPCEL002_StructureThatIsACollection_IsRefusedAsAnElementAndAStructureObjectIsNot()
    {
        const string Extra = """
            public readonly struct Bag : IReadOnlyCollection<long>
            {
                public int Count => 0;
                public IEnumerator<long> GetEnumerator() { yield break; }
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { yield break; }
            }
            public struct Point { public long X { get; set; } }
            """;
        const string Allowed = "public List<Point> Points { get; set; } = new(); public List<System.Collections.Immutable.ImmutableArray<long>> Arrays { get; set; } = new(); public Bag Own { get; set; }";

        Assert.Empty(GeneratorExample.Reported(GeneratorExample.Run(GeneratorExample.Compile(Source(Allowed, Extra)))));
        Diagnostic diagnostic = Warning("public List<Bag> Bags { get; set; } = new();", Extra);

        Assert.StartsWith("Member 'Bags' of type 'Acme.Holder' is excluded from the CEL type: the element type Acme.Bag cannot be read: Acme.Bag", diagnostic.GetMessage(CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    // The type of the library is no data and the type of an assembly of the application is, whatever its namespace: the assembly decides.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL002_TypeOfTheLibrary_HasNoCelType()
    {
        Diagnostic diagnostic = Warning("public Great.Parts.Cel.Values.CelObjectType Description { get; set; } = null!;");

        Assert.EndsWith("Great.Parts.Cel.Values.CelObjectType has no CEL type; use a string or your own type", diagnostic.GetMessage(CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void TypeOfTheApplicationInANamespaceOfTheLibrary_IsReadAsAnObject()
    {
        const string Source = """
            using Great.Parts.Cel.Values;
            namespace Great.Parts.Cel.Sales;
            [CelObject(typeof(Order))] public partial class Ctx : CelTypeContext { }
            public class Item { public long Qty { get; set; } }
            public class Order { public Item First { get; set; } = new(); }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("\"Great.Parts.Cel.Sales.Item\"", text, System.StringComparison.Ordinal);
        Assert.Contains("\"Great.Parts.Cel.Sales.Order\"", text, System.StringComparison.Ordinal);
    }

    // A root of the library is GPCEL001 with the same reason.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL001_RootOfTheLibrary_HasNoCelType()
    {
        const string Source = """
            using Great.Parts.Cel.Values;
            namespace Acme;
            [CelObject(typeof(CelObjectType))] public partial class Ctx : CelTypeContext { }
            """;

        Diagnostic diagnostic = GeneratorExample.Single(Source, "GPCEL001");

        Assert.EndsWith("Great.Parts.Cel.Values.CelObjectType has no CEL type; use a string or your own type", diagnostic.GetMessage(CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    // A member of an interface and a member of a type that only a member reaches are excluded at the member of the source.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL002_MemberOfAnInterfaceAndOfAReachedType_IsAtTheMember()
    {
        const string Source = """
            using System.IO;
            using Great.Parts.Cel.Values;
            namespace Acme;
            [CelObject(typeof(IHolder))] public partial class Ctx : CelTypeContext { }
            public interface IHolder { long Id { get; } Stream? Body { get; } Reached Next { get; } }
            public class Reached { public char Letter { get; set; } }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source));

        Diagnostic[] reported = [.. GeneratorExample.Reported(result).OrderBy(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture), System.StringComparer.Ordinal)];
        Assert.Equal(2, reported.Length);
        Assert.All(reported, diagnostic => Assert.Equal("GPCEL002", diagnostic.Id));
        Assert.Equal(["Body", "Letter"], [.. reported.Select(diagnostic => GeneratorExample.Place(diagnostic)).Order(System.StringComparer.Ordinal)]);
        Assert.Contains(reported, diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture).StartsWith("Member 'Letter' of type 'Acme.Reached'", System.StringComparison.Ordinal));
    }

    // decimal? is a member that net10.0 cannot read, GPCEL007, as decimal is.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL007_NullableDecimalWithoutDecimal128_IsAWarningAtTheMember()
    {
        Diagnostic diagnostic = GeneratorExample.Single(Source("public decimal? Rebate { get; set; }"), "GPCEL007");

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Rebate", GeneratorExample.Place(diagnostic));
        Assert.StartsWith("Member 'Rebate' of type 'Acme.Holder' is excluded from the CEL type: the CEL type decimal requires .NET 11", diagnostic.GetMessage(CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    // The language of the project is too old for the file: the file is not written and the build says so, and the other diagnostics of
    // the context still come out, so one build shows every error.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL008_ReadingGoesOn_TheOtherDiagnosticsOfTheContextAreReported()
    {
        const string Members = "public System.IO.Stream? Body { get; set; }";

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(Members), LanguageVersion.CSharp11));

        string[] ids = [.. GeneratorExample.Reported(result).Select(diagnostic => diagnostic.Id).Order(System.StringComparer.Ordinal)];
        Assert.Equal(["GPCEL002", "GPCEL008"], ids);
        Assert.Empty(result.GeneratedTrees);
    }

    // One type under two attributes with two names: the greater name, ordinal, is the second, whatever the order of the attributes, and
    // {3} is the name that stays.
    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("[CelObject(typeof(A), Name = \"x.Alpha\")] [CelObject(typeof(A), Name = \"x.Beta\")]")]
    [InlineData("[CelObject(typeof(A), Name = \"x.Beta\")] [CelObject(typeof(A), Name = \"x.Alpha\")]")]
    public static void GPCEL004_OneTypeWithTwoNames_TheGreaterNameIsTheSecond(string attributes)
    {
        string source = "using Great.Parts.Cel.Values;\nnamespace Acme;\n" + attributes + "\npublic partial class Ctx : CelTypeContext { }\npublic class A { public long V { get; set; } }\n";

        Diagnostic diagnostic = GeneratorExample.Single(source, "GPCEL004");

        Assert.Equal("Types 'Acme.A' and 'Acme.A' in type context 'Ctx' both map to the CEL type name 'x.Alpha'", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Equal("typeof(A)", GeneratorExample.Place(diagnostic));

        // The place is the argument of the attribute that holds the greater name.
        int beta = source.IndexOf("x.Beta", System.StringComparison.Ordinal);
        Assert.Equal(source.LastIndexOf("typeof(A)", beta, System.StringComparison.Ordinal), diagnostic.Location.SourceSpan.Start);
    }

    // A closure of five thousand types in a chain is the limit and not an overflow of the stack of the compiler: the walk keeps its own
    // stack. No file is written for the context.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void GPCEL009_ChainOfFiveThousandTypes_IsTheLimitAndNotAnOverflow()
    {
        const int Count = 5000;
        var text = new System.Text.StringBuilder(GeneratorExample.Preamble);
        text.Append("[CelObject(typeof(N0))] public partial class Ctx : CelTypeContext { }\n");
        for (int i = 0; i < Count; i++)
        {
            text.Append("public class N").Append(i).Append(i + 1 < Count ? " { public N" + (i + 1) + " Next { get; set; } = null!; }\n" : " { public long V { get; set; } }\n");
        }

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(text.ToString()));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL009", diagnostic.Id);
        Assert.Equal("The type closure of type context 'Ctx' contains more than 250 types", diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.Empty(result.GeneratedTrees);
    }
}

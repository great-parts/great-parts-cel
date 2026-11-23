using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Output;

// AllowObjectLiterals on [CelObject]: a type that the created code can create gets IsConstructible, CanSetField, IsRequiredField
// and CreateInstance; a type it cannot create (an interface, an abstract class, no usable constructor, a required member no literal
// can set) is GPCEL012 at the argument of the attribute; a type without the argument is not constructible.
public sealed class LiteralOutputTests
{
    private const string Allowed = "[CelObject(typeof(Thing), AllowObjectLiterals = true)]";

    private static string Source(string types, string attribute = Allowed) =>
        GeneratorExample.Preamble + attribute + "\npublic partial class Ctx : CelTypeContext { }\n" + types;

    [Theory]
    [Trait("Requirement", "F-26")]
    [InlineData("public interface Thing { int A { get; set; } }", "an interface cannot be created")]
    [InlineData("public abstract class Thing { public int A { get; set; } }", "an abstract class cannot be created")]
    [InlineData("public class Thing { private Thing() { } public int A { get; set; } }", "it has no public constructor")]
    [InlineData("public class Thing { public Thing(int a) { } public Thing(int a, int b) { } public int A { get; set; } }", "it has no public parameterless constructor and more than one public constructor with parameters")]
    [InlineData("public class Thing { public Thing(int other) { } public int A { get; set; } }", "the parameter 'other' of its only public constructor matches no field")]
    [InlineData("public class Thing { public Thing(long a) { } public int A { get; set; } }", "the type of the parameter 'a' of its only public constructor differs from the type of its field")]
    [InlineData("public class Thing { public required object Anything { get; set; } }", "the required member 'Anything' cannot be set by an object literal")]
    [InlineData("public class Thing { public int A { get; set; } [CelIgnore] public required int Hidden { get; set; } }", "the required member 'Hidden' cannot be set by an object literal")]
    public static void TypeThatCannotBeCreated_IsGpcel012AtTheArgument(string types, string reason)
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(types)));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result), reported => reported.Id == "GPCEL012");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("AllowObjectLiterals = true", GeneratorExample.Place(diagnostic));
        Assert.Equal(
            "Type 'Acme.Thing' is listed with AllowObjectLiterals = true but cannot be created by an object literal: " + reason,
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
        Assert.DoesNotContain("IsConstructible", GeneratorExample.CreatedText(result), System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-26")]
    public static void TypeWithoutTheArgument_IsNotConstructible()
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source("public class Thing { public int A { get; set; } }", "[CelObject(typeof(Thing))]")));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.DoesNotContain("IsConstructible", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("CreateInstance", text, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-26")]
    public static void TypeReachedOnlyThroughMembers_IsNotConstructible()
    {
        const string Types = "public class Thing { public Inner I { get; set; } = new(); }\npublic class Inner { public int A { get; set; } }";
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(Types)));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Equal(2, text.Split("protected override bool IsConstructible => true;", System.StringSplitOptions.None).Length);
    }

    [Fact]
    [Trait("Requirement", "F-26")]
    public static void TypeWithLiterals_HasTheMembersOfALiteral()
    {
        const string Types = """
            public sealed record Price(decimal Amount, string Currency = "RUB")
            {
                public int Weight { get; init; } = 1;
                public required string Tag { get; init; }
                public string Note { get; set; } = "";
                public string Label => Currency;
            }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(Types, "[CelObject(typeof(Price), AllowObjectLiterals = true)]")));

        Assert.Empty(GeneratorExample.Reported(result).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("protected override bool IsConstructible => true;", text, System.StringComparison.Ordinal);
        Assert.Contains("protected override bool CanSetField(int number)", text, System.StringComparison.Ordinal);
        Assert.Contains("protected override bool IsRequiredField(int number)", text, System.StringComparison.Ordinal);
        Assert.Contains("protected override global::Great.Parts.Cel.Values.CelValue CreateInstance(", text, System.StringComparison.Ordinal);
        // The parameter with a default keeps it when unset; the required member is in the initializer; the init member that is not
        // required goes through the accessor of its init accessor; the set member is assigned.
        Assert.Contains("var instance = new global::Acme.Price(v0, s1 ? v1 : \"RUB\")", text, System.StringComparison.Ordinal);
        Assert.Contains("Tag = v", text, System.StringComparison.Ordinal);
        Assert.Contains("UnsafeAccessorKind.Method, Name = \"set_Weight\"", text, System.StringComparison.Ordinal);
        Assert.Contains("instance.Note = v", text, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-26")]
    public static void CreatedCode_OfTheMembersOfTheMapping_Compiles()
    {
        const string Types = """
            public sealed class Thing
            {
                public Thing() { }
                public sbyte A { get; set; }
                public ushort B { get; init; }
                public float C { get; set; }
                public char D { get; set; }
                public Guid E { get; set; }
                public System.Int128 F { get; set; }
                public System.UInt128 G { get; set; }
                public System.Numerics.BigInteger H { get; set; }
                public DateOnly I { get; set; }
                public TimeOnly J { get; set; }
                public decimal K { get; set; }
                public byte[] L { get; set; } = [];
                public DateTimeOffset M { get; set; }
                public DateTime N { get; set; }
                public TimeSpan O { get; set; }
                public int? P { get; set; }
                public string? Q { get; set; }
                public List<string> R { get; set; } = [];
                public string[] S { get; set; } = [];
                public System.Collections.Immutable.ImmutableArray<long> T { get; set; }
                public Dictionary<string, Inner> U { get; set; } = [];
                public IReadOnlyDictionary<long, double> V { get; set; } = new Dictionary<long, double>();
                public IList<Inner> W { get; set; } = [];
                public Inner X { get; set; } = new();
                public CelValue Y { get; set; }
                public Pt Z { get; set; }
            }
            public sealed class Inner { public int A { get; set; } }
            public struct Pt { public long X; public long Y { get; init; } }
            """;

        using var loaded = new GeneratorExample.Loaded(Source(Types));

        Assert.NotNull(loaded.Provider("Acme.Ctx"));
    }

    [Fact]
    [Trait("Requirement", "F-26")]
    public static void CreatedCode_OfAStructure_Compiles()
    {
        const string Types = "public struct Thing { public long X; public long Y { get; init; } public required long Z { get; init; } }";

        using var loaded = new GeneratorExample.Loaded(Source(Types));

        Assert.NotNull(loaded.Provider("Acme.Ctx"));
    }
}

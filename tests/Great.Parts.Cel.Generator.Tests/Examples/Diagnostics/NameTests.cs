using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Diagnostics;

// GPCEL003, GPCEL004 and GPCEL005: names. The second of two members is the one that is reported and left out, by the order of
// the C# names (ordinal); for a type or a member, as for the diagnostics of the other examples, the place of an argument of the
// attribute is the whole argument (`typeof(T)` or `Name = "x"`) and the place of a member is its identifier.
public sealed class NameTests
{
    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("[CelName(\"x\")] public long A { get; set; } [CelName(\"x\")] public long B { get; set; }", "", "A", "B", "x", "B")]
    [InlineData("[System.Text.Json.Serialization.JsonPropertyName(\"second\")] public long First { get; set; } public long Second { get; set; }", "", "First", "Second", "second", "Second")]
    [InlineData("public long Id { get; set; } public long ID { get; set; }", "", "ID", "Id", "id", "Id")]
    [InlineData("public long TotalAmount { get; set; } public long total_amount { get; set; }", "[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.SnakeCaseLower)]", "TotalAmount", "total_amount", "total_amount", "total_amount")]
    public void GPCEL003_TwoMembersWithOneName_IsAnErrorAtTheSecond(string members, string options, string first, string second, string field, string place)
    {
        string source = GeneratorExample.Preamble + options + "\n[CelObject(typeof(Holder))]\npublic partial class Ctx : CelTypeContext { }\npublic class Holder { " + members + " }\n";

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Members '" + first + "' and '" + second + "' of type 'Acme.Holder' both map to the CEL field name '" + field + "'", diagnostic.GetMessage());
        Assert.Equal(place, GeneratorExample.Place(diagnostic));
        Assert.Equal("Duplicate CEL field name", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel003", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL004_TwoRootsWithOneName_IsAnErrorAtTheArgumentOfTheSecond()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(A), Name = "acme.Thing")]
            [CelObject(typeof(B), Name = "acme.Thing")]
            public partial class Ctx : CelTypeContext { }
            public class A { public long V { get; set; } }
            public class B { public long V { get; set; } }
            """;

        Diagnostic diagnostic = GeneratorExample.Single(Source, "GPCEL004");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Types 'Acme.A' and 'Acme.B' in type context 'Ctx' both map to the CEL type name 'acme.Thing'", diagnostic.GetMessage());
        Assert.Equal("typeof(B)", GeneratorExample.Place(diagnostic));
        Assert.Equal("Duplicate CEL type name", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel004", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }

    // Two types get one default name: the closed generic Box<A_B> is Acme.Box_Acme_A_B, the way the class Box_Acme_A_B is.
    // The second is left out; Name on either is the way out.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL004_TwoTypesWithOneDefaultName_IsAnErrorAndNameIsTheWayOut()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(Box<A_B>))]
            [CelObject(typeof(Box_Acme_A_B))]
            public partial class Ctx : CelTypeContext { }
            public class A_B { public long V { get; set; } }
            public class Box<T> { public long W { get; set; } }
            public class Box_Acme_A_B { public long Z { get; set; } }
            """;
        string named = Source.Replace("typeof(Box_Acme_A_B))", "typeof(Box_Acme_A_B), Name = \"Acme.Plain\")", System.StringComparison.Ordinal);

        Diagnostic diagnostic = GeneratorExample.Single(Source, "GPCEL004");
        GeneratorDriverRunResult fixedRun = GeneratorExample.Run(GeneratorExample.Compile(named));

        Assert.Equal("Types 'Acme.Box<Acme.A_B>' and 'Acme.Box_Acme_A_B' in type context 'Ctx' both map to the CEL type name 'Acme.Box_Acme_A_B'", diagnostic.GetMessage());
        Assert.Equal("typeof(Box_Acme_A_B)", GeneratorExample.Place(diagnostic));
        Assert.Empty(GeneratorExample.Reported(fixedRun));
    }

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("not valid!")]
    [InlineData("a..b")]
    [InlineData("a.1b")]
    [InlineData("acme.true")]
    public void GPCEL005_NameOfARootThatIsNotAQualifiedName_IsAnErrorAtTheArgument(string name)
    {
        string source = GeneratorExample.Preamble + "[CelObject(typeof(Holder), Name = \"" + name + "\")]\npublic partial class Ctx : CelTypeContext { }\npublic class Holder { public long V { get; set; } }\n";

        Diagnostic diagnostic = GeneratorExample.Single(source, "GPCEL005");

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("'" + name + "' is not a valid CEL qualified type name", diagnostic.GetMessage());
        Assert.Equal("Name = \"" + name + "\"", GeneratorExample.Place(diagnostic));
        Assert.Equal("Invalid CEL name", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel005", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }

    // The name of a field that is not an identifier: from [CelName], from [JsonPropertyName], or from the C# name with letters
    // outside ASCII (the policy AsIs keeps the name as written). The member is left out; the place is the member.
    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("[CelName(\"bad-name\")] public long A { get; set; }", "bad-name", "A")]
    [InlineData("[System.Text.Json.Serialization.JsonPropertyName(\"with space\")] public long A { get; set; }", "with space", "A")]
    [InlineData("[CelName(\"1abc\")] public long A { get; set; }", "1abc", "A")]
    [InlineData("public long Café { get; set; }", "Café", "Café")]
    public void GPCEL005_FieldNameThatIsNotAnIdentifier_IsAnErrorAtTheMember(string member, string name, string place)
    {
        string source = GeneratorExample.Preamble
            + "[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.AsIs)]\n[CelObject(typeof(Holder))]\npublic partial class Ctx : CelTypeContext { }\npublic class Holder { public long Kept { get; set; } " + member + " }\n";

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL005", diagnostic.Id);
        Assert.Equal("'" + name + "' is not a valid CEL identifier", diagnostic.GetMessage());
        Assert.Equal(place, GeneratorExample.Place(diagnostic));
        Assert.DoesNotContain("\"" + name + "\"", GeneratorExample.CreatedText(result), System.StringComparison.Ordinal);
        Assert.Contains("\"Kept\"", GeneratorExample.CreatedText(result), System.StringComparison.Ordinal);
    }

    // A type whose default name is not a CEL qualified name (a letter outside ASCII) is GPCEL005 at the argument of its
    // attribute, and Name is the way out.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL005_DefaultNameOfARootThatIsNotAQualifiedName_IsAnErrorAtTheArgument()
    {
        string source = GeneratorExample.Preamble + "[CelObject(typeof(Café))]\npublic partial class Ctx : CelTypeContext { }\npublic class Café { public long V { get; set; } }\n";

        Diagnostic diagnostic = GeneratorExample.Single(source, "GPCEL005");
        GeneratorDriverRunResult named = GeneratorExample.Run(GeneratorExample.Compile(source.Replace("typeof(Café))", "typeof(Café), Name = \"acme.Cafe\")", System.StringComparison.Ordinal)));

        Assert.Equal("'Acme.Café' is not a valid CEL qualified type name", diagnostic.GetMessage());
        Assert.Equal("typeof(Café)", GeneratorExample.Place(diagnostic));
        Assert.Empty(GeneratorExample.Reported(named));
    }

    // The field names that need no diagnostic: a reserved word of CEL, a name from [CelName] that is an identifier.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ReservedWordAsAFieldName_HasNoDiagnostic()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(Holder))]
            public partial class Ctx : CelTypeContext { }
            public class Holder { public long In { get; set; } [CelName("as")] public long Other { get; set; } }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("\"in\"", text, System.StringComparison.Ordinal);
        Assert.Contains("\"as\"", text, System.StringComparison.Ordinal);
    }
}

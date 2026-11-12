using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Diagnostics;

// GPCEL002 and GPCEL007: a member of an object type that the context cannot read is left out of the type with a warning;
// the type and the rest of its members are described. The place is the identifier of the member in the source; for a member
// of a type of another assembly, which has no place of its own, it is the argument of the attribute that lists the type.
// A member is marked when the attribute stands on the property or the field or on its get accessor (RequiresUnreferencedCode and
// RequiresDynamicCode have no target property). {0} is the C# name of the member, {1} the display name of the type that declares it, {2} the reason.
public sealed class ExcludedMemberTests
{
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "S-1")]
    [InlineData("[System.Obsolete] public int Old { get; set; }", "Old", "the member is marked [Obsolete]")]
    [InlineData("[System.Diagnostics.CodeAnalysis.Experimental(\"EXP01\")] public int Fresh { get; set; }", "Fresh", "the member is marked [Experimental]")]
    [InlineData("public int Slow { [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(\"x\")] get; set; }", "Slow", "the member is marked [RequiresUnreferencedCode]")]
    [InlineData("public int Dynamic { [System.Diagnostics.CodeAnalysis.RequiresDynamicCode(\"x\")] get; set; }", "Dynamic", "the member is marked [RequiresDynamicCode]")]
    [InlineData("public Marked Slow { get; set; } = new();", "Slow", "Acme.Marked is marked [RequiresUnreferencedCode]")]
    [InlineData("public OldType Aged { get; set; } = new();", "Aged", "Acme.OldType is marked [Obsolete]")]
    [InlineData("public System.IO.Stream? Body { get; set; }", "Body", "streams are not data")]
    [InlineData("public System.Type? Kind { get; set; }", "Kind", "System.Type is not data")]
    [InlineData("public char Letter { get; set; }", "Letter", "char is a UTF-16 code unit, not a code point; use string")]
    [InlineData("public System.DateOnly Day { get; set; }", "Day", "System.DateOnly is not supported; use DateTimeOffset or string")]
    [InlineData("public System.Uri? Link { get; set; }", "Link", "System.Uri has no CEL type; use a string or your own type")]
    [InlineData("public System.Collections.Generic.IEnumerable<int>? Lazy { get; set; }", "Lazy", "System.Collections.Generic.IEnumerable<int> has no size and no index; use an array, a list or a collection with a size")]
    [InlineData("public int[,] Grid { get; set; } = new int[0, 0];", "Grid", "multidimensional arrays are not supported; use an array of arrays or a list")]
    [InlineData("public System.Collections.Generic.Dictionary<double, int> Weights { get; set; } = new();", "Weights", "dictionary keys must be string, an integer, bool, an enumeration or Guid")]
    [InlineData("public System.Collections.Generic.HashSet<object> Mixed { get; set; } = new();", "Mixed", "set elements must be numbers, bool, string, bytes, timestamp, duration or decimal, which CEL orders")]
    [InlineData("public System.Collections.Generic.List<System.IO.Stream> Streams { get; set; } = new();", "Streams", "the element type System.IO.Stream cannot be read: streams are not data")]
    [InlineData("public System.Collections.Generic.Dictionary<string, System.Uri> Links { get; set; } = new();", "Links", "the value type System.Uri cannot be read: System.Uri has no CEL type; use a string or your own type")]
    [InlineData("public System.Collections.Generic.List<System.Collections.Generic.HashSet<long>> Sets { get; set; } = new();", "Sets", "the element type System.Collections.Generic.HashSet<long> cannot be read: System.Collections.Generic.HashSet<long> is copied at each read of an element, which the cost does not charge; use a list, an array or a dictionary")]
    [InlineData("public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Queue<long>> Queues { get; set; } = new();", "Queues", "the value type System.Collections.Generic.Queue<long> cannot be read: System.Collections.Generic.Queue<long> is copied at each read of an element, which the cost does not charge; use a list, an array or a dictionary")]
    public void GPCEL002_MemberThatCannotBeRead_IsAWarningAtTheMember(string member, string name, string reason)
    {
        string source = Source(member);

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Member '" + name + "' of type 'Acme.Holder' is excluded from the CEL type: " + reason, diagnostic.GetMessage());
        Assert.Equal(name, GeneratorExample.Place(diagnostic));
        Assert.Equal("Member excluded from CEL type", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel002", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);

        // The type and its other member are described; the excluded one answers GetExcludedFieldReason, and is not a field.
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("\"Acme.Holder\"", text, System.StringComparison.Ordinal);
        Assert.Contains("\"kept\"", text, System.StringComparison.Ordinal);
        Assert.Contains("GetExcludedFieldReason", text, System.StringComparison.Ordinal);
        Assert.Contains("\"" + char.ToLowerInvariant(name[0]) + name[1..] + "\"", text, System.StringComparison.Ordinal);
    }

    // A hidden member is not a field and has no warning: [CelIgnore], [JsonIgnore] (condition Always, also by default),
    // a name of IgnoreMembers of the root of its type. A [JsonIgnore] with another condition does not hide the member, and with
    // IgnoreJsonAttributes the attribute is not read.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "S-1")]
    [InlineData("[CelIgnore] public System.IO.Stream? Body { get; set; }", "", false)]
    [InlineData("[System.Text.Json.Serialization.JsonIgnore] public System.IO.Stream? Body { get; set; }", "", false)]
    [InlineData("[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Always)] public System.IO.Stream? Body { get; set; }", "", false)]
    [InlineData("public System.IO.Stream? Body { get; set; }", ", IgnoreMembers = new[] { \"Body\" }", false)]
    [InlineData("[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public System.IO.Stream? Body { get; set; }", "", true)]
    public void GPCEL002_HiddenMember_HasNoWarning(string member, string arguments, bool warns)
    {
        string source = Source(member, arguments);

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source));

        Assert.Equal(warns, GeneratorExample.Reported(result).Any(diagnostic => diagnostic.Id == "GPCEL002"));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL002_JsonIgnoreWithIgnoreJsonAttributes_IsNotRead()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(Holder))]
            [CelTypeContextOptions(IgnoreJsonAttributes = true)]
            public partial class Ctx : CelTypeContext { }
            public class Holder
            {
                public long Kept { get; set; }
                [System.Text.Json.Serialization.JsonIgnore] public System.IO.Stream? Body { get; set; }
            }
            """;

        Diagnostic diagnostic = GeneratorExample.Single(Source, "GPCEL002");

        Assert.Equal("Body", GeneratorExample.Place(diagnostic));
    }

    // A member of a type of another assembly has no place in the source: the warning is at the argument of the attribute that
    // lists the type, and IgnoreMembers there removes it.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL002_MemberOfAForeignType_IsAtTheArgumentOfTheAttribute()
    {
        MetadataReference foreign = GeneratorExample.Library("namespace Other { public class Holder { public long Kept { get; set; } public System.IO.Stream? Body { get; set; } } }");
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(Other.Holder))]
            public partial class Ctx : CelTypeContext { }
            """;
        const string Hidden = GeneratorExample.Preamble + """
            [CelObject(typeof(Other.Holder), IgnoreMembers = new[] { "Body" })]
            public partial class Ctx : CelTypeContext { }
            """;

        Diagnostic diagnostic = GeneratorExample.Single(Source, "GPCEL002", references: [foreign]);
        GeneratorDriverRunResult hidden = GeneratorExample.Run(GeneratorExample.Compile(Hidden, references: [foreign]));

        Assert.Equal("Member 'Body' of type 'Other.Holder' is excluded from the CEL type: streams are not data", diagnostic.GetMessage());
        Assert.Equal("typeof(Other.Holder)", GeneratorExample.Place(diagnostic));
        Assert.Empty(GeneratorExample.Reported(hidden));
    }

    // Members that are not data of an instance are not members at all: no warning, no field.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void StaticMembersIndexersAndNonPublicMembers_AreNotMembers()
    {
        const string Source = GeneratorExample.Preamble + """
            [CelObject(typeof(Holder))]
            public partial class Ctx : CelTypeContext { }
            public class Holder
            {
                public long Kept { get; set; }
                public static long Shared { get; set; }
                public long this[int index] => index;
                public long WriteOnly { set { } }
                internal long Inner { get; set; }
                private long Secret { get; set; }
                public long Plain;
            }
            """;

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("\"kept\"", text, System.StringComparison.Ordinal);
        Assert.Contains("\"plain\"", text, System.StringComparison.Ordinal);
        foreach (string absent in new[] { "shared", "writeOnly", "inner", "secret" })
        {
            Assert.DoesNotContain("\"" + absent + "\"", text, System.StringComparison.Ordinal);
        }
    }

    // GPCEL007: System.Decimal in a compilation that has no System.Numerics.Decimal128 (net10.0, which is what this project
    // runs on): the member is left out, with its own code. With the type in the compilation there is no diagnostic and the member
    // is read as decimal. A member of Decimal128 itself cannot exist in the compilation without the type, so no row for it.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-5")]
    public void GPCEL007_DecimalMemberWithoutDecimal128_IsAWarningAtTheMember()
    {
        string source = Source("public decimal Price { get; set; }");

        Diagnostic diagnostic = GeneratorExample.Single(source, "GPCEL007");

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(
            "Member 'Price' of type 'Acme.Holder' is excluded from the CEL type: the CEL type decimal requires .NET 11 (System.Numerics.Decimal128)",
            diagnostic.GetMessage());
        Assert.Equal("Price", GeneratorExample.Place(diagnostic));
        Assert.Equal("Decimal member excluded from CEL type", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel007", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-5")]
    public void GPCEL007_NoDiagnosticWithDecimal128InTheCompilation_AndTheMembersAreFields()
    {
        string source = Source("public decimal Price { get; set; } public decimal? Rebate { get; set; } public System.Numerics.Decimal128 Exact { get; set; }");

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source, more: [GeneratorExample.Decimal128Stub]));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("\"price\"", text, System.StringComparison.Ordinal);
        Assert.Contains("\"rebate\"", text, System.StringComparison.Ordinal);
        Assert.Contains("\"exact\"", text, System.StringComparison.Ordinal);
        Assert.Contains("CelType.Decimal", text, System.StringComparison.Ordinal);
    }

    private static string Source(string member, string arguments = "") =>
        GeneratorExample.Preamble + """
            [System.Obsolete] public class OldType { public int A { get; set; } }
            [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("x")] public class Marked { public int A { get; set; } }
            """ + "\n[CelObject(typeof(Holder)" + arguments + ")]\npublic partial class Ctx : CelTypeContext { }\n"
        + "public class Holder { public long Kept { get; set; } " + member + " }\n";
}

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Diagnostics;

// GPCEL006, GPCEL008 and GPCEL009: the declaration of the context and the facts of the compilation. The place is the
// identifier of the context class; no code is written for any of the three. {0} is the source name of the context, the simple
// name of its class, with no namespace, no containing type and no type parameters (the generator page, "Places and arguments").
public sealed class ContextTests
{
    private const string Rule = "must be a non-generic partial class that derives from CelTypeContext and is not nested in a generic type";

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("[CelObject(typeof(Good))] public class Ctx : CelTypeContext { }", "Ctx")]
    [InlineData("[CelObject(typeof(Good))] public partial class Ctx<T> : CelTypeContext { }", "Ctx")]
    [InlineData("public class Outer<T> { [CelObject(typeof(Good))] public partial class Ctx : CelTypeContext { } }", "Ctx")]
    [InlineData("[CelObject(typeof(Good))] public partial class Ctx { }", "Ctx")]
    [InlineData("[CelObject(typeof(Good))] public partial class Ctx : System.Collections.Generic.List<int> { }", "Ctx")]
    public void GPCEL006_ContextThatIsNotAPartialNonGenericDerivedClass_IsAnErrorAtTheDeclaration(string declaration, string shown)
    {
        string source = GeneratorExample.Preamble + declaration + "\npublic class Good { public long V { get; set; } }\n";

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result), d => d.Id == "GPCEL006");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Type context '" + shown + "' " + Rule, diagnostic.GetMessage());
        Assert.Equal("Ctx", GeneratorExample.Place(diagnostic));
        Assert.Equal("Invalid CEL type context", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel006", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
        Assert.Empty(result.GeneratedTrees);
        Assert.Single(GeneratorExample.Reported(result));
    }

    // GPCEL008: the language version of the project below C# 12. No code is written; 12 and the later versions, and `default`
    // and `latest`, which the compiler resolves to a version, are fine.
    [Theory]
    [Trait("Requirement", "S-5")]
    [Trait("Requirement", "U-1")]
    [InlineData(LanguageVersion.CSharp11, "11")]
    [InlineData(LanguageVersion.CSharp10, "10")]
    [InlineData(LanguageVersion.CSharp7_3, "7.3")]
    public void GPCEL008_LanguageVersionBelow12_IsAnErrorAtTheDeclaration(LanguageVersion version, string shown)
    {
        string source = GeneratorExample.Preamble + "[CelObject(typeof(Good))]\npublic partial class Ctx : CelTypeContext { }\npublic class Good { public long V { get; set; } }\n";

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source, version));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("The code generated for type context 'Ctx' requires C# 12 or later; the project uses C# " + shown, diagnostic.GetMessage());
        Assert.Equal("Ctx", GeneratorExample.Place(diagnostic));
        Assert.Equal("C# language version not supported", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel008", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
        Assert.Empty(result.GeneratedTrees);
    }

    [Theory]
    [Trait("Requirement", "S-5")]
    [InlineData(LanguageVersion.CSharp12)]
    [InlineData(LanguageVersion.CSharp13)]
    [InlineData(LanguageVersion.Default)]
    [InlineData(LanguageVersion.Latest)]
    [InlineData(LanguageVersion.LatestMajor)]
    public void GPCEL008_LanguageVersion12OrLater_HasNoDiagnostic(LanguageVersion version)
    {
        string source = GeneratorExample.Preamble + "[CelObject(typeof(Good))]\npublic partial class Ctx : CelTypeContext { }\npublic class Good { public long V { get; set; } }\n";

        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(source, version));

        Assert.Empty(GeneratorExample.Reported(result));
        Assert.Single(result.GeneratedTrees);
    }

    // GPCEL009: a chain of types T0 -> T1 -> ... -> Tn, the closure of the root T0. 251 object types are over the limit, 250 are
    // not; enumerations are leaves and do not count (the last type of the chain also holds an enumeration).
    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL009_ClosureOf251Types_IsAnErrorAtTheDeclaration()
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Chain(251)));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("The type closure of type context 'Ctx' contains more than 250 types", diagnostic.GetMessage());
        Assert.Equal("Ctx", GeneratorExample.Place(diagnostic));
        Assert.Equal("CEL type context too large", diagnostic.Descriptor.Title.ToString());
        Assert.EndsWith("docs/generator-diagnostics.md#gpcel009", diagnostic.Descriptor.HelpLinkUri, System.StringComparison.Ordinal);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void GPCEL009_ClosureOf250TypesAndAnEnumeration_HasNoDiagnostic()
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Chain(250)));

        Assert.Empty(GeneratorExample.Reported(result));
        Assert.Single(result.GeneratedTrees);
    }

    private static string Chain(int count)
    {
        var source = new StringBuilder(GeneratorExample.Preamble);
        source.Append("[CelObject(typeof(T0))]\npublic partial class Ctx : CelTypeContext { }\npublic enum Color { Red }\n");
        for (int index = 0; index < count; index++)
        {
            source.Append("public class T").Append(index).Append(" { public long V { get; set; } ");
            source.Append(index + 1 < count ? "public T" + (index + 1) + " Next { get; set; }" : "public Color Last { get; set; }");
            source.Append(" }\n");
        }

        return source.ToString();
    }
}

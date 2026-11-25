using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.CallSites;

// The generator of call sites with classes of the application in the properties of the anonymous object. It writes one
// file of descriptions for the assembly, CelCallSiteTypes.g.cs, with each type that any call site reaches, and the registration of
// each shape as before; a list or a dictionary of classes is declared and bound by the created code (CelList.CreateView and
// CelMap.CreateView), not by CelBindings.Add<T>. The diagnostics of a type context come from the call site too. The compilation is
// the one of GeneratorExample.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "S-1")]
public sealed class ClassCallSiteTests
{
    private const string Head = "using System.Collections.Generic;\nusing System.Collections.Immutable;\nusing Great.Parts.Cel;\nnamespace Acme;\n";

    private const string Types =
        "public sealed class Line { public string Sku { get; set; } = \"\"; public long Qty { get; set; } }\n"
        + "public sealed class Order { public double Total { get; set; } public Line[] Lines { get; set; } = []; }\n"
        + "public struct Point { public long X { get; set; } }\n";

    [Fact]
    public void ClassInAProperty_IsARegistrationAndOneFileOfDescriptions()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<bool>(\"order.total > 1.0\", new { order = new Order() });\n"));

        Assert.Empty(outcome.Reported);
        Assert.Equal(["CelAnonymousData_1.g.cs", "CelCallSiteTypes.g.cs"], outcome.HintNames);
        Assert.Empty(outcome.CompilerErrors);
    }

    // Two shapes that reach the same class: two registrations, one file of descriptions with one description of each type.
    [Fact]
    public void TwoShapesReachingOneClass_ShareTheFileOfDescriptions()
    {
        Outcome outcome = Run(Body(
            "CelExpression.Evaluate<bool>(\"order.total > 1.0\", new { order = new Order() });\n"
            + "CelExpression.Evaluate<bool>(\"o.lines.size() == 0 && n == 1\", new { o = new Order(), n = 1L });\n"));

        Assert.Empty(outcome.Reported);
        Assert.Equal(["CelAnonymousData_1.g.cs", "CelAnonymousData_2.g.cs", "CelCallSiteTypes.g.cs"], outcome.HintNames);
        Assert.Empty(outcome.CompilerErrors);
    }

    // Lists and dictionaries of classes and a list of structures: no diagnostic; the created code declares and binds them with the
    // views of the library, closed at build time.
    [Fact]
    public void CollectionsOfClasses_AreBoundByTheCreatedCode()
    {
        Outcome outcome = Run(Body(
            "CelExpression.Evaluate<bool>(\"true\", new { lines = new List<Line>(), array = new Line[0], "
            + "immutable = ImmutableArray<Line>.Empty, byName = new Dictionary<string, Line>(), points = new List<Point>() });\n"));

        Assert.Empty(outcome.Reported);
        Assert.Empty(outcome.CompilerErrors);
        string created = string.Concat(outcome.Texts);
        Assert.Contains("CelList.CreateView<", created, StringComparison.Ordinal);
        Assert.Contains("CelMap.CreateView<", created, StringComparison.Ordinal);
    }

    // A type that a type context cannot describe: GPCEL011 with that reason, at the property.
    [Theory]
    [InlineData("new { u = new System.Uri(\"http://a\") }", "u", "System.Uri")]
    [InlineData("new { f = (System.Func<long>)(() => 1L) }", "f", "System.Func<long>")]
    public void TypeThatAContextCannotDescribe_IsGPCEL011(string data, string property, string shown)
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", " + data + ");\n"));

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL011", error.Id);
        Assert.StartsWith("Property '" + property + "' of the anonymous data has type '" + shown + "'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("a type that a type context cannot describe", error.GetMessage(), StringComparison.Ordinal);
        Assert.StartsWith(property, Place(error), StringComparison.Ordinal);
    }

    // A named class as the data itself: GPCEL010 with the advice to pass it as a property of an anonymous object.
    [Fact]
    public void NamedClassAsTheData_IsGPCEL010WithTheAdviceOfAProperty()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<bool>(\"total > 1.0\", new Order());\n"));

        Diagnostic warning = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL010", warning.Id);
        Assert.Contains("new { order }", warning.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(outcome.HintNames);
    }

    // The diagnostics of a context are reported for the descriptions of a call site: a member of a delegate type is excluded with
    // GPCEL002 (the file is still created), two members of one CEL name are GPCEL003 at the second member.
    [Fact]
    public void ExcludedMember_IsGPCEL002_AndTheFileIsCreated()
    {
        Outcome outcome = Run(Head
            + "public sealed class Job { public long Id { get; set; } public System.Action Callback { get; set; } = static () => { }; }\n"
            + "class C\n{\n    void M()\n    {\n"
            + "CelExpression.Evaluate<bool>(\"job.id == 1\", new { job = new Job() });\n"
            + "    }\n}\n");

        Diagnostic warning = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL002", warning.Id);
        Assert.StartsWith("Member 'Callback' of type 'Acme.Job' is excluded from the CEL type", warning.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(["CelAnonymousData_1.g.cs", "CelCallSiteTypes.g.cs"], outcome.HintNames);
        Assert.Empty(outcome.CompilerErrors);
    }

    [Fact]
    public void TwoMembersOfOneCelName_AreGPCEL003()
    {
        Outcome outcome = Run(Head
            + "public sealed class Twin { public long Name { get; set; } public long name { get; set; } }\n"
            + "class C\n{\n    void M()\n    {\n"
            + "CelExpression.Evaluate<bool>(\"true\", new { twin = new Twin() });\n"
            + "    }\n}\n");

        Diagnostic error = Assert.Single(outcome.Reported, diagnostic => diagnostic.Id == "GPCEL003");
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Acme.Twin'", error.GetMessage(), StringComparison.Ordinal);
    }

    private static string Body(string statements) =>
        Head + Types + "class C\n{\n    void M()\n    {\n" + statements + "    }\n}\n";

    private static string Place(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static Outcome Run(string source)
    {
        CSharpCompilation compilation = GeneratorExample.Compile(source, LanguageVersion.CSharp12);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new CelAnonymousDataGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp12));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();
        GeneratedSourceResult[] sources = [.. result.Results.SelectMany(run => run.GeneratedSources).OrderBy(source => source.HintName, StringComparer.Ordinal)];

        return new Outcome(
            [.. result.Diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("GPCEL", StringComparison.Ordinal))],
            [.. sources.Select(source => source.HintName)],
            [.. sources.Select(source => source.SourceText.ToString().Replace("\r\n", "\n", StringComparison.Ordinal))],
            [.. output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)]);
    }

    private sealed record Outcome(ImmutableArray<Diagnostic> Reported, string[] HintNames, string[] Texts, ImmutableArray<Diagnostic> CompilerErrors);
}

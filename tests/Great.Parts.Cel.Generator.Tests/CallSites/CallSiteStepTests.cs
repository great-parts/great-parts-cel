using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Great.Parts.Cel.Generator.Tests.CallSites.CallSiteRun;

namespace Great.Parts.Cel.Generator.Tests.CallSites;

// The steps of the pipeline of call sites are cached (trackIncrementalGeneratorSteps): CallSite reads the calls of one file into values,
// Shapes is the distinct shapes in the order of their first call, and CallSiteDiagnostics the places of the diagnostics. What an editor
// does to the application: nothing, a change of a file that has no call, lines inserted above a call, a change of a property type.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "S-6")]
public sealed class CallSiteStepTests
{
    private const string Calls = "CelExpression.Evaluate<long>(\"x\", new { x = 1L });\nCelExpression.Evaluate<long>(\"y\", new { y = \"s\" });\n";

    private const string Unrelated = "namespace Acme; public class Unrelated { public long V { get; set; } }\n";

    private static readonly string[] Steps = ["CallSite", "Shapes", "CallSiteDiagnostics"];

    [Fact]
    public void NothingChanged_EveryCallSiteStepIsCachedOrUnchanged()
    {
        CSharpCompilation compilation = Build(Body(Calls), Unrelated);
        GeneratorDriver driver = Driver(track: true).RunGenerators(compilation, TestContext.Current.CancellationToken);

        GeneratorDriverRunResult second = driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();

        foreach (string step in Steps)
        {
            Assert.All(Reasons(second, step), reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, step + ": " + reason));
        }

        Assert.NotEmpty(Reasons(second, "Shapes"));
    }

    [Fact]
    public void ChangeOfAFileWithoutACall_LeavesEveryStepCachedOrUnchanged()
    {
        CSharpCompilation first = Build(Body(Calls), Unrelated);
        GeneratorDriver driver = Driver(track: true).RunGenerators(first, TestContext.Current.CancellationToken);
        SyntaxTree old = first.SyntaxTrees.Single(tree => tree.FilePath == "More0.cs");
        CSharpCompilation second = first.ReplaceSyntaxTree(old, old.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(Unrelated + "// an edit\n")));

        GeneratorDriverRunResult result = driver.RunGenerators(second, TestContext.Current.CancellationToken).GetRunResult();

        foreach (string step in Steps)
        {
            Assert.All(Reasons(result, step), reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, step + ": " + reason));
        }
    }

    // Lines inserted above the calls move their places, which are a part of the value of the call, and not the shapes: the step of the
    // shapes is unchanged, so the files are not made again for a change of a line number.
    [Fact]
    public void LinesInsertedAboveTheCalls_ModifyTheCallsAndNotTheShapes()
    {
        CSharpCompilation first = Build(Body(Calls));
        GeneratorDriver driver = Driver(track: true).RunGenerators(first, TestContext.Current.CancellationToken);
        string before = Texts(driver);
        SyntaxTree old = first.SyntaxTrees.Single(tree => tree.FilePath == "Source.cs");
        CSharpCompilation second = first.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText("// one\n// two\n" + Body(Calls), new CSharpParseOptions(LanguageVersion.CSharp12), path: "Source.cs", cancellationToken: TestContext.Current.CancellationToken));

        driver = driver.RunGenerators(second, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult result = driver.GetRunResult();

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(result, "CallSite"));
        Assert.All(Reasons(result, "Shapes"), reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, "Shapes: " + reason));
        Assert.Equal(before, Texts(driver));
    }

    [Fact]
    public void ChangeOfAPropertyType_ModifiesTheShapesAndTheText()
    {
        CSharpCompilation first = Build(Body(Calls));
        GeneratorDriver driver = Driver(track: true).RunGenerators(first, TestContext.Current.CancellationToken);
        string before = Texts(driver);
        SyntaxTree old = first.SyntaxTrees.Single(tree => tree.FilePath == "Source.cs");
        CSharpCompilation second = first.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(Body(Calls).Replace("x = 1L", "x = 1", System.StringComparison.Ordinal), new CSharpParseOptions(LanguageVersion.CSharp12), path: "Source.cs", cancellationToken: TestContext.Current.CancellationToken));

        driver = driver.RunGenerators(second, TestContext.Current.CancellationToken);

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(driver.GetRunResult(), "Shapes"));
        Assert.NotEqual(before, Texts(driver));
    }

    private static CSharpCompilation Build(string source, params string[] more) => GeneratorExample.Compile(source, LanguageVersion.CSharp12, more: more);

    private static string Texts(GeneratorDriver driver) =>
        string.Join("\n", driver.GetRunResult().GeneratedTrees.OrderBy(tree => tree.FilePath, System.StringComparer.Ordinal).Select(tree => tree.GetText().ToString()));

    private static IEnumerable<IncrementalStepRunReason> Reasons(GeneratorDriverRunResult result, string step) =>
        result.Results[0].TrackedSteps.TryGetValue(step, out var runs)
            ? runs.SelectMany(run => run.Outputs).Select(output => output.Reason)
            : [];
}

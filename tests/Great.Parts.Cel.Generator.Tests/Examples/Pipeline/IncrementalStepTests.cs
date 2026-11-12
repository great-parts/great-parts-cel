using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Pipeline;

// The steps of the pipeline are cached (trackIncrementalGeneratorSteps): the tracked names are ContextRead, which reads
// everything about one context, ContextModel, the model that the writer takes, and Diagnostics, the places that move when a
// file above the context changes. What an editor does with the application: nothing changes, an unrelated file changes, a type
// that the context reaches changes, lines are inserted above the context.
public sealed class IncrementalStepTests
{
    private const string Context = """
        using Great.Parts.Cel.Values;
        namespace Acme;
        [CelObject(typeof(Holder))]
        public partial class Ctx : CelTypeContext { }
        public class Holder
        {
            public long Kept { get; set; }
            public System.IO.Stream? Body { get; set; }
        }
        """;

    private const string Unrelated = "namespace Acme; public class Unrelated { public long V { get; set; } }\n";

    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    public void NothingChanged_EveryTrackedStepIsCachedOrUnchanged()
    {
        CSharpCompilation compilation = Build(Context, Unrelated);
        GeneratorDriver driver = GeneratorExample.Driver(track: true).RunGenerators(compilation);

        GeneratorDriverRunResult second = driver.RunGenerators(compilation).GetRunResult();

        foreach (string step in new[] { "ContextRead", "ContextModel", "Diagnostics" })
        {
            Assert.All(Reasons(second, step), reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, step + ": " + reason));
        }

        Assert.NotEmpty(Reasons(second, "ContextModel"));
    }

    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    public void ChangeOfAFileNoContextReads_LeavesTheStepsAfterTheReadCached()
    {
        CSharpCompilation first = Build(Context, Unrelated);
        GeneratorDriver driver = GeneratorExample.Driver(track: true).RunGenerators(first);
        SyntaxTree old = first.SyntaxTrees.Single(tree => tree.FilePath == "More0.cs");
        CSharpCompilation second = first.ReplaceSyntaxTree(old, old.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(Unrelated + "// an edit\n")));

        GeneratorDriverRunResult result = driver.RunGenerators(second).GetRunResult();

        foreach (string step in new[] { "ContextModel", "Diagnostics" })
        {
            Assert.All(Reasons(result, step), reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, step + ": " + reason));
        }
    }

    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    public void ChangeOfATypeTheContextReaches_MakesTheModelModified()
    {
        CSharpCompilation first = Build(Context, Unrelated);
        GeneratorDriver driver = GeneratorExample.Driver(track: true).RunGenerators(first);
        SyntaxTree old = first.SyntaxTrees.Single(tree => tree.FilePath == "Source.cs");
        string edited = Context.Replace("public long Kept", "public int Kept", System.StringComparison.Ordinal);
        CSharpCompilation second = first.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(edited, new CSharpParseOptions(LanguageVersion.CSharp12), path: "Source.cs"));

        GeneratorDriverRunResult result = driver.RunGenerators(second).GetRunResult();

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(result, "ContextModel"));
    }

    // Lines inserted above the context move the places of the diagnostics, which are an output of their own, and not the model:
    // the file of the context is not written again for a change of a line number.
    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    public void LinesInsertedAboveTheContext_ModifyTheDiagnosticsAndNotTheModel()
    {
        CSharpCompilation first = Build(Context, Unrelated);
        GeneratorDriver driver = GeneratorExample.Driver(track: true).RunGenerators(first);
        SyntaxTree old = first.SyntaxTrees.Single(tree => tree.FilePath == "Source.cs");
        string moved = "// one\n// two\n// three\n" + Context;
        CSharpCompilation second = first.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(moved, new CSharpParseOptions(LanguageVersion.CSharp12), path: "Source.cs"));

        GeneratorDriverRunResult result = driver.RunGenerators(second).GetRunResult();

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(result, "Diagnostics"));
        Assert.All(Reasons(result, "ContextModel"), reason => Assert.Equal(IncrementalStepRunReason.Unchanged, reason));
        Assert.Contains(GeneratorExample.Reported(result), diagnostic => diagnostic.Id == "GPCEL002" && diagnostic.Location.GetLineSpan().StartLinePosition.Line == 7 + 3);
    }

    private static CSharpCompilation Build(string source, string other) =>
        GeneratorExample.Compile(source, more: [other]);

    private static IEnumerable<IncrementalStepRunReason> Reasons(GeneratorDriverRunResult result, string step) =>
        result.Results.Single().TrackedSteps[step].SelectMany(run => run.Outputs).Select(output => output.Reason);
}

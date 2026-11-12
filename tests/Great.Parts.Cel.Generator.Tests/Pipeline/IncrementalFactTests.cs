using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Pipeline;

// The language version of the project is a fact of the compilation that the transform of ContextRead reads: a change of it changes what
// the read gives (GPCEL008 comes and goes, and the file is not written), with no step of its own for the fact.
public sealed class IncrementalFactTests
{
    private const string Context = """
        using Great.Parts.Cel.Values;
        namespace Acme;
        [CelObject(typeof(Holder))]
        public partial class Ctx : CelTypeContext { }
        public class Holder { public long Kept { get; set; } }
        """;

    [Fact]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    public void ChangeOfTheLanguageVersion_ChangesTheOutputOfTheRead()
    {
        CSharpCompilation first = GeneratorExample.Compile(Context);
        GeneratorDriver driver = GeneratorExample.Driver(track: true).RunGenerators(first, TestContext.Current.CancellationToken);
        SyntaxTree old = first.SyntaxTrees.Single();
        CSharpCompilation second = first.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(Context, new CSharpParseOptions(LanguageVersion.CSharp11), path: "Source.cs", cancellationToken: TestContext.Current.CancellationToken));

        GeneratorDriverRunResult result = driver.RunGenerators(second, TestContext.Current.CancellationToken).GetRunResult();

        Assert.Contains(IncrementalStepRunReason.Modified, result.Results[0].TrackedSteps["ContextRead"].SelectMany(step => step.Outputs).Select(output => output.Reason));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "GPCEL008");
        Assert.Empty(result.GeneratedTrees);
    }
}

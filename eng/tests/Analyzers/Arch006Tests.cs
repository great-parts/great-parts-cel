using System.Collections.Generic;
using System.Threading.Tasks;
using Great.Parts.Cel.Eng.Analyzers;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Analyzers;

[Trait("Requirement", "+R-16")]
public sealed class Arch006Tests
{
    private const string Types = """
        namespace Great.Parts.Cel.Values;

        public sealed class Policy
        {
        }

        internal sealed class Filtering
        {
        }
        """;

    [Fact]
    public async Task PublicTypeWithReservedName_IsReported()
    {
        IReadOnlyList<AnalyzerFinding> findings = await AnalyzerRunner.RunAsync(
            new PublicNameAnalyzer(),
            "Great.Parts.Cel",
            [new RepositoryFile("src/Great.Parts.Cel/Values/Policy.cs", Types)],
            [new RepositoryFile("docs/api-concepts.md", "# API concepts\n")],
            TestContext.Current.CancellationToken);

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH006", "src/Great.Parts.Cel/Values/Policy.cs", 3, 21, "public type name 'Policy' is reserved"));
    }

    [Fact]
    public async Task CompilationWithoutTheList_IsNotChecked()
    {
        IReadOnlyList<AnalyzerFinding> findings = await AnalyzerRunner.RunAsync(
            new PublicNameAnalyzer(),
            "Great.Parts.Cel.Tests",
            [new RepositoryFile("tests/Great.Parts.Cel.Tests/Policy.cs", Types)],
            [],
            TestContext.Current.CancellationToken);

        FindingAssert.None(findings);
    }
}

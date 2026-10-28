using System.Collections.Generic;
using System.Threading.Tasks;
using Great.Parts.Cel.Eng.Analyzers;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Analyzers;

[Trait("Requirement", "system-S-3")]
[Trait("Requirement", "+R-16")]
public sealed class Arch005Tests
{
    private const string LayersPath = "eng/layers.txt";

    private const string Layers = """
        # Namespace layers of the example.

        [Great.Parts.Cel]
        Great.Parts.Cel.Types ->
        Great.Parts.Cel.Values -> Great.Parts.Cel.Types
        Great.Parts.Cel.Evaluation -> Great.Parts.Cel.Values, Great.Parts.Cel.Types
        """;

    private const string Planner = """
        namespace Great.Parts.Cel.Evaluation;

        internal static class Planner
        {
            internal static int Depth => 1;
        }
        """;

    private const string ValuesToEvaluation = """
        using Great.Parts.Cel.Evaluation;

        namespace Great.Parts.Cel.Values;

        internal static class CelList
        {
            internal static int Depth => Planner.Depth;
        }
        """;

    [Fact]
    public async Task ReferenceOutsideTheRowOfItsNamespace_IsReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            "Great.Parts.Cel",
            Layers,
            new RepositoryFile("src/Great.Parts.Cel/Evaluation/Planner.cs", Planner),
            new RepositoryFile("src/Great.Parts.Cel/Values/CelList.cs", ValuesToEvaluation));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH005", "src/Great.Parts.Cel/Values/CelList.cs", 7, 34, "Great.Parts.Cel.Values must not reference Great.Parts.Cel.Evaluation (layer table eng/layers.txt)"));
    }

    [Fact]
    public async Task ReferenceListedInTheRow_IsNotReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            "Great.Parts.Cel",
            Layers,
            new RepositoryFile("src/Great.Parts.Cel/Values/CelList.cs", """
                namespace Great.Parts.Cel.Values;

                internal static class CelList
                {
                    internal static int Depth => 1;
                }
                """),
            new RepositoryFile("src/Great.Parts.Cel/Evaluation/Planner.cs", """
                using Great.Parts.Cel.Values;

                namespace Great.Parts.Cel.Evaluation;

                internal static class Planner
                {
                    internal static int Depth => CelList.Depth;
                }
                """));

        FindingAssert.None(findings);
    }

    [Fact]
    public async Task ReferenceToAHigherLayerInDocumentation_IsNotReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            "Great.Parts.Cel",
            Layers,
            new RepositoryFile("src/Great.Parts.Cel/Evaluation/Planner.cs", Planner),
            new RepositoryFile("src/Great.Parts.Cel/Values/CelList.cs", """
                using Great.Parts.Cel.Evaluation;

                namespace Great.Parts.Cel.Values;

                /// <summary>A list; <see cref="Planner"/> plans its evaluation.</summary>
                /// <seealso cref="Great.Parts.Cel.Evaluation.Planner.Depth"/>
                internal static class CelList
                {
                    /// <summary>The same depth as <see cref="Planner.Depth"/>.</summary>
                    internal static int Depth => 1;
                }
                """));

        FindingAssert.None(findings);
    }

    [Fact]
    public async Task NamespaceWithoutRow_IsReportedAtItsDeclaration()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            "Great.Parts.Cel",
            Layers,
            new RepositoryFile("src/Great.Parts.Cel/Syntax/Parser.cs", """
                namespace Great.Parts.Cel.Syntax;

                internal static class Parser
                {
                }
                """));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH005", "src/Great.Parts.Cel/Syntax/Parser.cs", 1, 11, "namespace Great.Parts.Cel.Syntax has no line in the layer table eng/layers.txt"));
    }

    [Fact]
    public async Task MalformedLineOfTheTable_IsReportedAtThatLine()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            "Great.Parts.Cel",
            Layers + "\nGreat.Parts.Cel.Syntax Great.Parts.Cel.Values\n",
            new RepositoryFile("src/Great.Parts.Cel/Evaluation/Planner.cs", Planner));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH005", LayersPath, 7, 1, "line of the layer table eng/layers.txt does not parse: a line is '<namespace> -> <namespace>, <namespace>, ...'"));
    }

    [Fact]
    public async Task AssemblyWithoutSection_IsNotChecked()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            "Great.Parts.Cel.Generator",
            Layers,
            new RepositoryFile("src/Great.Parts.Cel.Generator/Evaluation/Planner.cs", Planner),
            new RepositoryFile("src/Great.Parts.Cel.Generator/Values/CelList.cs", ValuesToEvaluation),
            new RepositoryFile("src/Great.Parts.Cel.Generator/Syntax/Parser.cs", """
                namespace Great.Parts.Cel.Syntax;

                internal static class Parser
                {
                }
                """));

        FindingAssert.None(findings);
    }

    private static Task<IReadOnlyList<Finding>> RunAsync(string assemblyName, string layers, params RepositoryFile[] sources) =>
        AnalyzerRunner.RunAsync(new NamespaceGraphAnalyzer(), assemblyName, sources, [new RepositoryFile(LayersPath, layers)], TestContext.Current.CancellationToken);
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Great.Parts.Cel.Eng.Analyzers;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Analyzers;

public sealed class Arch008Tests
{
    // Stands for a type generated from the cel-spec protos.
    private const string ExprValue = """
        namespace Cel.Expr;

        public sealed class Value
        {
            public static Value Default { get; } = new();
        }
        """;

    [Fact]
    public async Task QualifiedNameStartingWithCel_IsReported()
    {
        IReadOnlyList<AnalyzerFinding> findings = await RunAsync("""
            namespace Conformance;

            internal static class Runner
            {
                internal static object Create() => new Cel.Expr.Value();

                internal static object Member() => Cel.Expr.Value.Default;
            }
            """);

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH008", "tests/Conformance/Runner.cs", 5, 44, "qualified name 'Cel.Expr.Value' must use a using directive or global::"),
            FindingAssert.Error("ARCH008", "tests/Conformance/Runner.cs", 7, 40, "qualified name 'Cel.Expr.Value.Default' must use a using directive or global::"));
    }

    [Fact]
    public async Task QualifiedNameWithGlobalOrUsingDirective_IsNotReported()
    {
        IReadOnlyList<AnalyzerFinding> findings = await RunAsync("""
            using Cel.Expr;

            namespace Conformance;

            internal static class Runner
            {
                internal static object Create() => new global::Cel.Expr.Value();

                internal static object Member() => Value.Default;
            }
            """);

        FindingAssert.None(findings);
    }

    private static Task<IReadOnlyList<AnalyzerFinding>> RunAsync(string runner) =>
        AnalyzerRunner.RunAsync(
            new QualifiedNameAnalyzer(),
            "Great.Parts.Cel.Conformance",
            [new RepositoryFile("tests/Conformance/Value.cs", ExprValue), new RepositoryFile("tests/Conformance/Runner.cs", runner)],
            [],
            TestContext.Current.CancellationToken);
}

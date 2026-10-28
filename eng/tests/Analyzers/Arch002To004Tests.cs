using System.Collections.Generic;
using System.Threading.Tasks;
using Great.Parts.Cel.Eng.Analyzers;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Analyzers;

[Trait("Requirement", "+R-12")]
public sealed class Arch002To004Tests
{
    private const string ListPath = "docs/api-concepts.md";

    // Rows start at line 5.
    private const string Header = """
        # API concepts

        | Type | Namespace | cel-spec concept or requirement | Name and difference from the term |
        | :- | :- | :- | :- |

        """;

    private const string ProgramRow = "| `CelProgram<T>` | `Great.Parts.Cel` | `checked.proto` `CheckedExpr` | \"Program\" is the cel-go term |\n";

    private const string ValueRow = "| `CelValue` | `Great.Parts.Cel.Values` | `value.proto` `Value` | Same as the term, with the `Cel` prefix |\n";

    private const string Program = """
        namespace Great.Parts.Cel;

        public sealed class CelProgram<T>
        {
        }
        """;

    private const string Value = """
        namespace Great.Parts.Cel.Values;

        public readonly struct CelValue
        {
        }
        """;

    [Fact]
    public async Task EveryTypeHasRowAndEveryRowHasType_NothingIsReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + ProgramRow + ValueRow,
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program),
            new RepositoryFile("src/Great.Parts.Cel/Values/CelValue.cs", Value));

        FindingAssert.None(findings);
    }

    [Fact]
    public async Task PublicTypeWithoutRow_IsReportedAtItsDeclaration()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + ProgramRow,
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program),
            new RepositoryFile("src/Great.Parts.Cel/CelPolicy.cs", """
                namespace Great.Parts.Cel;

                public sealed class CelPolicy
                {
                    public sealed class Options
                    {
                    }
                }

                internal sealed class CelCache
                {
                }
                """));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH002", "src/Great.Parts.Cel/CelPolicy.cs", 3, 21, "public type 'Great.Parts.Cel.CelPolicy' is missing from the API concept list"),
            FindingAssert.Error("ARCH002", "src/Great.Parts.Cel/CelPolicy.cs", 5, 25, "public type 'Great.Parts.Cel.CelPolicy.Options' is missing from the API concept list"));
    }

    [Fact]
    public async Task RowWithoutPublicType_IsReportedAtTheRow()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + ProgramRow + "| `CelBindings` | `Great.Parts.Cel` | langdef \"Evaluation Environment\" | cel-spec term \"bindings\" |\n",
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program),
            new RepositoryFile("src/Great.Parts.Cel/CelBindings.cs", """
                namespace Great.Parts.Cel;

                internal sealed class CelBindings
                {
                }
                """));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH003", ListPath, 6, 1, "API concept list row 'Great.Parts.Cel.CelBindings' has no public type"));
    }

    [Fact]
    public async Task RowOfNamespaceOfAnotherAssembly_IsNotCheckedHere()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + ProgramRow + "| `CelProtobufTypes` | `Great.Parts.Cel.Protobuf` | protobuf message types | Not a cel-spec term |\n",
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program));

        FindingAssert.None(findings);
    }

    [Fact]
    public async Task RowWithEmptyConcept_IsReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + ProgramRow + "| `CelValue` | `Great.Parts.Cel.Values` |  | Same as the term, with the `Cel` prefix |\n",
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program),
            new RepositoryFile("src/Great.Parts.Cel/Values/CelValue.cs", Value));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH004", ListPath, 6, 1, "row 'Great.Parts.Cel.Values.CelValue': the cel-spec concept or requirement is empty"));
    }

    [Fact]
    public async Task NameDifferenceWithoutExplanation_IsReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + "| `CelProgram<T>` | `Great.Parts.Cel` | `checked.proto` `CheckedExpr` |  |\n",
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH004", ListPath, 5, 1, "row 'Great.Parts.Cel.CelProgram<T>': the name differs from the cel-spec term and the difference is not explained"));
    }

    [Fact]
    public async Task RowWithoutFourCells_IsReported()
    {
        IReadOnlyList<Finding> findings = await RunAsync(
            Header + "| `CelProgram<T>` | `Great.Parts.Cel` | `checked.proto` `CheckedExpr` |\n",
            new RepositoryFile("src/Great.Parts.Cel/CelProgram.cs", Program));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error("ARCH004", ListPath, 5, 1, "row 'Great.Parts.Cel.CelProgram<T>': the row has 3 cells instead of 4"));
    }

    [Fact]
    public async Task CompilationWithoutTheList_IsNotChecked()
    {
        IReadOnlyList<Finding> findings = await AnalyzerRunner.RunAsync(
            new ApiConceptListAnalyzer(),
            "Great.Parts.Cel.Tests",
            [new RepositoryFile("tests/Great.Parts.Cel.Tests/CelPolicyTests.cs", """
                namespace Great.Parts.Cel.Tests;

                public sealed class CelPolicyTests
                {
                }
                """)],
            [],
            TestContext.Current.CancellationToken);

        FindingAssert.None(findings);
    }

    private static Task<IReadOnlyList<Finding>> RunAsync(string list, params RepositoryFile[] sources) =>
        AnalyzerRunner.RunAsync(new ApiConceptListAnalyzer(), "Great.Parts.Cel", sources, [new RepositoryFile(ListPath, list)], TestContext.Current.CancellationToken);
}

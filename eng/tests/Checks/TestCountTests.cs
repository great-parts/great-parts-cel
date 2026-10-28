using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks test-count` (docs/design/eng-checks-artifacts.md, "test-count"). The reports are the real reports of the test project
// of the repository, one of the JIT run and one of the Native AOT run, with a [Fact] and the cases of a [Theory]; a test edits
// them with System.Xml.Linq. The command runs through Program, so the options and the exit codes are the ones of the command line.
// A JIT build with a PDB is compiled in memory with Roslyn and written to a temporary directory.
public sealed class TestCountTests
{
    private const string Where = "linux-x64, net11.0";
    private const string MapCase = "Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Map, number: 12)";
    private const string OptionalCase = "Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Optional, number: 17)";
    private const string FactName = "Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests.Stages_FollowThePipelineOrder";
    private const string TheoryFile = "tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs";
    private const string JitDll = "tests/Great.Parts.Cel.Tests/bin/Release/net11.0/Great.Parts.Cel.Tests.dll";

    // The first lines of CelTypeKindTests.cs: the attribute of the theory is at line 8, as in the reports.
    private const string TheorySource = """
        using Great.Parts.Cel.Types;
        using Xunit;

        namespace Great.Parts.Cel.Tests.Types;

        public sealed class CelTypeKindTests
        {
            [Theory]
            [InlineData(CelTypeKind.Map, 12)]
            public static void Kind_HasItsPublishedNumber(CelTypeKind kind, int number)
        """;

    // The same class as the JIT build has it: the method of the theory is at line 5, its opening brace at line 6.
    private const string TheoryBuildSource = """
        namespace Great.Parts.Cel.Tests.Types;

        public sealed class CelTypeKindTests
        {
            public static void Kind_HasItsPublishedNumber(int kind, int number)
            {
                System.GC.KeepAlive(kind + number);
            }
        }
        """;

    private const string OtherBuildSource = """
        namespace Great.Parts.Cel.Tests;

        public sealed class Outer
        {
            public sealed class Inner
            {
                public void NestedTest()
                {
                    System.GC.KeepAlive(this);
                }
            }

            public async System.Threading.Tasks.Task AsyncTest()
            {
                await System.Threading.Tasks.Task.Yield();
            }
        }
        """;

    private const string JitReport = """
        <assemblies schema-version="3" id="a557cdc4-590d-4e7f-b1ed-e39c449aa5fa" computer="64fca34abcf5" start-rtf="2026-10-28T14:30:34.1547369+00:00" finish-rtf="2026-10-28T14:30:34.3292164+00:00" timestamp="10/28/2026 14:30:34"><assembly environment="64-bit (x64) .NET 11.0.0-rc.2.26504.105 [collection-per-class, parallel (collections, 12 threads)]" id="ce919fac-4623-4ec0-815f-074c4da2112b" name="/repo/tests/Great.Parts.Cel.Tests/bin/Release/net11.0/Great.Parts.Cel.Tests.dll" run-date="2026-10-28" run-time="14:30:34" start-rtf="2026-10-28T14:30:34.1547369+00:00" test-framework="xUnit.net v3 4.0.2+0c7aa4991f (Native AOT)" target-framework=".NETCoreApp,Version=v11.0" errors="0" failed="0" finish-rtf="2026-10-28T14:30:34.3292164+00:00" not-run="0" passed="6" skipped="0" time="0.175" time-rtf="00:00:00.1750000" total="6"><collection name="Test collection for Great.Parts.Cel.Tests.Types.CelTypeKindTests (id: 243c856a52f45ab0cababd0201da1c275720e744ad96ebaa84d9875c5fad20d4)" id="7c837daa-7ed7-4a49-894a-5c845915ae95" failed="0" not-run="0" passed="5" skipped="0" time="0.023" time-rtf="00:00:00.0230000" total="5"><test id="1123cee4-7b0e-46c7-b8d2-96d0ac66c3e2" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Dyn, number: 0)" result="Pass" time="0.023" time-rtf="00:00:00.0230000" start-rtf="2026-10-28T14:30:34.2637345+00:00" finish-rtf="2026-10-28T14:30:34.3034080+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="07267343-abb9-4773-8449-6a20f960a6c5" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Bool, number: 2)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:30:34.3085912+00:00" finish-rtf="2026-10-28T14:30:34.3088165+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="b6bdecb4-0039-48a9-ac01-4cf356c54ced" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: String, number: 6)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:30:34.3088301+00:00" finish-rtf="2026-10-28T14:30:34.3088523+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="3a901ede-a8b3-4dc3-8f41-112053167b04" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Map, number: 12)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:30:34.3088593+00:00" finish-rtf="2026-10-28T14:30:34.3088780+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="c08c44d6-a999-4b1e-b094-2b83aa9d4261" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Optional, number: 17)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:30:34.3088845+00:00" finish-rtf="2026-10-28T14:30:34.3089008+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /></collection><collection name="Test collection for Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests (id: 8b9e548be8d9dd1979ca30e915785dad5370cf7b537043cd54462cb7a17e0d9e)" id="c35487f4-6f67-4c5c-ad99-35f730c09a91" failed="0" not-run="0" passed="1" skipped="0" time="0.027" time-rtf="00:00:00.0270000" total="1"><test id="bc1f154b-eb48-41ad-8887-f51c130a93b7" name="Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests.Stages_FollowThePipelineOrder" result="Pass" time="0.027" time-rtf="00:00:00.0270000" start-rtf="2026-10-28T14:30:34.2637473+00:00" finish-rtf="2026-10-28T14:30:34.3034211+00:00" type="Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests" method="Stages_FollowThePipelineOrder" source-file="/repo/tests/Great.Parts.Cel.Tests/Diagnostics/CelErrorStageTests.cs" source-line="8" /></collection></assembly></assemblies>
        """;

    private const string AotReport = """
        <assemblies schema-version="3" id="60a403da-2263-4c0d-bdd6-79235e73b589" computer="64fca34abcf5" start-rtf="2026-10-28T14:31:13.6753098+00:00" finish-rtf="2026-10-28T14:31:13.6888847+00:00" timestamp="10/28/2026 14:31:13"><assembly environment="64-bit (x64) .NET 11.0.0-rc.2.26504.105 [collection-per-class, parallel (collections, 12 threads)]" id="4458c805-f3f7-4851-a4d9-93cfef89e30d" name="/repo/artifacts/aot/Great.Parts.Cel.Tests/net11.0/Great.Parts.Cel.Tests" run-date="2026-10-28" run-time="14:31:13" start-rtf="2026-10-28T14:31:13.6753098+00:00" test-framework="xUnit.net v3 4.0.2+0c7aa4991f (Native AOT)" target-framework=".NETCoreApp,Version=v11.0" errors="0" failed="0" finish-rtf="2026-10-28T14:31:13.6888847+00:00" not-run="0" passed="6" skipped="0" time="0.013" time-rtf="00:00:00.0130000" total="6"><collection name="Test collection for Great.Parts.Cel.Tests.Types.CelTypeKindTests (id: 243c856a52f45ab0cababd0201da1c275720e744ad96ebaa84d9875c5fad20d4)" id="fdbc17de-e300-4ce0-907b-f47c3af6b0da" failed="0" not-run="0" passed="5" skipped="0" time="0.000" time-rtf="00:00:00" total="5"><test id="8a93a166-922b-4f22-bfd7-9b66b9016afa" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Dyn, number: 0)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:31:13.6887482+00:00" finish-rtf="2026-10-28T14:31:13.6887808+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="a5ea17b7-2531-4235-9370-dac2fccdd5a4" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Bool, number: 2)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:31:13.6887879+00:00" finish-rtf="2026-10-28T14:31:13.6887946+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="09383cfb-375c-4b7b-ad42-f0f38b978b25" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: String, number: 6)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:31:13.6888069+00:00" finish-rtf="2026-10-28T14:31:13.6888121+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="a21c153c-68be-4ddb-ab2b-b92dc4e78964" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Map, number: 12)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:31:13.6888144+00:00" finish-rtf="2026-10-28T14:31:13.6888219+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /><test id="e0477681-befa-4c3d-87d7-44702a9de90f" name="Great.Parts.Cel.Tests.Types.CelTypeKindTests.Kind_HasItsPublishedNumber(kind: Optional, number: 17)" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:31:13.6888238+00:00" finish-rtf="2026-10-28T14:31:13.6888315+00:00" type="Great.Parts.Cel.Tests.Types.CelTypeKindTests" method="Kind_HasItsPublishedNumber" source-file="/repo/tests/Great.Parts.Cel.Tests/Types/CelTypeKindTests.cs" source-line="8" /></collection><collection name="Test collection for Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests (id: 8b9e548be8d9dd1979ca30e915785dad5370cf7b537043cd54462cb7a17e0d9e)" id="f9cf4bfd-c687-4e8c-8b60-f34f3e88a67f" failed="0" not-run="0" passed="1" skipped="0" time="0.000" time-rtf="00:00:00" total="1"><test id="cb688825-f9bd-49ea-9f18-44564ddd225f" name="Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests.Stages_FollowThePipelineOrder" result="Pass" time="0" time-rtf="00:00:00.0000000" start-rtf="2026-10-28T14:31:13.6815666+00:00" finish-rtf="2026-10-28T14:31:13.6818593+00:00" type="Great.Parts.Cel.Tests.Diagnostics.CelErrorStageTests" method="Stages_FollowThePipelineOrder" source-file="/repo/tests/Great.Parts.Cel.Tests/Diagnostics/CelErrorStageTests.cs" source-line="8" /></collection></assembly></assemblies>
        """;

    [Fact]
    public void TestCountCommand_IsInTheRegistryAndTakesNoArgument()
    {
        ICheckCommand command = Assert.Single(CommandRegistry.All, candidate => candidate.Name == "test-count");

        Assert.Null(command.ArgumentName);
    }

    [Fact]
    public void RealReportsOfTheSameTests_HaveNoFindings()
    {
        using TempDirectory root = new();

        Result result = Compare(root, JitReport, AotReport);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Eng810_ACaseOnlyInTheJitReport_IsReportedAtTheSourceOfTheReport()
    {
        using TempDirectory root = new();
        root.Write(TheoryFile, TheorySource);
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());

        Result result = Compare(root, JitReport, aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng810, TheoryFile, 8, 5, $"test '{MapCase}' ran under JIT but not under Native AOT ({Where})"));
    }

    [Fact]
    public void Eng811_ATestOnlyInTheNativeAotReport_IsReportedAtTheSourceOfTheReport()
    {
        using TempDirectory root = new();
        root.Write("tests/Great.Parts.Cel.Tests/Extra/ExtraTests.cs", "namespace Extra;\n\npublic sealed class ExtraTests\n{\n        public void Added() { }\n}\n");
        string aot = Edit(AotReport, document => AddTest(document, "Extra.ExtraTests", "Added", "/repo/tests/Great.Parts.Cel.Tests/Extra/ExtraTests.cs", 5));

        Result result = Compare(root, JitReport, aot);

        AssertErrors(
            result,
            Error(
                EngCodes.Eng811,
                "tests/Great.Parts.Cel.Tests/Extra/ExtraTests.cs",
                5,
                9,
                $"test 'Extra.ExtraTests.Added' ran under Native AOT but not under JIT ({Where})"));
    }

    [Fact]
    public void MissingAndExtraTests_GiveBothCodesInOneRun()
    {
        using TempDirectory root = new();
        root.Write(TheoryFile, TheorySource);
        string jit = Edit(JitReport, document => AddTest(document, "Only.InJit", "Method", "/repo/only/InJit.cs", 3));
        string aot = Edit(AotReport, document => AddTest(document, "Only.InAot", "Method", "/repo/only/InAot.cs", 4));

        Result result = Compare(root, jit, aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng810, "only/InJit.cs", 3, 1, $"test 'Only.InJit.Method' ran under JIT but not under Native AOT ({Where})"),
            Error(EngCodes.Eng811, "only/InAot.cs", 4, 1, $"test 'Only.InAot.Method' ran under Native AOT but not under JIT ({Where})"));
    }

    [Fact]
    public void CasesOfATheoryWithOtherArguments_AreOtherTests()
    {
        using TempDirectory root = new();
        string aot = Edit(AotReport, document => Test(document, "number: 17").SetAttributeValue("name", OptionalCase.Replace("number: 17", "number: 18", StringComparison.Ordinal)));

        Result result = Compare(root, JitReport, aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng810, TheoryFile, 8, 1, $"test '{OptionalCase}' ran under JIT but not under Native AOT ({Where})"),
            Error(
                EngCodes.Eng811,
                TheoryFile,
                8,
                1,
                $"test '{OptionalCase.Replace("number: 17", "number: 18", StringComparison.Ordinal)}' ran under Native AOT but not under JIT ({Where})"));
    }

    [Fact]
    public void TestWithTheSameNameInAnotherType_IsAnotherTest()
    {
        using TempDirectory root = new();
        string aot = Edit(AotReport, document => Test(document, "Stages_Follow").SetAttributeValue("type", "Other.Type"));

        Result result = Compare(root, JitReport, aot);

        Assert.Equal([EngCodes.Eng810, EngCodes.Eng811], result.Lines.Select(line => Finding.TryParse(line, out Finding finding) ? finding.Code : line).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void FailedAndSkippedTests_AreStillTestsThatRan()
    {
        using TempDirectory root = new();
        string jit = Edit(JitReport, document => Test(document, "Stages_Follow").SetAttributeValue("result", "Fail"));
        string aot = Edit(AotReport, document => Test(document, "Stages_Follow").SetAttributeValue("result", "Skip"));

        Result result = Compare(root, jit, aot);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void NotRunTests_AreNotCounted()
    {
        using TempDirectory root = new();
        string jit = Edit(JitReport, document => Test(document, "Stages_Follow").SetAttributeValue("result", "NotRun"));
        string aot = Edit(AotReport, document => Test(document, "number: 12").SetAttributeValue("result", "NotRun"));

        Result result = Compare(root, jit, aot);

        // The JIT report has no counted FactName, the Native AOT report has it; the Map case is counted only in the JIT report.
        AssertErrors(
            result,
            Error(EngCodes.Eng810, TheoryFile, 8, 1, $"test '{MapCase}' ran under JIT but not under Native AOT ({Where})"),
            Error(EngCodes.Eng811, "tests/Great.Parts.Cel.Tests/Diagnostics/CelErrorStageTests.cs", 8, 1, $"test '{FactName}' ran under Native AOT but not under JIT ({Where})"));
    }

    [Fact]
    public void ReportWithNotRunTestOnBothSides_HasNoFindings()
    {
        using TempDirectory root = new();
        string jit = Edit(JitReport, document => Test(document, "Stages_Follow").SetAttributeValue("result", "NotRun"));
        string aot = Edit(AotReport, document => Test(document, "Stages_Follow").Remove());

        Result result = Compare(root, jit, aot);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void SourceRootOfADeterministicBuild_IsAcceptedAsTheRootOfTheRepository()
    {
        using TempDirectory root = new();
        root.Write(TheoryFile, TheorySource);
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());

        Result result = Compare(root, JitReport, aot, "/_/");

        AssertErrors(
            result,
            Error(EngCodes.Eng810, TheoryFile, 8, 5, $"test '{MapCase}' ran under JIT but not under Native AOT ({Where})"));
    }

    [Fact]
    public void SourceFileOutsideTheRoot_KeepsItsPathAndColumnOne()
    {
        using TempDirectory root = new();
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());

        Result result = Compare(root, JitReport.Replace("/repo/", "/elsewhere/", StringComparison.Ordinal), aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng810, "/elsewhere/" + TheoryFile, 8, 1, $"test '{MapCase}' ran under JIT but not under Native AOT ({Where})"));
    }

    [Fact]
    public void SourceFileThatDoesNotExist_GivesColumnOne()
    {
        using TempDirectory root = new();
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());

        Result result = Compare(root, JitReport, aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng810, TheoryFile, 8, 1, $"test '{MapCase}' ran under JIT but not under Native AOT ({Where})"));
    }

    [Fact]
    public void ReportWithoutSource_TakesThePlaceFromThePdbOfTheJitBuild()
    {
        using TempDirectory root = new();
        root.Write(TheoryFile, TheoryBuildSource);
        Build(root, JitDll, "/_/" + TheoryFile, TheoryBuildSource);
        string jit = Edit(JitReport, document => WithoutSource(document, root, JitDll));
        string aot = Edit(AotReport, document =>
        {
            WithoutSource(document, root, "unused");
            Test(document, "number: 12").Remove();
        });

        Result result = Compare(root, jit, aot);

        // The first sequence point of the method is its opening brace; the build knows the source as /_/<path>, the column is read from the file.
        AssertErrors(
            result,
            Error(EngCodes.Eng810, TheoryFile, 6, 5, $"test '{MapCase}' ran under JIT but not under Native AOT ({Where})"));
    }

    [Fact]
    public void ReportWithoutSource_TheMethodOfAnAotOnlyTestIsFoundInThePdbOfTheJitBuildToo()
    {
        using TempDirectory root = new();
        root.Write(TheoryFile, TheoryBuildSource);
        Build(root, JitDll, "/_/" + TheoryFile, TheoryBuildSource);
        string jit = Edit(JitReport, document =>
        {
            WithoutSource(document, root, JitDll);
            Test(document, "number: 12").Remove();
        });
        string aot = Edit(AotReport, document => WithoutSource(document, root, "unused"));

        Result result = Compare(root, jit, aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng811, TheoryFile, 6, 5, $"test '{MapCase}' ran under Native AOT but not under JIT ({Where})"));
    }

    [Fact]
    public void ReportWithoutSource_AsyncAndNestedMethodsAreFoundInThePdb()
    {
        using TempDirectory root = new();
        root.Write("tests/Outer.cs", OtherBuildSource);
        Build(root, JitDll, "/_/tests/Outer.cs", OtherBuildSource);
        string jit = Edit(JitReport, document =>
        {
            WithoutSource(document, root, JitDll);
            AddTest(document, "Great.Parts.Cel.Tests.Outer+Inner", "NestedTest", null, 0);
            AddTest(document, "Great.Parts.Cel.Tests.Outer", "AsyncTest", null, 0);
        });
        string aot = Edit(AotReport, document => WithoutSource(document, root, "unused"));

        Result result = Compare(root, jit, aot);

        AssertErrors(
            result,
            Error(EngCodes.Eng810, "tests/Outer.cs", 8, 9, $"test 'Great.Parts.Cel.Tests.Outer+Inner.NestedTest' ran under JIT but not under Native AOT ({Where})"),
            Error(EngCodes.Eng810, "tests/Outer.cs", 14, 5, $"test 'Great.Parts.Cel.Tests.Outer.AsyncTest' ran under JIT but not under Native AOT ({Where})"));
    }

    [Fact]
    public void ReportWithoutSource_MethodThatIsNotInThePdb_IsAtTheTestElementOfTheReport()
    {
        using TempDirectory root = new();
        Build(root, JitDll, "/_/tests/Outer.cs", OtherBuildSource);
        string jit = Edit(JitReport, document => WithoutSource(document, root, JitDll));
        string aot = Edit(AotReport, document =>
        {
            WithoutSource(document, root, "unused");
            Test(document, "number: 12").Remove();
        });

        Result result = Compare(root, jit, aot);

        Finding finding = Assert.Single(result.Lines.Select(line => Finding.TryParse(line, out Finding parsed) ? parsed : throw new InvalidOperationException(line)));
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(EngCodes.Eng810, finding.Code);
        Assert.Equal("artifacts/test-results/jit/report.xml", finding.Path);
        Assert.Equal(1, finding.Column);
        // Indented by Edit: a root element, an assembly and a collection come before the test elements.
        Assert.True(finding.Line > 3, finding.ToString());
        Assert.Equal(TestElementLine(root, "jit", "number: 12"), finding.Line);
    }

    [Fact]
    public void ReportWithSource_ReadsNeitherTheAssemblyNorItsPdb()
    {
        using TempDirectory root = new();
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());

        // The assembly of the report does not exist in the temporary directory.
        Result result = Compare(root, JitReport, aot);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void ReportWithoutSource_AssemblyThatDoesNotExist_IsExitCodeTwoAndNamesTheFile()
    {
        using TempDirectory root = new();

        Result result = CompareNeedingThePdb(root);

        AssertInputError(result, "the assembly of the JIT run cannot be read", "Great.Parts.Cel.Tests.dll");
    }

    [Fact]
    public void ReportWithoutSource_PdbThatDoesNotExist_IsExitCodeTwoAndNamesTheFile()
    {
        using TempDirectory root = new();
        string dll = Build(root, JitDll, "/_/" + TheoryFile, TheoryBuildSource);
        File.Delete(Path.ChangeExtension(dll, ".pdb"));

        Result result = CompareNeedingThePdb(root);

        AssertInputError(result, "the PDB of the JIT build cannot be read", "Great.Parts.Cel.Tests.pdb");
    }

    [Fact]
    public void ReportWithoutSource_PdbThatIsNotAPortablePdb_IsExitCodeTwo()
    {
        using TempDirectory root = new();
        string dll = Build(root, JitDll, "/_/" + TheoryFile, TheoryBuildSource);
        File.WriteAllText(Path.ChangeExtension(dll, ".pdb"), "not a pdb");

        Result result = CompareNeedingThePdb(root);

        AssertInputError(result, "Great.Parts.Cel.Tests.dll");
    }

    [Fact]
    public void ReportWithoutSourceAndWithoutAssemblyName_IsExitCodeTwo()
    {
        using TempDirectory root = new();
        string jit = Edit(JitReport, document =>
        {
            WithoutSource(document, root, JitDll);
            document.Descendants("assembly").Single().SetAttributeValue("name", null);
        });
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());

        Result result = Compare(root, jit, aot);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("the JIT report has no <assembly> element with a name", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--jit")]
    [InlineData("--aot")]
    [InlineData("--where")]
    public static void MissingOption_IsExitCodeTwoAndNamesTheOption(string missing)
    {
        using TempDirectory root = new();
        (string jit, string aot) = Write(root, JitReport, AotReport, "/repo/");
        Dictionary<string, string> options = new(StringComparer.Ordinal) { ["--jit"] = jit, ["--aot"] = aot, ["--where"] = Where };
        options.Remove(missing);

        Result result = Run(root, ["test-count", .. options.SelectMany(pair => new[] { pair.Key, pair.Value })]);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains(missing, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void NoOption_IsExitCodeTwo()
    {
        using TempDirectory root = new();

        Result result = Run(root, ["test-count"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--jit", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("jit")]
    [InlineData("aot")]
    public static void ReportThatDoesNotExist_IsExitCodeTwoAndNamesTheFile(string which)
    {
        using TempDirectory root = new();
        (string jit, string aot) = Write(root, JitReport, AotReport, "/repo/");
        File.Delete(which == "jit" ? jit : aot);

        Result result = Run(root, ["test-count", "--jit", jit, "--aot", aot, "--where", Where]);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("cannot be read", result.Error, StringComparison.Ordinal);
        Assert.Contains("report.xml", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<assemblies><assembly name=\"a\"")]
    [InlineData("<results><test type=\"T\" name=\"N\" /></results>")]
    [InlineData("<assemblies><assembly name=\"a\"><collection><test type=\"T\" /></collection></assembly></assemblies>")]
    public static void ReportThatIsNotAnXunitReport_IsExitCodeTwo(string content)
    {
        using TempDirectory root = new();
        (string jit, string aot) = Write(root, JitReport, AotReport, "/repo/");
        File.WriteAllText(aot, content);

        Result result = Run(root, ["test-count", "--jit", jit, "--aot", aot, "--where", Where]);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("report.xml", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereTextWithALineBreak_IsOneLineOfTheOutput()
    {
        using TempDirectory root = new();
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());
        (string jit, string aotPath) = Write(root, JitReport, aot, "/repo/");

        Result result = Run(root, ["test-count", "--jit", jit, "--aot", aotPath, "--where", "win-x64,\nnet10.0"]);

        string line = Assert.Single(result.Lines);
        Assert.Matches(Finding.Pattern, line);
        Assert.EndsWith("(win-x64, net10.0)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativePathsOfTheReports_AreTakenFromTheWorkingDirectory()
    {
        using TempDirectory root = new();
        root.Write("work/jit.xml", JitReport);
        root.Write("work/aot.xml", Edit(AotReport, document => Test(document, "number: 12").Remove()));
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new() { NewLine = "\n" };

        int exitCode = Program.Run(
            ["test-count", "--jit", "jit.xml", "--aot", "aot.xml", "--where", Where, "--root", root.Path],
            [new TestCountCommand()],
            Path.Combine(root.Path, "work"),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("ENG810", output.ToString(), StringComparison.Ordinal);
    }

    // The report written under the root with the root of the build of the run replaced: "/repo/" is the root in the container.
    private static (string Jit, string Aot) Write(TempDirectory root, string jit, string aot, string sourceRoot)
    {
        string prefix = sourceRoot == "/repo/" ? root.Path.Replace('\\', '/') + "/" : sourceRoot;
        root.Write("artifacts/test-results/jit/report.xml", jit.Replace("/repo/", prefix, StringComparison.Ordinal));
        root.Write("artifacts/test-results/aot/report.xml", aot.Replace("/repo/", prefix, StringComparison.Ordinal));
        return (Path.Combine(root.Path, "artifacts/test-results/jit/report.xml"), Path.Combine(root.Path, "artifacts/test-results/aot/report.xml"));
    }

    private static Result Compare(TempDirectory root, string jit, string aot, string sourceRoot = "/repo/")
    {
        (string jitPath, string aotPath) = Write(root, jit, aot, sourceRoot);
        return Run(root, ["test-count", "--jit", jitPath, "--aot", aotPath, "--where", Where]);
    }

    private static Result Run(TempDirectory root, IReadOnlyList<string> args)
    {
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new() { NewLine = "\n" };
        int exitCode = Program.Run([.. args, "--root", root.Path], [new TestCountCommand()], root.Path, output, error);
        return new Result(exitCode, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString());
    }

    private static void AssertErrors(Result result, params Finding[] expected)
    {
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(1, result.ExitCode);
        foreach (string line in result.Lines)
        {
            Assert.Matches(Finding.Pattern, line);
        }

        Assert.Equal(expected.Select(finding => finding.ToString()).Order(StringComparer.Ordinal), result.Lines.Order(StringComparer.Ordinal));
    }

    // The JIT report has no source and the Map case is missing under Native AOT: the place of the finding needs the PDB.
    private static Result CompareNeedingThePdb(TempDirectory root)
    {
        string jit = Edit(JitReport, document => WithoutSource(document, root, JitDll));
        string aot = Edit(AotReport, document => Test(document, "number: 12").Remove());
        return Compare(root, jit, aot);
    }

    private static void AssertInputError(Result result, params string[] messageParts)
    {
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        foreach (string part in messageParts)
        {
            Assert.Contains(part, result.Error, StringComparison.Ordinal);
        }
    }

    private static Finding Error(string code, string path, int line, int column, string message) => FindingAssert.Error(code, path, line, column, message);

    // The text of a report after the edit, indented, so every <test> element has its own line.
    private static string Edit(string report, Action<XElement> edit)
    {
        XDocument document = XDocument.Parse(report);
        XElement root = document.Root ?? throw new InvalidOperationException("The report has no root element.");
        edit(root);
        return document.ToString();
    }

    private static XElement Test(XElement root, string nameFragment) =>
        root.Descendants("test").Single(test => test.Attribute("name")?.Value.Contains(nameFragment, StringComparison.Ordinal) == true);

    private static void AddTest(XElement root, string type, string method, string? sourceFile, int sourceLine)
    {
        XElement test = new(
            "test",
            new XAttribute("id", Guid.NewGuid().ToString()),
            new XAttribute("name", type + "." + method),
            new XAttribute("result", "Pass"),
            new XAttribute("type", type),
            new XAttribute("method", method));
        if (sourceFile is not null)
        {
            test.Add(new XAttribute("source-file", sourceFile), new XAttribute("source-line", sourceLine));
        }

        root.Descendants("collection").First().Add(test);
    }

    // The report of a run that wrote no source: the tests lose source-file and source-line, the assembly gets the built file.
    private static void WithoutSource(XElement root, TempDirectory directory, string assembly)
    {
        foreach (XElement test in root.Descendants("test"))
        {
            test.SetAttributeValue("source-file", null);
            test.SetAttributeValue("source-line", null);
        }

        root.Descendants("assembly").Single().SetAttributeValue("name", Path.Combine(directory.Path, assembly));
    }

    private static int TestElementLine(TempDirectory root, string run, string nameFragment)
    {
        string[] lines = File.ReadAllLines(Path.Combine(root.Path, "artifacts/test-results", run, "report.xml"));
        return Array.FindIndex(lines, line => line.Contains("<test ", StringComparison.Ordinal) && line.Contains(nameFragment, StringComparison.Ordinal)) + 1;
    }

    // Compiles the source into a debug assembly with a portable PDB next to it; the sequence points name the source as sourcePath.
    private static string Build(TempDirectory root, string relativeDll, string sourcePath, string source)
    {
        CSharpCompilation compilation = Compile(Path.GetFileNameWithoutExtension(relativeDll), sourcePath, source);
        string dll = Path.Combine(root.Path, relativeDll);
        Directory.CreateDirectory(Path.GetDirectoryName(dll) ?? throw new InvalidOperationException("The assembly has no directory."));
        using (FileStream dllStream = File.Create(dll))
        using (FileStream pdbStream = File.Create(Path.ChangeExtension(dll, ".pdb")))
        {
            EmitResult result = compilation.Emit(dllStream, pdbStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }

        return dll;
    }

    private static CSharpCompilation Compile(string assemblyName, string sourcePath, string source) => CSharpCompilation.Create(
        assemblyName,
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14), sourcePath, Encoding.UTF8)],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));

    private sealed record Result(int ExitCode, string[] Lines, string Error);
}

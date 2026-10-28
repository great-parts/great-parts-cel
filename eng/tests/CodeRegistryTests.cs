using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests;

public sealed class CodeRegistryTests
{
    private static readonly RegisteredCode[] Codes =
    [
        new("ENG601", "Eng601"),
        new("ENG604", "Eng604"),
        new("ARCH001", null),
    ];

    [Fact]
    public void EveryCodeHasViolationTest()
    {
        string root = RepositoryRoot.Find();
        IReadOnlyList<RegisteredCode> engCodes = CodeRegistry.EngCodesOf(typeof(EngCodes));
        IReadOnlyList<RegisteredCode> archCodes = CodeRegistry.ArchCodesIn(Directory
            .EnumerateFiles(Path.Combine(root, "eng", "analyzers"), "AnalyzerReleases.*.md")
            .Select(File.ReadAllText));
        IEnumerable<string> sources = CodeRegistry.TestFiles(root).Select(File.ReadAllText);

        // The release files hold ARCH001-ARCH008: an empty column would make the test pass without checking anything.
        Assert.Equal(
            ["ARCH001", "ARCH002", "ARCH003", "ARCH004", "ARCH005", "ARCH006", "ARCH007", "ARCH008"],
            archCodes.Select(code => code.Code).Order(System.StringComparer.Ordinal));
        Assert.Empty(CodeRegistry.CodesWithoutTest([.. engCodes, .. archCodes], sources));
    }

    [Fact]
    public void EngCodesValues_AreEngAndThreeDigitsOfTheFieldNameAndUnique()
    {
        Assert.Empty(CodeRegistry.ValueProblems(CodeRegistry.EngCodesOf(typeof(EngCodes))));
    }

    [Fact]
    public void EngCodesValues_ViolatingFields_AreReported()
    {
        IReadOnlyList<string> problems = CodeRegistry.ValueProblems(
        [
            new("ENG601", "Eng601"),
            new("ENG62", "Eng62"),
            new("ENG603", "Eng604"),
            new("ENG605", "Eng605"),
            new("ENG605", "Eng605Again"),
        ]);

        Assert.Equal(
            [
                "Eng62: the value ENG62 is not ENG and three digits",
                "Eng604: the value ENG603 is not the field name in upper case",
                "Eng605Again: the value ENG605 is not the field name in upper case",
                "ENG605: the value occurs 2 times",
            ],
            problems);
    }

    [Fact]
    public void CodeWithoutTest_GivesHasNoTest()
    {
        IReadOnlyList<string> problems = CodeRegistry.CodesWithoutTest(
            Codes,
            [
                """
                public sealed class SomeTests
                {
                    public void Test() => Expect(EngCodes.Eng601);
                }
                """,
            ]);

        Assert.Equal(["ENG604 has no test", "ARCH001 has no test"], problems);
    }

    [Fact]
    public void CodeInComment_IsNotATest()
    {
        IReadOnlyList<string> problems = CodeRegistry.CodesWithoutTest(
            Codes,
            [
                """
                // ENG601 ENG604 ARCH001 Eng601 "ENG601"
                /* "ENG604" and Eng604 and "ARCH001" */
                /// <summary>Checks ENG601 and Eng604 and "ARCH001".</summary>
                public sealed class SomeTests;
                """,
            ]);

        Assert.Equal(["ENG601 has no test", "ENG604 has no test", "ARCH001 has no test"], problems);
    }

    [Fact]
    public void CodeAsStringLiteralOrFieldIdentifier_IsATest()
    {
        IReadOnlyList<string> problems = CodeRegistry.CodesWithoutTest(
            Codes,
            [
                """public sealed class FirstTests { public string A => "ENG601"; }""",
                """public sealed class SecondTests { public string B => EngCodes.Eng604; public string C => "ARCH001"; }""",
            ]);

        Assert.Empty(problems);
    }

    [Fact]
    public void CodeUnderPreprocessorDirective_IsATest()
    {
        IReadOnlyList<string> problems = CodeRegistry.CodesWithoutTest(
            Codes,
            [
                """
                public sealed class SomeTests
                {
                #if NET99_0_OR_GREATER
                    public string A => "ENG601";
                #else
                    public string B => EngCodes.Eng604;
                #endif
                    public string C => "ARCH001";
                }
                """,
            ]);

        Assert.Empty(problems);
    }

    [Fact]
    public void ArchCodes_AreTheRuleIdColumn()
    {
        IReadOnlyList<RegisteredCode> codes = CodeRegistry.ArchCodesIn(
        [
            """
            ; Shipped rules.
            """,
            """
            ### New Rules

            Rule ID | Category | Severity | Notes
            --------|----------|----------|-------
            ARCH001 | Architecture | Error | ARCH009: a code in the notes is not a rule
            ARCH002 | Architecture | Error | text
            """,
        ]);

        Assert.Equal(["ARCH001", "ARCH002"], codes.Select(code => code.Code));
    }

    [Fact]
    public void TestFiles_AreTheTestsOfTheRepositoryExceptTheRegistryTest()
    {
        string root = RepositoryRoot.Find();

        IReadOnlyList<string> files = CodeRegistry.TestFiles(root);

        Assert.Contains(files, file => file.EndsWith("Arch001Tests.cs", System.StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.EndsWith("CodeRegistryTests.cs", System.StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.EndsWith("AnalyzerRunner.cs", System.StringComparison.Ordinal));
    }
}

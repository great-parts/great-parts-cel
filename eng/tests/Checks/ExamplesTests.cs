using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks examples` (docs/design/eng-docs.md, "examples"). The three cases of the examples check and the reading of the examples
// are tested here; the option skip as a form of `suppressions` is tested in SuppressionsTests. A test that builds writes the files of
// its example into a temporary repository with two small libraries in the places of the package projects, so the examples are built and
// run for real: these tests take longer than the others. The tests that only read the examples or compare output do not build anything.
// The command runs through Program, so the options and the exit codes are the ones of the command line.
public sealed class ExamplesTests
{
    private const string ReadmePath = "README.md";
    private const string ProjectFile = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0;net11.0</TargetFrameworks></PropertyGroup></Project>";

    private static readonly string[] CalculatorSource =
    [
        "namespace Great.Parts.Cel;",
        "",
        "public static class Calculator",
        "{",
        "    public static int Add(int left, int right) => left + right;",
        "}",
    ];

    // ---- The three cases of the examples check ----

    [Fact]
    public void Eng301_OutputThatDiffers_IsReportedAtTheCommentOfTheExpectedLine()
    {
        using TempDirectory root = new();
        string[] readme =
        [
            "# Calculator",
            "",
            "```csharp",
            "Console.WriteLine(Great.Parts.Cel.Calculator.Add(1, 2)); // Output: 4",
            "```",
        ];
        WriteLibraries(root);
        root.Write(ReadmePath, Text(readme));

        Result result = Check(root);

        (int line, int column) = Locate(readme, "// Output:");
        AssertFindings(result, Error(EngCodes.Eng301, ReadmePath, line, column, "example output '3' differs from expected '4'"));
    }

    [Fact]
    public void CompilerError_IsReportedWithItsCodeAtTheLineAndColumnOfTheMarkdown()
    {
        using TempDirectory root = new();
        string[] readme =
        [
            "# Calculator",
            "",
            "```csharp",
            "Console.WriteLine(env2);",
            "```",
        ];
        WriteLibraries(root);
        root.Write(ReadmePath, Text(readme));

        Result result = Check(root);

        (int line, int column) = Locate(readme, "env2");
        AssertFindings(
            result,
            Error("CS0103", ReadmePath, line, column, "The name 'env2' does not exist in the current context"));
    }

    [Fact]
    public void Eng302_ExceptionAndExitCode_AreReportedAtTheFirstLineOfTheExample()
    {
        using TempDirectory root = new();
        WriteLibraries(root);
        root.Write(
            ReadmePath,
            Text(
                "# Failures",
                "",
                "```csharp",
                "throw new InvalidOperationException(\"boom\");",
                "```",
                "",
                "```csharp",
                "Environment.Exit(3);",
                "```"));

        Result result = Check(root);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(2, result.Lines.Length);
        Assert.StartsWith("README.md(4,1): error ENG302: example ended with the exit code ", result.Lines[0], StringComparison.Ordinal);
        Assert.EndsWith(": Unhandled exception. System.InvalidOperationException: boom", result.Lines[0], StringComparison.Ordinal);
        Assert.Equal(Error(EngCodes.Eng302, ReadmePath, 8, 1, "example ended with the exit code 3: no message on standard error").ToString(), result.Lines[1]);
    }

    [Fact]
    public void Eng302_ExampleThatDoesNotEndInTime_IsStoppedAndReported()
    {
        using TempDirectory root = new();
        WriteLibraries(root);
        root.Write(ReadmePath, Text("# Waiting", "", "```csharp", "new ManualResetEventSlim().Wait();", "```"));

        Result result = Check(root, new ExamplesCommand(TimeSpan.FromSeconds(3)));

        AssertFindings(result, Error(EngCodes.Eng302, ReadmePath, 4, 1, "example did not end within 3 s"));
    }

    // ---- Output ----

    [Fact]
    public void Eng301_EveryWayThatTheOutputDiffers_IsReportedAtItsPlace()
    {
        using TempDirectory root = new();
        string[] readme =
        [
            "# Outputs",
            "",
            "```csharp",
            "Console.WriteLine(\"one\");",
            "Console.WriteLine(\"two\");",
            "```",
            "",
            "```text",
            "one",
            "three",
            "```",
            "",
            "```csharp",
            "Console.WriteLine(\"one\"); // Output: one",
            "Console.WriteLine(\"two\");",
            "```",
            "",
            "```csharp",
            "Console.WriteLine(\"one\");",
            "```",
            "",
            "```text",
            "one",
            "two",
            "```",
            "",
            "```csharp",
            "Console.WriteLine(\"shown nowhere\");",
            "```",
        ];
        WriteLibraries(root);
        root.Write(ReadmePath, Text(readme));

        Result result = Check(root);

        (int moreLine, int moreColumn) = Locate(readme, "// Output: one");
        AssertFindings(
            result,
            Error(EngCodes.Eng301, ReadmePath, 10, 1, "example output 'two' differs from expected 'three'"),
            Error(EngCodes.Eng301, ReadmePath, moreLine, moreColumn, "example output continues with 'two' after the last expected line 'one'"),
            Error(EngCodes.Eng301, ReadmePath, 24, 1, "example output ends before the expected line 'two'"),
            Error(EngCodes.Eng301, ReadmePath, 28, 1, "example prints 'shown nowhere' but shows no output"));
    }

    [Fact]
    public void ExamplesThatPrintWhatTheyShow_HaveNoFindings()
    {
        using TempDirectory root = new();
        WriteLibraries(root);
        root.Write(
            ReadmePath,
            Text(
                "# Examples",
                "",
                "```csharp",
                "Console.WriteLine(Great.Parts.Cel.Calculator.Add(1, 2)); // Output: 3",
                "Console.WriteLine(\"two\"); // Output: two",
                "```",
                "",
                "```csharp",
                "Console.WriteLine(\"a\");",
                "Console.WriteLine(\"b\");",
                "```",
                "",
                "```text",
                "a",
                "b",
                "```",
                "",
                "```csharp",
                "int total = Great.Parts.Cel.Calculator.Add(2, 3);",
                "if (total != 5)",
                "{",
                "    throw new InvalidOperationException();",
                "}",
                "```",
                "",
                "```csharp net11.0",
                "Console.WriteLine(Environment.Version.Major >= 11); // Output: True",
                "```",
                "",
                "```csharp skip: needs a server",
                "this is not C#",
                "```"));
        root.Write(
            "docs/guide.md",
            Text(
                "# Guide",
                "",
                "~~~csharp",
                "Console.WriteLine(\"guide\"); // Output: guide",
                "~~~"));
        root.Write(
            "src/Great.Parts.Cel/Documented.cs",
            Text(
                "namespace Great.Parts.Cel;",
                "",
                "/// <summary>Shows the library.</summary>",
                "/// <example>",
                "/// <code>",
                "/// int total = Great.Parts.Cel.Calculator.Add(2, 2);",
                "/// Console.WriteLine(total); // Output: 4",
                "/// </code>",
                "/// <code target=\"net11.0\">",
                "/// List&lt;int&gt; numbers = [1, 2];",
                "/// Console.WriteLine(numbers.Count); // Output: 2",
                "/// </code>",
                "/// <code><![CDATA[",
                "/// List<int> items = [1, 2, 3];",
                "/// Console.WriteLine(items.Count); // Output: 3",
                "/// ]]></code>",
                "/// <code skip=\"needs a server\">not C# either</code>",
                "/// </example>",
                "public static class Documented",
                "{",
                "}"));

        Result result = Check(root);

        AssertFindings(result);
    }

    [Fact]
    public void XmlDocumentationExamples_AreReportedAtTheirLinesAndColumnsInTheSourceFile()
    {
        using TempDirectory root = new();
        string[] source =
        [
            "namespace Great.Parts.Cel;",
            "",
            "/// <summary>Shows the library.</summary>",
            "/// <example>",
            "/// <code>",
            "/// int total = Great.Parts.Cel.Calculator.Add(2, 2);",
            "/// Console.WriteLine(total); // Output: 5",
            "/// </code>",
            "/// </example>",
            "public static class Documented",
            "{",
            "    /// <summary>Does nothing.</summary>",
            "    /// <code>",
            "    /// Console.WriteLine(missing);",
            "    /// </code>",
            "    public static void Nothing()",
            "    {",
            "    }",
            "}",
        ];
        WriteLibraries(root);
        root.Write("src/Great.Parts.Cel/Documented.cs", Text(source));

        Result result = Check(root);

        (int outputLine, int outputColumn) = Locate(source, "// Output:");
        (int errorLine, int errorColumn) = Locate(source, "missing");
        AssertFindings(
            result,
            Error(EngCodes.Eng301, "src/Great.Parts.Cel/Documented.cs", outputLine, outputColumn, "example output '4' differs from expected '5'"),
            Error("CS0103", "src/Great.Parts.Cel/Documented.cs", errorLine, errorColumn, "The name 'missing' does not exist in the current context"));
    }

    [Fact]
    public void AnExampleThatDoesNotCompile_DoesNotStopTheOthers()
    {
        using TempDirectory root = new();
        WriteLibraries(root);
        root.Write(
            ReadmePath,
            Text(
                "# Two",
                "",
                "```csharp",
                "Console.WriteLine(env2);",
                "```",
                "",
                "```csharp",
                "Console.WriteLine(\"a\"); // Output: b",
                "```"));

        Result result = Check(root);

        AssertFindings(
            result,
            Error("CS0103", ReadmePath, 4, 19, "The name 'env2' does not exist in the current context"),
            Error(EngCodes.Eng301, ReadmePath, 8, "Console.WriteLine(\"a\"); ".Length + 1, "example output 'a' differs from expected 'b'"));
    }

    // ---- Input ----

    [Fact]
    public void ExamplesCommand_IsInTheRegistryAndTakesNoArgument()
    {
        ICheckCommand command = Assert.Single(CommandRegistry.All, candidate => candidate.Name == "examples");

        Assert.Null(command.ArgumentName);
    }

    [Fact]
    public void APageWithoutCSharpExamples_NeedsNoProjectAndHasNoFindings()
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text("# Text", "", "```text", "hello", "```", "", "```json", "{}", "```"));

        Result result = Check(root);

        AssertFindings(result);
    }

    [Fact]
    public void SkippedExamples_AreNeitherBuiltNorRun()
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text("# Skipped", "", "```csharp skip: needs a server", "this is not C#", "```"));
        root.Write("src/A.cs", Text("/// <code skip=\"needs a server\">not C# either</code>", "public static class A { }"));

        Result result = Check(root);

        AssertFindings(result);
    }

    [Fact]
    public void ExamplesWithoutThePackageProjects_AreAnInputError()
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text("# Example", "", "```csharp", "Console.WriteLine(1);", "```"));

        Result result = Check(root);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal("eng/checks: the project src/Great.Parts.Cel/Great.Parts.Cel.csproj does not exist; the examples refer to it" + Environment.NewLine, result.Error);
    }

    [Fact]
    public void AFailingBuildOfTheLibrary_IsAnInputErrorThatNamesTheFirstError()
    {
        using TempDirectory root = new();
        WriteLibraries(root);
        root.Write("src/Great.Parts.Cel/Broken.cs", "namespace Great.Parts.Cel;\n\npublic static class Broken { public static int Value => missing; }\n");
        root.Write(ReadmePath, Text("# Example", "", "```csharp", "Console.WriteLine(1);", "```"));

        Result result = Check(root);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.StartsWith("eng/checks: the build of the examples failed with the exit code 1: CS0103: The name 'missing' does not exist in the current context", result.Error, StringComparison.Ordinal);
    }

    // ---- Reading the examples ----

    [Fact]
    public void MarkdownBlocks_AreReadWithTheirOptionsAndExpectedOutput()
    {
        using TempDirectory root = new();
        root.Write(
            ReadmePath,
            Text(
                "# Blocks",
                "",
                "```csharp",
                "Console.WriteLine(1); // Output: 1",
                "```",
                "",
                "```csharp net11.0",
                "Console.WriteLine(2);",
                "```",
                "",
                "```text",
                "2",
                "```",
                "",
                "```csharpx",
                "not an example",
                "```",
                "",
                "~~~csharp skip: uses the network",
                "Console.WriteLine(3);",
                "~~~"));

        IReadOnlyList<Example> examples = Read(root);

        Assert.Equal(3, examples.Count);
        Assert.Equal(("README.md", 4, 1, Example.DefaultTarget), (examples[0].Path, examples[0].FirstLine, examples[0].Lines.Count, examples[0].Target));
        Assert.Equal([new ExpectedLine("1", new Position(4, 23))], examples[0].Expected);
        Assert.Null(examples[0].Skip);
        Assert.Equal((8, Example.DecimalTarget), (examples[1].FirstLine, examples[1].Target));
        Assert.Equal([new ExpectedLine("2", new Position(12, 1))], examples[1].Expected);
        Assert.Equal(new ExampleSkip(new Position(19, 1), "uses the network", "'csharp skip'", "reason after the word skip in the same line"), examples[2].Skip);
    }

    [Fact]
    public void MarkdownBlock_TextBlockThatIsNotDirectlyAfter_IsNotTheExpectedOutput()
    {
        using TempDirectory root = new();
        root.Write(
            ReadmePath,
            Text(
                "```csharp",
                "Console.WriteLine(1);",
                "```",
                "",
                "Some words.",
                "",
                "```text",
                "1",
                "```",
                "",
                "```csharp",
                "Console.WriteLine(2); // Output: 2",
                "```",
                "",
                "```text",
                "not this",
                "```"));

        IReadOnlyList<Example> examples = Read(root);

        Assert.Equal(2, examples.Count);
        Assert.Empty(examples[0].Expected);
        Assert.Equal(["2"], examples[1].Expected.Select(line => line.Text));
    }

    [Fact]
    public void MarkdownBlock_InAListAndInALongerFence_IsReadAsCommonMarkReadsIt()
    {
        using TempDirectory root = new();
        root.Write(
            "docs/a.md",
            Text(
                "- Step:",
                "",
                "  ```csharp",
                "  Console.WriteLine(1);",
                "  ```",
                "",
                "  ```text",
                "  1",
                "  ```",
                "",
                "````markdown",
                "```csharp",
                "Console.WriteLine(2);",
                "```",
                "````"));

        IReadOnlyList<Example> examples = Read(root);

        Example example = Assert.Single(examples);
        Assert.Equal(["  Console.WriteLine(1);"], example.Lines);
        Assert.Equal([new ExpectedLine("1", new Position(8, 3))], example.Expected);
    }

    [Fact]
    public void MarkdownSkip_WithoutAColonOrAReason_IsASkipWithAnEmptyReason()
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text("```csharp skip", "x", "```", "", "```csharp skip:", "y", "```", "", "```csharp skipper", "z", "```"));

        IReadOnlyList<Example> examples = Read(root);

        Assert.Equal(["", ""], examples.Select(example => example.Skip).OfType<ExampleSkip>().Select(skip => skip.Reason));
        Assert.Null(examples[2].Skip);
    }

    [Fact]
    public void XmlDocumentationExamples_KeepTheirColumnsAndDecodeTheirText()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "namespace A;",
                "",
                "/// <example>",
                "/// <code target=\"net11.0\">",
                "/// List&lt;int&gt; numbers = [1];",
                "///",
                "/// Console.WriteLine(numbers.Count); // Output: 1",
                "/// </code>",
                "/// <code><![CDATA[",
                "/// List<int> items = [2];",
                "/// ]]></code>",
                "/// <code skip=\"needs a server\">not C#</code>",
                "/// </example>",
                "public static class A { }"));

        IReadOnlyList<Example> examples = Read(root);

        Assert.Equal(3, examples.Count);
        Assert.Equal((5, Example.DecimalTarget), (examples[0].FirstLine, examples[0].Target));
        Assert.Equal(["    List<int> numbers = [1];", "", "    Console.WriteLine(numbers.Count); // Output: 1"], examples[0].Lines);
        Assert.Equal([new ExpectedLine("1", new Position(7, 39))], examples[0].Expected);
        Assert.Equal((10, Example.DefaultTarget), (examples[1].FirstLine, examples[1].Target));
        Assert.Equal(["    List<int> items = [2];"], examples[1].Lines);
        Assert.Equal(new ExampleSkip(new Position(12, 5), "needs a server", "'<code skip>'", "reason in the skip attribute"), examples[2].Skip);
        Assert.Equal(["not C#"], examples[2].Lines.Select(line => line.Trim()));
    }

    [Fact]
    public void XmlDocumentationExample_UnderAConditionOfTheSymbols_IsFoundOnce()
    {
        using TempDirectory root = new();
        root.Write("Directory.Build.props", "<Project><PropertyGroup><TargetFrameworks>net10.0;net11.0</TargetFrameworks></PropertyGroup></Project>");
        root.Write(
            "src/A.cs",
            Text(
                "namespace A;",
                "",
                "public static class A",
                "{",
                "#if NET11_0_OR_GREATER",
                "    /// <code target=\"net11.0\">Console.WriteLine(1m);</code>",
                "    public static void OnlyHere() { }",
                "#endif",
                "",
                "    /// <code>Console.WriteLine(1);</code>",
                "    public static void Everywhere() { }",
                "}"));

        IReadOnlyList<Example> examples = Read(root);

        Assert.Equal([6, 10], examples.Select(example => example.FirstLine));
    }

    [Fact]
    public void ExamplesOutsideReadmeDocsAndSrc_AreNotRead()
    {
        using TempDirectory root = new();
        root.Write("CHANGELOG.md", Text("```csharp", "Console.WriteLine(1);", "```"));
        root.Write("tests/A.cs", Text("/// <code>Console.WriteLine(1);</code>", "public static class A { }"));
        root.Write("docs/design/b.md", Text("```csharp", "Console.WriteLine(2);", "```"));

        IReadOnlyList<Example> examples = Read(root);

        Assert.Equal(["docs/design/b.md"], examples.Select(example => example.Path));
    }

    // ---- Comparing the output ----

    [Theory]
    [InlineData("a\nb\n", "a\nb")]
    [InlineData("a\r\nb\r\n", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("", "")]
    [InlineData("\n", "")]
    public static void Output_IsComparedExceptForLineEndingsAndTheFinalLineBreak(string printed, string expected)
    {
        Example example = ExampleOf(string.IsNullOrEmpty(expected) ? [] : expected.Split('\n'));

        Assert.Null(ExpectedOutput.Compare(example, printed));
    }

    [Theory]
    [InlineData("a \nb", "example output 'a ' differs from expected 'a'", 10)]
    [InlineData("A\nb", "example output 'A' differs from expected 'a'", 10)]
    [InlineData("a\nb\n\n", "example output continues with '' after the last expected line 'b'", 11)]
    [InlineData("a", "example output ends before the expected line 'b'", 11)]
    [InlineData("", "example output ends before the expected line 'a'", 10)]
    public static void Output_ThatDiffers_IsEng301AtTheExpectedLine(string printed, string message, int line)
    {
        Example example = ExampleOf(["a", "b"]);

        Finding? finding = ExpectedOutput.Compare(example, printed);

        Assert.Equal(Error(EngCodes.Eng301, "README.md", line, 1, message), finding);
    }

    [Fact]
    public void Output_WhenNothingIsExpected_IsReportedAtTheFirstLineOfTheCode()
    {
        Example example = new("README.md", 5, ["", "    Console.WriteLine(1);"], Example.DefaultTarget, null, []);

        Finding? finding = ExpectedOutput.Compare(example, "1\n");

        Assert.Equal(Error(EngCodes.Eng301, "README.md", 6, 5, "example prints '1' but shows no output"), finding);
    }

    // ---- The files of the build ----

    [Fact]
    public void Project_IsAConsoleApplicationOfTheTargetThatReferencesBothPackageProjects()
    {
        using TempDirectory root = new();
        Example example = new("README.md", 5, ["Console.WriteLine(1);"], Example.DecimalTarget, null, []);

        string project = ExampleProjects.ProjectText(root.Path, example);

        Assert.Contains("<TargetFramework>net11.0</TargetFramework>", project, StringComparison.Ordinal);
        Assert.Contains("<OutputType>Exe</OutputType>", project, StringComparison.Ordinal);
        Assert.Contains("<Nullable>enable</Nullable>", project, StringComparison.Ordinal);
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", project, StringComparison.Ordinal);
        Assert.Contains("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", project, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(root.Path, ExampleProjects.LibraryProject), project, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(root.Path, ExampleProjects.ProtobufProject), project, StringComparison.Ordinal);
        Assert.DoesNotContain("Great.Parts.Cel.Generator", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_ReferencesTheGeneratorAsAnAnalyzerWhenTheRepositoryHasIt()
    {
        using TempDirectory root = new();
        root.Write(ExampleProjects.GeneratorProject, ProjectFile);
        Example example = new("README.md", 5, ["Console.WriteLine(1);"], Example.DefaultTarget, null, []);

        string project = ExampleProjects.ProjectText(root.Path, example);

        Assert.Contains("OutputItemType=\"Analyzer\"", project, StringComparison.Ordinal);
        Assert.Contains("ReferenceOutputAssembly=\"false\"", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_StartsAtTheLineOfTheFileThatHoldsTheExample()
    {
        Example example = new("docs/a.md", 42, ["Console.WriteLine(1);", "Console.WriteLine(2);"], Example.DefaultTarget, null, []);

        Assert.Equal("#line 42 \"docs/a.md\"\nConsole.WriteLine(1);\nConsole.WriteLine(2);\n", ExampleProjects.ProgramText(example));
    }

    [Fact]
    public void Solution_ListsEveryProject()
    {
        string solution = ExampleProjects.SolutionText(2);

        Assert.Contains("Path=\"Example0001/Example0001.csproj\"", solution, StringComparison.Ordinal);
        Assert.Contains("Path=\"Example0002/Example0002.csproj\"", solution, StringComparison.Ordinal);
        Assert.Equal("Example0003", ExampleProjects.Name(2));
    }

    // ---- Reading the output of the build ----

    [Fact]
    public void BuildOutput_ErrorLinesAreReadWithTheirPlaceAndProject_AndNothingElse()
    {
        string output = string.Join(
            '\n',
            "README.md(42,17): error CS0103: The name 'env2' does not exist in the current context [/tmp/x/Example0001/Example0001.csproj]",
            "README.md(42,17): error CS0103: The name 'env2' does not exist in the current context [/tmp/x/Example0001/Example0001.csproj]",
            "/tmp/x/Example0002/Program.cs(3,1,3,9): error CS1002: ; expected [/tmp/x/Example0002/Example0002.csproj]",
            "CSC : error CS5001: Program does not contain a static 'Main' method suitable for an entry point [/tmp/x/Example0003/Example0003.csproj]",
            "README.md(5,1): warning CS0219: The variable 'x' is assigned but its value is never used [/tmp/x/Example0004/Example0004.csproj]",
            "/tmp/x/src/Great.Parts.Cel/A.cs(1,1): error CS0103: The name 'a' does not exist in the current context [/tmp/x/src/Great.Parts.Cel/Great.Parts.Cel.csproj::TargetFramework=net10.0]",
            "Build FAILED.");

        IReadOnlyList<BuildError> errors = ExampleBuild.ErrorsOf(output);

        Assert.Equal(
            [
                new BuildError("README.md", new Position(42, 17), "CS0103", "The name 'env2' does not exist in the current context", "/tmp/x/Example0001/Example0001.csproj"),
                new BuildError("/tmp/x/Example0002/Program.cs", new Position(3, 1), "CS1002", "; expected", "/tmp/x/Example0002/Example0002.csproj"),
                new BuildError(null, null, "CS5001", "Program does not contain a static 'Main' method suitable for an entry point", "/tmp/x/Example0003/Example0003.csproj"),
                new BuildError("/tmp/x/src/Great.Parts.Cel/A.cs", new Position(1, 1), "CS0103", "The name 'a' does not exist in the current context", "/tmp/x/src/Great.Parts.Cel/Great.Parts.Cel.csproj"),
            ],
            errors);
        Assert.Equal(["Example0001", "Example0002", "Example0003", "Great.Parts.Cel"], errors.Select(error => ExampleBuild.ProjectName(error) ?? string.Empty));
    }

    // ---- Helpers ----

    private static Finding Error(string code, string path, int line, int column, string message) =>
        FindingAssert.Error(code, path, line, column, message);

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    // The line and the column, both from 1, of the first occurrence of the text in the lines.
    private static (int Line, int Column) Locate(string[] lines, string text)
    {
        int index = Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
        return (index + 1, lines[index].IndexOf(text, StringComparison.Ordinal) + 1);
    }

    private static Example ExampleOf(string[] expected) =>
        new(
            "README.md",
            5,
            ["Console.WriteLine(1);"],
            Example.DefaultTarget,
            null,
            [.. expected.Select((text, index) => new ExpectedLine(text, new Position(10 + index, 1)))]);

    private static IReadOnlyList<Example> Read(TempDirectory root) =>
        ExampleSources.Of(new CheckContext(root.Path, new Dictionary<string, string>()));

    // Two small libraries in the places of the package projects: the examples are built against them.
    private static void WriteLibraries(TempDirectory root)
    {
        root.Write(ExampleProjects.LibraryProject, ProjectFile);
        root.Write("src/Great.Parts.Cel/Calculator.cs", Text(CalculatorSource));
        root.Write(ExampleProjects.ProtobufProject, ProjectFile);
        root.Write("src/Great.Parts.Cel.Protobuf/Marker.cs", Text("namespace Great.Parts.Cel.Protobuf;", "", "public static class Marker", "{", "}"));
    }

    private static Result Check(TempDirectory root, ICheckCommand? command = null)
    {
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new();
        int exitCode = Program.Run(["examples", "--root", root.Path], [command ?? new ExamplesCommand()], root.Path, output, error);
        return new Result(exitCode, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString());
    }

    // The exit code is 1 when there is a finding (all the findings of these tests are errors), and the lines are exactly the expected ones. A line of the compiler has its own code
    // and does not match the pattern of the codes ENG, so the lines are compared as text.
    private static void AssertFindings(Result result, params Finding[] expected)
    {
        Assert.Equal(expected.Length == 0 ? 0 : 1, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(expected.Select(finding => finding.ToString()), result.Lines);
    }

    private sealed record Result(int ExitCode, string[] Lines, string Error);
}

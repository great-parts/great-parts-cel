using System;
using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

public sealed class ProgramTests
{
    private const string Newline = "\n";

    [Fact]
    public void NoFindings_ExitCodeIsZero()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "first", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(1, command.Runs);
    }

    [Fact]
    public void WarningOnly_ExitCodeIsZeroAndTheLineIsWritten()
    {
        using TempDirectory root = new();
        StubCommand command = new("first", new Finding("a.cs", 1, 1, FindingSeverity.Warning, "ENG102", "w"));

        Result result = Run(root.Path, [command], "first", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a.cs(1,1): warning ENG102: w" + Newline, result.Output);
    }

    [Fact]
    public void ErrorFinding_ExitCodeIsOne()
    {
        using TempDirectory root = new();
        StubCommand command = new(
            "first",
            new Finding("a.cs", 1, 1, FindingSeverity.Warning, "ENG102", "w"),
            new Finding("b.cs", 2, 3, FindingSeverity.Error, "ENG101", "e"));

        Result result = Run(root.Path, [command], "first", "--root", root.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("a.cs(1,1): warning ENG102: w" + Newline + "b.cs(2,3): error ENG101: e" + Newline, result.Output);
    }

    [Fact]
    public void FindingsOfOneCommand_AreSortedByPathOrdinalLineAndColumn()
    {
        using TempDirectory root = new();
        StubCommand command = new(
            "first",
            Error("b.cs", 1, 1),
            Error("a.cs", 10, 1),
            Error("a.cs", 2, 9),
            Error("a.cs", 2, 3),
            Error("B.cs", 5, 5),
            Error("a/b.cs", 1, 1));

        Result result = Run(root.Path, [command], "first", "--root", root.Path);

        // Ordinal order: an upper case letter before a lower case one, "." before "/".
        Assert.Equal(
            [
                "B.cs(5,5): error ENG101: m",
                "a.cs(2,3): error ENG101: m",
                "a.cs(2,9): error ENG101: m",
                "a.cs(10,1): error ENG101: m",
                "a/b.cs(1,1): error ENG101: m",
                "b.cs(1,1): error ENG101: m",
            ],
            Lines(result.Output));
    }

    [Fact]
    public void Commands_RunInTheGivenOrderAndEachSortsItsOwnFindings()
    {
        using TempDirectory root = new();
        StubCommand first = new("first", Error("z.cs", 1, 1), Error("y.cs", 1, 1));
        StubCommand second = new("second", Error("b.cs", 1, 1));

        Result result = Run(root.Path, [first, second], "second", "first", "--root", root.Path);

        Assert.Equal(
            ["b.cs(1,1): error ENG101: m", "y.cs(1,1): error ENG101: m", "z.cs(1,1): error ENG101: m"],
            Lines(result.Output));
    }

    [Fact]
    public void EveryOutputLine_MatchesTheFindingPattern()
    {
        using TempDirectory root = new();
        StubCommand command = new(
            "first",
            Error("a.cs", 1, 1),
            Finding.ForCommit("0123abc", FindingSeverity.Error, "ENG701", "m"),
            Finding.ForTag("v1.0.0", FindingSeverity.Warning, "ENG200", "m"));

        Result result = Run(root.Path, [command], "first", "--root", root.Path);

        Assert.Equal(3, Lines(result.Output).Length);
        foreach (string line in Lines(result.Output))
        {
            Assert.Matches(Finding.Pattern, line);
        }
    }

    [Fact]
    public void UnknownCommand_ExitCodeIsTwoAndNothingRuns()
    {
        using TempDirectory root = new();
        StubCommand command = new("first", Error("a.cs", 1, 1));

        Result result = Run(root.Path, [command], "first", "nothing", "--root", root.Path);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal("eng/checks: unknown command nothing; known commands: first" + Newline, result.Error);
        Assert.Equal(0, command.Runs);
    }

    [Fact]
    public void NoCommand_ExitCodeIsTwo()
    {
        using TempDirectory root = new();

        Result result = Run(root.Path, [new StubCommand("first")], "--root", root.Path);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("eng/checks: no command given; known commands: first" + Newline, result.Error);
    }

    [Fact]
    public void UnknownOption_ExitCodeIsTwo()
    {
        using TempDirectory root = new();

        Result result = Run(root.Path, [new StubCommand("first")], "first", "--bogus", "--root", root.Path);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("eng/checks: unknown option --bogus" + Newline, result.Error);
    }

    [Fact]
    public void RootOptionWithoutValue_ExitCodeIsTwo()
    {
        using TempDirectory root = new();

        Result result = Run(root.Path, [new StubCommand("first")], "first", "--root");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("eng/checks: the option --root needs a directory" + Newline, result.Error);
    }

    [Fact]
    public void RootThatDoesNotExist_ExitCodeIsTwoAndNothingRuns()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");
        string missing = Path.Combine(root.Path, "missing");

        Result result = Run(root.Path, [command], "first", "--root", missing);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal($"eng/checks: the directory given by --root does not exist: {missing}" + Newline, result.Error);
        Assert.Equal(0, command.Runs);
    }

    [Fact]
    public void RootOption_IsTheRootOfTheContext()
    {
        using TempDirectory working = new();
        using TempDirectory other = new();
        StubCommand command = new("first");

        Result result = Run(working.Path, [command], "first", "--root", other.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(other.Path, command.LastRoot);
    }

    [Fact]
    public void WithoutRootOption_TheRootIsTheNearestParentWithTheSolutionFile()
    {
        using TempDirectory root = new();
        root.Write("Great.Parts.Cel.slnx", "<Solution />");
        root.Write("eng/checks/readme.txt", "x");
        StubCommand command = new("first");

        Result result = Run(Path.Combine(root.Path, "eng", "checks"), [command], "first");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(root.Path, command.LastRoot);
    }

    [Fact]
    public void WithoutRootOptionAndSolutionFile_ExitCodeIsTwo()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "first");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Great.Parts.Cel.slnx is not in", result.Error, StringComparison.Ordinal);
        Assert.Equal(0, command.Runs);
    }

    [Fact]
    public void ScansAreComputedOnceInARun()
    {
        using TempDirectory root = new();
        int computed = 0;
        ScanCommand first = new("first", () => ++computed);
        ScanCommand second = new("second", () => ++computed);

        Result result = Run(root.Path, [first, second], "first", "second", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, computed);
        Assert.Equal([1, 1], [first.Seen, second.Seen]);
    }

    [Fact]
    public void NoBuildOption_IsAFlagWithAnEmptyValueInTheOptionsOfTheContext()
    {
        using TempDirectory root = new();
        StubCommand before = new("first");
        StubCommand after = new("second");

        Result result = Run(root.Path, [before, after], "--no-build", "first", "second", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal([string.Empty, string.Empty], [before.LastOptions["--no-build"], after.LastOptions["--no-build"]]);
        Assert.Equal(root.Path, before.LastOptions["--root"]);
    }

    [Fact]
    public void NoBuildOption_AfterACommandAndAfterTheArgumentOfACommand_IsAFlagToo()
    {
        using TempDirectory root = new();
        StubCommand plain = new("plain");
        ArgumentCommand withArgument = new("argued");

        Result result = Run(root.Path, [plain, withArgument], "plain", "--no-build", "argued", "main", "--no-build", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.True(plain.LastOptions.ContainsKey("--no-build"));
        Assert.True(withArgument.LastOptions.ContainsKey("--no-build"));
        Assert.Equal("main", withArgument.LastArgument);
    }

    [Fact]
    public void WithoutNoBuildOption_TheContextHasNoSuchOption()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "first", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.False(command.LastOptions.ContainsKey("--no-build"));
    }

    [Fact]
    public void NoBuildOptionDoesNotTakeTheNextTokenAsItsValue()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "--no-build", "--root", root.Path, "first");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, command.Runs);
        Assert.Equal(root.Path, command.LastRoot);
    }

    [Fact]
    public void ReportAndWhereOptions_TakeTheNextTokenAsTheirValueAndReportsAreFullPaths()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "--jit", "jit.xml", "first", "--where", "linux-x64, net11.0", "--aot", "../aot.xml", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(Path.Combine(root.Path, "jit.xml"), command.LastOptions["--jit"]);
        Assert.Equal(Path.GetFullPath("../aot.xml", root.Path), command.LastOptions["--aot"]);
        Assert.Equal("linux-x64, net11.0", command.LastOptions["--where"]);
    }

    [Fact]
    public void WhereOption_IsTakenAsItIsAndNotParsed()
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "first", "--where", "--jit", "--root", root.Path);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("--jit", command.LastOptions["--where"]);
        Assert.False(command.LastOptions.ContainsKey("--jit"));
    }

    [Theory]
    [InlineData("--jit", "the option --jit needs a report")]
    [InlineData("--aot", "the option --aot needs a report")]
    [InlineData("--where", "the option --where needs a text")]
    public static void ReportAndWhereOptionsWithoutValue_ExitCodeIsTwoAndNothingRuns(string option, string message)
    {
        using TempDirectory root = new();
        StubCommand command = new("first");

        Result result = Run(root.Path, [command], "first", "--root", root.Path, option);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("eng/checks: " + message + Newline, result.Error);
        Assert.Equal(0, command.Runs);
    }

    private static Finding Error(string path, int line, int column) => new(path, line, column, FindingSeverity.Error, "ENG101", "m");

    private static string[] Lines(string output) => output.Split(Newline, StringSplitOptions.RemoveEmptyEntries);

    private static Result Run(string workingDirectory, IReadOnlyList<ICheckCommand> commands, params string[] args)
    {
        using StringWriter output = new() { NewLine = Newline };
        using StringWriter error = new() { NewLine = Newline };
        int exitCode = Program.Run(args, commands, workingDirectory, output, error);
        return new Result(exitCode, output.ToString(), error.ToString());
    }

    private sealed record Result(int ExitCode, string Output, string Error);

    private sealed class StubCommand(string name, params Finding[] findings) : ICheckCommand
    {
        public string Name { get; } = name;

        public int Runs { get; private set; }

        public string? LastRoot { get; private set; }

        public IReadOnlyDictionary<string, string> LastOptions { get; private set; } = new Dictionary<string, string>();

        public IEnumerable<Finding> Run(CheckContext context)
        {
            Runs++;
            LastRoot = context.Root;
            LastOptions = context.Options;
            return findings;
        }
    }

    private sealed class ArgumentCommand(string name) : ICheckCommand
    {
        public string Name { get; } = name;

        public string? ArgumentName => "<range>";

        public string? LastArgument { get; private set; }

        public IReadOnlyDictionary<string, string> LastOptions { get; private set; } = new Dictionary<string, string>();

        public IEnumerable<Finding> Run(CheckContext context)
        {
            LastArgument = context.Arguments[Name];
            LastOptions = context.Options;
            return [];
        }
    }

    private sealed class ScanCommand(string name, Func<int> compute) : ICheckCommand
    {
        public string Name { get; } = name;

        public int Seen { get; private set; }

        public IEnumerable<Finding> Run(CheckContext context)
        {
            Seen = context.Scan("shared", compute);
            return [];
        }
    }
}

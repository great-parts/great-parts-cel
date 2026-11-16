using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using TestRoot = Great.Parts.Cel.Eng.Tests.Support.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks quick-start` (docs/design/eng-release.md, "eng/checks quick-start"). The command runs through Program on a temporary
// repository with a README and a directory of packages, with a replaced container and a replaced clock: no test needs Docker or waits.
// The container answers the commands by what the test gives it and moves the clock by the time the test gives them.
public sealed class QuickStartTests
{
    private const string Image = "mcr.microsoft.com/dotnet/sdk:10.0.100";
    private const string ReadmePath = "README.md";
    private const string AddPackage = "dotnet add package Great.Parts.Cel";
    private const string RunExample = "dotnet run";
    private const string StablePackage = "Great.Parts.Cel.1.0.0.nupkg";

    // The lines of the README of a quick start; the begin marker is the line 3, the commands the lines 5, 6, 7 and 13, the expected output the line 10.
    private static readonly string[] Readme =
    [
        "# Title",
        "",
        "<!-- quick-start:begin -->",
        "```shell",
        "dotnet new console -o QuickStart",
        "cd QuickStart",
        AddPackage,
        "```",
        "```csharp",
        "Console.WriteLine(1 + 2); // Output: 3",
        "```",
        "```shell",
        RunExample,
        "```",
        "<!-- quick-start:end -->",
    ];

    private static readonly int OutputColumn = Readme[9].IndexOf("//", StringComparison.Ordinal) + 1;

    // ---- The cases of the check ----

    [Fact]
    public void StepsThatPassInTime_NoFindingsAndTheStepsRunInTheOrderOfTheReadme()
    {
        Outcome outcome = Check(Readme, container => container.Results[RunExample] = new QuickStartStepResult(0, "Welcome\n3\n", string.Empty));

        AssertFindings(outcome);
        Assert.Equal(
            [
                "run: dotnet new console -o QuickStart",
                "run: cd QuickStart",
                "run: " + AddPackage,
                "write Program.cs: Console.WriteLine(1 + 2); // Output: 3\n",
                "run: " + RunExample,
            ],
            outcome.Container.Events);
        (string image, string packages) = Assert.Single(outcome.Started);
        Assert.Equal(Image, image);
        Assert.Equal(Path.Combine(outcome.Root, "packages"), packages);
        Assert.True(outcome.Disposed);
    }

    [Fact]
    public void Eng304_ACommandThatExitsWith1_IsReportedAtItsLineAndTheLaterStepsDoNotRun()
    {
        Outcome outcome = Check(Readme, container => container.Results[AddPackage] = new QuickStartStepResult(1, "restoring\n", "\nerror: Unable to find package 'Great.Parts.Cel'\nmore\n"));

        AssertFindings(outcome, Error(EngCodes.Eng304, 7, 1, "step 'dotnet add package Great.Parts.Cel' exited with code 1: error: Unable to find package 'Great.Parts.Cel'"));
        Assert.Equal(["run: dotnet new console -o QuickStart", "run: cd QuickStart", "run: " + AddPackage], outcome.Container.Events);
        Assert.True(outcome.Disposed);
    }

    // The reason is the first line with "error" in the error output and then in the standard output; without it the last line of the error
    // output, or of the standard output when the error output is empty.
    [Theory]
    [InlineData("", "Determining projects to restore...\nerror NU1101: Unable to find package\nRestore failed\n", "error NU1101: Unable to find package")]
    [InlineData("warning: slow\nERROR: late\nlast\n", "error in the standard output\n", "ERROR: late")]
    [InlineData("first\nlast\n", "out line\n", "last")]
    [InlineData("", "out line\nlast out\n", "last out")]
    public static void Eng304_TheReasonOfAFailedCommand_IsOneLineOfItsOutput(string error, string output, string reason)
    {
        Outcome outcome = Check(Readme, container => container.Results[AddPackage] = new QuickStartStepResult(1, output, error));

        AssertFindings(outcome, Error(EngCodes.Eng304, 7, 1, $"step '{AddPackage}' exited with code 1: {reason}"));
    }

    [Fact]
    public void Eng304_ACommandThatExitsWithoutAMessage_NamesThatInTheFinding()
    {
        Outcome outcome = Check(Readme, container => container.Results["cd QuickStart"] = new QuickStartStepResult(2, string.Empty, string.Empty));

        AssertFindings(outcome, Error(EngCodes.Eng304, 6, 1, "step 'cd QuickStart' exited with code 2: no message on standard error"));
    }

    [Fact]
    public void Eng304_TheEndOfTheOutputThatIsNotTheExpectedOutput_IsReportedAtTheFirstExpectedLine()
    {
        Outcome outcome = Check(Readme, container => container.Results[RunExample] = new QuickStartStepResult(0, "Welcome\n4\n", string.Empty));

        AssertFindings(
            outcome,
            Error(EngCodes.Eng304, 10, OutputColumn, "the end of the output of step 'dotnet run' is not the expected output: example output '4' differs from expected '3'"));
    }

    [Fact]
    public void Eng304_AnOutputThatEndsBeforeTheExpectedLines_IsReportedAtTheFirstExpectedLine()
    {
        Outcome outcome = Check(Readme, container => container.Results[RunExample] = new QuickStartStepResult(0, string.Empty, string.Empty));

        AssertFindings(
            outcome,
            Error(EngCodes.Eng304, 10, OutputColumn, "the end of the output of step 'dotnet run' is not the expected output: example output ends before the expected line '3'"));
    }

    [Fact]
    public void Eng304_ASessionThatIsStopped_IsReportedAtTheCommandThatDidNotEnd()
    {
        Outcome outcome = Check(Readme, container => container.Results[RunExample] = new QuickStartStepResult(null, string.Empty, string.Empty));

        AssertFindings(outcome, Error(EngCodes.Eng304, 13, 1, "step 'dotnet run' did not end: the session was stopped at 10 minutes"));
    }

    [Fact]
    public void Eng305_TheTimeOf301Seconds_IsReportedAtTheBeginMarker()
    {
        Outcome outcome = Check(Readme, TakeSeconds(301));

        AssertFindings(outcome, Error(EngCodes.Eng305, 3, 1, "the quick start took 301 s, the limit is 300 s"));
    }

    [Fact]
    public void TheTimeOf300Seconds_IsNotAFinding()
    {
        Outcome outcome = Check(Readme, TakeSeconds(300));

        AssertFindings(outcome);
    }

    [Fact]
    public void Eng304_AFailedStep_IsNotJudgedByTime()
    {
        Outcome outcome = Check(
            Readme,
            container =>
            {
                TakeSeconds(500)(container);
                container.Results[AddPackage] = new QuickStartStepResult(1, string.Empty, "error: failed");
            });

        AssertFindings(outcome, Error(EngCodes.Eng304, 7, 1, "step 'dotnet add package Great.Parts.Cel' exited with code 1: error: failed"));
    }

    [Fact]
    public void TheBuildMessagesBeforeTheExpectedLines_AreNotCompared()
    {
        Outcome outcome = Check(
            Readme,
            container => container.Results[RunExample] = new QuickStartStepResult(0, "Restore complete\r\nBuild succeeded: 0 warnings\r\n3\r\n", string.Empty));

        AssertFindings(outcome);
    }

    [Fact]
    public void EachStep_GetsWhatIsLeftOfTheSessionLimit()
    {
        Outcome outcome = Check(Readme, container => container.Times["cd QuickStart"] = TimeSpan.FromSeconds(100));

        Assert.Equal(
            [TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(500), TimeSpan.FromSeconds(500), TimeSpan.FromSeconds(500)],
            outcome.Container.Limits);
    }

    // ---- Input that cannot be read: exit code 2 ----

    public static TheoryData<string, string> UnreadableReadmes => new()
    {
        { Text("# Title", "", "No block."), "README.md has no quick-start block" },
        { Text([.. Readme[..14]]), "it is one begin marker followed by one end marker" },
        { Text([.. Readme, .. Readme]), "it is one begin marker followed by one end marker" },
        { Text([Readme[14], .. Readme[..14]]), "it is one begin marker followed by one end marker" },
        { Text([.. Readme[..11], .. Readme[8..11], .. Readme[11..]]), "it needs exactly one block 'csharp' with no word after it" },
        { Text([.. Readme[..8], "```csharp net11.0", .. Readme[9..]]), "it needs exactly one block 'csharp' with no word after it" },
        { Text([.. Readme[..8], .. Readme[11..]]), "it needs exactly one block 'csharp' with no word after it" },
        { Text(Readme[2], "```csharp", "Console.WriteLine(1);", "```", Readme[14]), "it has no command in a block 'shell'" },
        { Text([.. Readme[..9], "Console.WriteLine(1 + 2);", .. Readme[10..]]), "the block 'csharp' shows no expected output" },
    };

    [Theory]
    [MemberData(nameof(UnreadableReadmes))]
    public static void ABlockThatCannotBeRead_IsExitCode2AndNoContainerStarts(string readme, string message)
    {
        ArgumentNullException.ThrowIfNull(readme);
        Outcome outcome = Check(Lines(readme));

        Assert.Equal(2, outcome.ExitCode);
        Assert.Empty(outcome.Lines);
        Assert.Contains(message, outcome.Error, StringComparison.Ordinal);
        Assert.StartsWith("eng/checks: ", outcome.Error, StringComparison.Ordinal);
        Assert.Empty(outcome.Started);
    }

    [Theory]
    [InlineData("Great.Parts.Cel.1.0.0-preview.1.nupkg")]
    [InlineData("Great.Parts.Cel.Protobuf.1.0.0.nupkg")]
    [InlineData("Great.Parts.Cel.1.0.0.snupkg")]
    public static void ADirectoryWithoutAStablePackage_IsExitCode2AndNoContainerStarts(string file)
    {
        Outcome outcome = Check(Readme, packageFiles: [file]);

        Assert.Equal(2, outcome.ExitCode);
        Assert.Contains("has no stable package Great.Parts.Cel", outcome.Error, StringComparison.Ordinal);
        Assert.Empty(outcome.Started);
    }

    [Fact]
    public void AMissingDirectoryOfPackages_IsExitCode2()
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text(Readme));

        Outcome outcome = Execute(root, "--packages", "nowhere", "--image", Image);

        Assert.Equal(2, outcome.ExitCode);
        Assert.Contains("has no stable package Great.Parts.Cel", outcome.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingReadme_IsExitCode2()
    {
        using TempDirectory root = new();

        Outcome outcome = Execute(root, "--packages", "packages", "--image", Image);

        Assert.Equal(2, outcome.ExitCode);
        Assert.Contains("README.md does not exist", outcome.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--packages", "packages")]
    [InlineData("--image", Image)]
    public static void ACommandWithoutOneOfItsOptions_IsExitCode2(string option, string value)
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text(Readme));

        Outcome outcome = Execute(root, option, value);

        Assert.Equal(2, outcome.ExitCode);
        Assert.Equal("eng/checks: the command quick-start needs --packages and --image" + Environment.NewLine, outcome.Error);
    }

    // ---- The README and the registry ----

    [Fact]
    public void TheReadmeOfTheRepository_HasABlockOfTheFormTheCommandRuns()
    {
        QuickStartBlock block = QuickStartBlock.Read(ReadmePath, File.ReadAllText(Path.Combine(TestRoot.Find(), ReadmePath)));

        Assert.Equal("dotnet new console -o QuickStart", block.Steps[0].Text);
        Assert.Contains(block.Steps, step => step.Text == AddPackage && !step.IsProgram);
        Assert.Single(block.Steps, step => step.IsProgram);
        Assert.Equal(RunExample, block.Steps[^1].Text);
        Assert.False(block.Steps[^1].IsProgram);
        Assert.NotEmpty(block.Expected);
    }

    // The image of the check is the tag of the stage sdk-min of the container file, in the same words in both workflows.
    [Fact]
    public void TheImageOfTheQuickStart_IsTheSameInBothWorkflowsAndInTheStageSdkMin()
    {
        string root = TestRoot.Find();
        string ci = Match(Path.Combine(root, ".github", "workflows", "ci.yml"), "QUICK_START_IMAGE: (\\S+)");
        string release = Match(Path.Combine(root, ".github", "workflows", "release.yml"), "QUICK_START_IMAGE: (\\S+)");
        string stage = Match(Path.Combine(root, "eng", "docker", "Dockerfile"), "FROM (\\S+) AS sdk-min");

        Assert.StartsWith("mcr.microsoft.com/dotnet/sdk:", ci, StringComparison.Ordinal);
        Assert.Equal(ci, release);
        Assert.Equal(ci, stage);
    }

    [Fact]
    public void TheCommand_IsInTheRegistryOfTheApplication()
    {
        Assert.Contains(CommandRegistry.All, command => command is QuickStartCommand && command.Name == "quick-start");
    }

    // ---- Helpers ----

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    private static string Match(string file, string pattern) => Regex.Match(File.ReadAllText(file), pattern, RegexOptions.CultureInvariant).Groups[1].Value;

    private static string[] Lines(string text) => text.Split('\n');

    private static Finding Error(string code, int line, int column, string message) => FindingAssert.Error(code, ReadmePath, line, column, message);

    // The first step takes the time; the others none.
    private static Action<FakeContainer> TakeSeconds(int seconds) => container => container.Times["dotnet new console -o QuickStart"] = TimeSpan.FromSeconds(seconds);

    private static Outcome Check(string[] readme, Action<FakeContainer>? arrange = null, string[]? packageFiles = null)
    {
        using TempDirectory root = new();
        root.Write(ReadmePath, Text(readme));
        foreach (string file in packageFiles ?? [StablePackage])
        {
            root.Write("packages/" + file, "package");
        }

        return Execute(root, arrange, "--packages", "packages", "--image", Image);
    }

    private static Outcome Execute(TempDirectory root, params string[] options) => Execute(root, null, options);

    private static Outcome Execute(TempDirectory root, Action<FakeContainer>? arrange, params string[] options)
    {
        FakeClock clock = new();
        using FakeContainer container = new(clock);

        // The example prints what the README expects, unless the test says otherwise.
        container.Results[RunExample] = new QuickStartStepResult(0, "3\n", string.Empty);
        arrange?.Invoke(container);
        List<(string Image, string Packages)> started = [];
        QuickStartCommand command = new(
            (image, packages) =>
            {
                started.Add((image, packages));
                return container;
            },
            clock);
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new();
        int exitCode = Program.Run(["quick-start", "--root", root.Path, .. options], [command], root.Path, output, error);
        return new Outcome(exitCode, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString(), container, container.Disposed, started, root.Path);
    }

    // The exit code is 1 when there is a finding (all the findings of these tests are errors), and the lines are exactly the expected ones.
    private static void AssertFindings(Outcome outcome, params Finding[] expected)
    {
        Assert.Equal(expected.Length == 0 ? 0 : 1, outcome.ExitCode);
        Assert.Equal(string.Empty, outcome.Error);
        Assert.Equal(expected.Select(finding => finding.ToString()), outcome.Lines);
    }

    private sealed record Outcome(int ExitCode, string[] Lines, string Error, FakeContainer Container, bool Disposed, List<(string Image, string Packages)> Started, string Root);

    private sealed class FakeClock : IQuickStartClock
    {
        public TimeSpan Now { get; set; }
    }

    // A container that answers a command with the result of the test, or with success, and spends the time of the test on it.
    private sealed class FakeContainer(FakeClock clock) : IQuickStartContainer
    {
        internal List<string> Events { get; } = [];

        internal List<TimeSpan> Limits { get; } = [];

        internal Dictionary<string, QuickStartStepResult> Results { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, TimeSpan> Times { get; } = new(StringComparer.Ordinal);

        internal bool Disposed { get; private set; }

        public QuickStartStepResult Run(string command, TimeSpan limit) => Step("run: " + command, command, limit);

        public QuickStartStepResult Write(string fileName, string text, TimeSpan limit) => Step($"write {fileName}: {text}", fileName, limit);

        public void Dispose() => Disposed = true;

        private QuickStartStepResult Step(string description, string key, TimeSpan limit)
        {
            Events.Add(description);
            Limits.Add(limit);
            clock.Now += Times.GetValueOrDefault(key);
            return Results.GetValueOrDefault(key, new QuickStartStepResult(0, string.Empty, string.Empty));
        }
    }
}

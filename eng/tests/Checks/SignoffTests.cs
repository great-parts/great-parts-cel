using System;
using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using GitProcess = Great.Parts.Cel.Eng.Checks.Git;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks signoff` (docs/design/eng-checks-artifacts.md, "signoff"). Every test makes a git repository in a temporary
// directory with `git init` and commits with and without the trailer. The commits are empty: only their messages are read.
public sealed class SignoffTests
{
    private const string Newline = "\n";
    private const string Signed = "Signed-off-by: Test <test@example.com>";
    private const string BotAuthor = "dependabot[bot] <49699333+dependabot[bot]@users.noreply.github.com>";

    [Fact]
    public void CommitWithoutTrailer_IsEng701()
    {
        using TempDirectory root = new();
        Init(root);
        string unsigned = Commit(root, "feat: add strings extension v4");

        List<Finding> findings = Check(root, "main");

        FindingAssert.Exactly(findings, Error(unsigned, "feat: add strings extension v4"));
    }

    [Fact]
    public void CommitWithoutTrailer_IsReportedAtTheCommitWithTheEightCharactersOfItsHash()
    {
        using TempDirectory root = new();
        Init(root);
        string unsigned = Commit(root, "fix: handle an empty list");

        Result result = Run(root, "main");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(
            $"commit/{unsigned[..8]}(1,1): error ENG701: commit {unsigned[..8]} 'fix: handle an empty list' has no Signed-off-by trailer" + Newline,
            result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void CommitWithTrailer_HasNoFindingsAndExitCodeIsZero()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "feat: add a function\n\n" + Signed);

        Result result = Run(root, "main");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void MergeWithoutTrailer_IsEng701ForTheMergeOnly()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "feat: first\n\n" + Signed);
        RunGit(root, "switch", "-q", "-c", "side");
        Commit(root, "feat: second\n\n" + Signed);
        RunGit(root, "switch", "-q", "main");
        Commit(root, "feat: third\n\n" + Signed);
        RunGit(root, "merge", "-q", "--no-ff", "-m", "Merge branch 'side'", "side");
        string merge = Rev(root, "HEAD");
        Assert.Equal(2, Rev(root, "HEAD^@").Split(' ').Length);

        List<Finding> findings = Check(root, "main");

        FindingAssert.Exactly(findings, Error(merge, "Merge branch 'side'"));
    }

    [Fact]
    public void MergeWithTrailer_HasNoFindings()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "feat: first\n\n" + Signed);
        RunGit(root, "switch", "-q", "-c", "side");
        Commit(root, "feat: second\n\n" + Signed);
        RunGit(root, "switch", "-q", "main");
        Commit(root, "feat: third\n\n" + Signed);
        RunGit(root, "merge", "-q", "--no-ff", "-m", "Merge branch 'side'\n\n" + Signed, "side");

        FindingAssert.None(Check(root, "main"));
    }

    [Fact]
    public void CommitOfABotWithoutTrailer_IsEng701()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "feat: first\n\n" + Signed);
        string bot = Commit(root, "chore(deps): bump Example.Package from 1.0.0 to 1.0.1", BotAuthor);

        List<Finding> findings = Check(root, "main");

        FindingAssert.Exactly(findings, Error(bot, "chore(deps): bump Example.Package from 1.0.0 to 1.0.1"));
    }

    [Fact]
    public void EveryCommitWithoutTrailerOfTheRange_IsAFindingAndSignedOnesAreNot()
    {
        using TempDirectory root = new();
        Init(root);
        string first = Commit(root, "one");
        Commit(root, "two\n\n" + Signed);
        string third = Commit(root, "three");

        List<Finding> findings = Check(root, "main");

        FindingAssert.Exactly(findings, Error(first, "one"), Error(third, "three"));
    }

    [Fact]
    public void OnlyTheCommitsOfTheRange_AreChecked()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "old unsigned");
        string before = Rev(root, "HEAD");
        string inRange = Commit(root, "new unsigned");
        string after = Rev(root, "HEAD");

        List<Finding> findings = Check(root, before + ".." + after);

        FindingAssert.Exactly(findings, Error(inRange, "new unsigned"));
    }

    [Fact]
    public void EmptyRange_HasNoFindings()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "unsigned");
        string head = Rev(root, "HEAD");

        Result result = Run(root, head + ".." + head);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
    }

    [Theory]
    [InlineData("subject\n\nbody\n\nsigned-off-by: Test <test@example.com>")]
    [InlineData("subject\n\nSigned-off-by: A <a@example.com>\nSigned-off-by: B <b@example.com>")]
    [InlineData("subject\n\nbody\n---\nmore text\n\nSigned-off-by: Test <test@example.com>")]
    [InlineData("subject\n\nCo-authored-by: C <c@example.com>\nSigned-off-by: Test <test@example.com>")]
    public static void TrailerInTheLastParagraph_CountsWhateverTheCaseOrTheNeighbours(string message)
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, message);

        FindingAssert.None(Check(root, "main"));
    }

    [Theory]
    [InlineData("subject\n\nSigned-off-by: Test <test@example.com>\n\nbody after the trailer")]
    [InlineData("subject\n\nCo-authored-by: C <c@example.com>")]
    [InlineData("Signed-off-by is mentioned in the subject")]
    [InlineData("subject\n\nbody that says Signed-off-by: someone but is not a trailer block\nand goes on")]
    public static void TrailerOutsideTheLastParagraphOrOnlyMentioned_IsEng701(string message)
    {
        using TempDirectory root = new();
        Init(root);
        ArgumentNullException.ThrowIfNull(message);
        string commit = Commit(root, message);

        List<Finding> findings = Check(root, "main");

        FindingAssert.Exactly(findings, Error(commit, message.Split('\n')[0]));
    }

    [Fact]
    public void SubjectOfTheMessage_IsTheFirstLineOnly()
    {
        using TempDirectory root = new();
        Init(root);
        string commit = Commit(root, "feat: a subject\n\nA long body\nthat goes on");

        FindingAssert.Exactly(Check(root, "main"), Error(commit, "feat: a subject"));
    }

    [Fact]
    public void RangeThatGitDoesNotKnow_ExitCodeIsTwoAndNothingIsWrittenToOutput()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "unsigned");

        Result result = Run(root, "no-such-branch..main");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.StartsWith("eng/checks: the range 'no-such-branch..main' cannot be listed by git rev-list: ", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RangeThatStartsWithADash_IsARevisionNotAnOptionOfGit()
    {
        using TempDirectory root = new();
        Init(root);
        Commit(root, "unsigned");

        Result result = Run(root, "-1");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.StartsWith("eng/checks: the range '-1' cannot be listed by git rev-list: ", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectoryThatIsNotARepository_ExitCodeIsTwo()
    {
        using TempDirectory root = new();

        Result result = Run(root, "main");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("cannot be listed by git rev-list", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandWithoutRange_ExitCodeIsTwo()
    {
        using TempDirectory root = new();
        Init(root);

        Result beforeAnOption = RunArguments(root, "signoff", "--root", root.Path);
        Result atTheEnd = RunArguments(root, "--root", root.Path, "signoff");

        Assert.Equal(2, beforeAnOption.ExitCode);
        Assert.Equal("eng/checks: the command signoff needs <range>" + Newline, beforeAnOption.Error);
        Assert.Equal(2, atTheEnd.ExitCode);
        Assert.Equal("eng/checks: the command signoff needs <range>" + Newline, atTheEnd.Error);
    }

    [Fact]
    public void TheRangeAfterTheCommand_IsNotTakenForACommandName()
    {
        using TempDirectory root = new();
        Init(root);
        string unsigned = Commit(root, "unsigned");
        RunGit(root, "branch", "signoff");

        Result result = RunArguments(root, "signoff", "signoff", "--root", root.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(
            $"commit/{unsigned[..8]}(1,1): error ENG701: commit {unsigned[..8]} 'unsigned' has no Signed-off-by trailer" + Newline,
            result.Output);
    }

    [Fact]
    public void TheCommand_IsInTheRegistryOfTheApplication()
    {
        Assert.Contains(CommandRegistry.All, command => command is SignoffCommand && command.Name == "signoff");
    }

    private static Finding Error(string hash, string subject) => Finding.ForCommit(
        hash[..8],
        FindingSeverity.Error,
        EngCodes.Eng701,
        $"commit {hash[..8]} '{subject}' has no Signed-off-by trailer");

    private static List<Finding> Check(TempDirectory root, string range)
    {
        CheckContext context = new(
            root.Path,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["signoff"] = range });
        return [.. new SignoffCommand().Run(context)];
    }

    private static Result Run(TempDirectory root, string range) => RunArguments(root, "signoff", range, "--root", root.Path);

    private static Result RunArguments(TempDirectory root, params string[] args)
    {
        using StringWriter output = new() { NewLine = Newline };
        using StringWriter error = new() { NewLine = Newline };
        int exitCode = Program.Run(args, CommandRegistry.All, root.Path, output, error);
        return new Result(exitCode, output.ToString(), error.ToString());
    }

    private sealed record Result(int ExitCode, string Output, string Error);

    // The hooks of the host are not used: the hooks path points to a directory that does not exist.
    private static string[] Identity(TempDirectory root) =>
    [
        "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "-c", "merge.gpgsign=false",
        "-c", "core.hooksPath=" + Path.Combine(root.Path, "no-hooks"),
    ];

    private static void Init(TempDirectory root) => RunGit(root, "init", "-q", "-b", "main");

    private static void RunGit(TempDirectory root, params string[] arguments)
    {
        GitResult result = GitProcess.Run(root.Path, [.. Identity(root), .. arguments]);
        Assert.True(result.Succeeded, result.Error);
    }

    private static string Rev(TempDirectory root, string revision)
    {
        GitResult result = GitProcess.Run(root.Path, "rev-parse", revision);
        Assert.True(result.Succeeded, result.Error);
        return result.Output.Trim().Replace('\n', ' ');
    }

    // An empty commit with the message; the author is the default one or the given "Name <email>".
    private static string Commit(TempDirectory root, string message, string? author = null)
    {
        List<string> arguments = [.. Identity(root), "commit", "-q", "--allow-empty", "--cleanup=verbatim", "-F", "-"];
        if (author is not null)
        {
            arguments.Add("--author=" + author);
        }

        GitResult result = GitProcess.RunWithInput(root.Path, message + Newline, [.. arguments]);
        Assert.True(result.Succeeded, result.Error);
        return Rev(root, "HEAD");
    }
}

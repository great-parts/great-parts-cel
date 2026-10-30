using System;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Run;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using RunProgram = Great.Parts.Cel.Eng.Run.Program;
using TestRoot = Great.Parts.Cel.Eng.Tests.Support.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The arguments of `eng/run mutate`: the configuration file and the commit from which the changes are counted
// (docs/design/eng-mutate.md, "Steps").
public sealed class MutateOptionsTests
{
    [Fact]
    public void Parse_NoArgument_IsTheDefaultConfigurationAndNoCommit()
    {
        Assert.Equal(new MutateOptions("stryker-config.json", null), MutateOptions.Parse([]));
    }

    [Theory]
    [InlineData("eng/stryker-control.json", "--since", "abc123")]
    [InlineData("--since", "abc123", "eng/stryker-control.json")]
    public static void Parse_ConfigurationAndCommit_InAnyOrder(string first, string second, string third)
    {
        Assert.Equal(new MutateOptions("eng/stryker-control.json", "abc123"), MutateOptions.Parse([first, second, third]));
    }

    [Fact]
    public static void Parse_SinceWithoutACommit_IsRefused()
    {
        InvalidOperationException atTheEnd = Assert.Throws<InvalidOperationException>(() => MutateOptions.Parse(["--since"]));
        InvalidOperationException beforeAnOption = Assert.Throws<InvalidOperationException>(() => MutateOptions.Parse(["--since", "--other"]));

        Assert.Equal(["--since needs a commit", "--since needs a commit"], [atTheEnd.Message, beforeAnOption.Message]);
    }

    [Fact]
    public static void Parse_AnythingElse_IsRefused()
    {
        Assert.Equal("at most one configuration file", Assert.Throws<InvalidOperationException>(() => MutateOptions.Parse(["a.json", "b.json"])).Message);
        Assert.Equal("--since is given twice", Assert.Throws<InvalidOperationException>(() => MutateOptions.Parse(["--since", "abc", "--since", "def"])).Message);
        Assert.StartsWith("unknown option '--nope'", Assert.Throws<InvalidOperationException>(() => MutateOptions.Parse(["--nope"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MutateStages_WithACommit_BothTheToolAndTheCheckCountTheChangesFromIt()
    {
        string sha = new('a', 40);

        Step[] steps = [.. Commands.MutateStages(new MutateOptions("stryker-config.json", "main"), sha).SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        Assert.Equal(["tool", "build", "stryker", "run"], steps.Select(step => step.Arguments[0]));
        Assert.Equal(["stryker", "--config-file", "stryker-config.json", "--output", "artifacts/mutate", $"--since:{sha}"], steps[2].Arguments);
        Assert.Equal(["--config", "stryker-config.json", "--since", sha], steps[3].Arguments.TakeLast(4));
    }

    [Fact]
    public void MutateStages_WithoutACommit_TheArgumentsAreThoseOfTheConfiguration()
    {
        Step[] steps = [.. Commands.MutateStages(new MutateOptions("stryker-config.json", null), null).SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        Assert.Equal(["stryker", "--config-file", "stryker-config.json", "--output", "artifacts/mutate"], steps[2].Arguments);
        Assert.DoesNotContain("--since", steps[3].Arguments);
    }

    [Fact]
    public void ResolveSince_NoCommit_IsNull()
    {
        Assert.Null(new MutateOptions("stryker-config.json", null).ResolveSince());
    }

    // ---- since and linked worktrees ----

    [Theory]
    [InlineData("{ \"stryker-config\": { \"since\": { \"enabled\": true, \"target\": \"main\" } } }", true)]
    [InlineData("{ \"stryker-config\": { \"since\": { \"target\": \"main\" } } }", true)]
    [InlineData("{ \"since\": { } }", true)]
    [InlineData("{ \"stryker-config\": { \"since\": { \"enabled\": false } } }", false)]
    [InlineData("{ \"since\": { \"enabled\": false, }, // the control set\n }", false)]
    [InlineData("{ \"stryker-config\": { \"project\": \"a.csproj\" } }", false)]
    [InlineData("{ \"stryker-config\": { \"since\": true } }", true)]
    [InlineData("{ \"stryker-config\": [] }", true)]
    [InlineData("not json", true)]
    public static void UsesSince_ConfigurationFile_AsStrykerReadsIt(string text, bool expected)
    {
        using TempDirectory root = new();
        root.Write("c.json", text);

        Assert.Equal(expected, new MutateOptions("c.json", null).UsesSince(root.Path));
    }

    [Fact]
    public static void UsesSince_CommitGivenOrNoConfigurationFile_IsTrue()
    {
        using TempDirectory root = new();
        root.Write("c.json", "{ \"since\": { \"enabled\": false } }");

        Assert.True(new MutateOptions("c.json", "abc123").UsesSince(root.Path));
        Assert.True(new MutateOptions("missing.json", null).UsesSince(root.Path));
    }

    [Fact]
    public void UsesSince_ConfigurationsOfTheRepository_TheMainRunUsesItAndTheControlSetDoesNot()
    {
        string root = TestRoot.Find();

        Assert.True(new MutateOptions("stryker-config.json", null).UsesSince(root));
        Assert.False(new MutateOptions("eng/stryker-control.json", null).UsesSince(root));
    }

    [Fact]
    public void LinkedWorktree_OnlyARunWithSinceIsRefused()
    {
        string root = TestRoot.Find();
        const string Refused = "linked worktree: a run with since needs the main worktree";

        Assert.Null(RunProgram.LinkedWorktreeProblem(new MutateOptions("eng/stryker-control.json", null), linkedWorktree: true, root));
        Assert.Equal(Refused, RunProgram.LinkedWorktreeProblem(new MutateOptions("eng/stryker-control.json", "main"), linkedWorktree: true, root));
        Assert.Equal(Refused, RunProgram.LinkedWorktreeProblem(new MutateOptions("stryker-config.json", null), linkedWorktree: true, root));
        Assert.Null(RunProgram.LinkedWorktreeProblem(new MutateOptions("stryker-config.json", "main"), linkedWorktree: false, root));
    }

    [Fact]
    public void ResolveSince_ShortShaAndBranch_AreTheFullSha()
    {
        using TempDirectory root = new();
        root.Write("a.txt", "a\n");
        Commit(root);
        string full = Git.Run(root.Path, "rev-parse", "HEAD").Output.Trim();

        string? fromShort = new MutateOptions("c.json", full[..8]).ResolveSince(root.Path);
        string? fromBranch = new MutateOptions("c.json", "main").ResolveSince(root.Path);

        Assert.Equal(40, full.Length);
        Assert.Equal((full, full), (fromShort, fromBranch));
    }

    [Theory]
    [InlineData("no-such-branch")]
    [InlineData("HEAD~5")]
    public static void ResolveSince_NotACommit_IsRefused(string commit)
    {
        using TempDirectory root = new();
        root.Write("a.txt", "a\n");
        Commit(root);

        InvalidOperationException problem = Assert.Throws<InvalidOperationException>(() => new MutateOptions("c.json", commit).ResolveSince(root.Path));

        Assert.Equal($"--since '{commit}' is not a commit of this repository", problem.Message);
    }

    private static void Commit(TempDirectory root)
    {
        Assert.True(Git.Run(root.Path, "init", "-q", "-b", "main").Succeeded);
        Assert.True(Git.Run(root.Path, "add", "--all").Succeeded);
        GitResult commit = Git.Run(root.Path, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "base");
        Assert.True(commit.Succeeded, commit.Error);
    }
}

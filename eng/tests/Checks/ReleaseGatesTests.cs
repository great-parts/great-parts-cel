using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using GitProcess = Great.Parts.Cel.Eng.Checks.Git;
using TestRoot = Great.Parts.Cel.Eng.Tests.Support.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks release-gates` (docs/design/eng-release.md, "eng/checks release-gates"). Every gate has a red and a green case. The command runs
// through Program on a temporary git repository with the files of the gates, a temporary repository of cel-spec built by git fast-import
// with chosen commit dates, and replaced API of GitHub, gh, nuget.org and clock: no test goes to the network or waits.
public sealed class ReleaseGatesTests
{
    private const string Repository = "great-parts/great-parts-cel";
    private const string ChangelogDate = "2026-06-01";
    private const string RegressionPath = "tests/Great.Parts.Cel.Conformance/Regression/skips.md";
    private const string CelSpecPath = "tests/Great.Parts.Cel.Conformance/CelSpec/skips.md";
    private const string IssueLink = "https://github.com/great-parts/great-parts-cel/issues/12";
    private const string PackageName = "Great.Parts.Cel.1.2.0.nupkg";
    private const string ProtobufName = "Great.Parts.Cel.Protobuf.1.2.0.nupkg";

    // The text of Directory.Build.props and of a project under src/ at the commit of the long run, for the cases of the package metadata.
    private const string PropsBefore = "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags></PropertyGroup></Project>";
    private const string ProjectBefore = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description></PropertyGroup></Project>";

    // The entries of a package that the build made, without the signature of nuget.org.
    private static readonly (string Name, string Content)[] Entries = [("Great.Parts.Cel.nuspec", "nuspec"), ("lib/net10.0/Great.Parts.Cel.dll", "dll")];

    // The commits of the temporary repository of cel-spec: the first two are older than 6 weeks on 2026-06-01, the third is not.
    private static readonly (string Subject, DateTimeOffset When)[] CelSpecHistory =
    [
        ("first", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        ("second", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)),
        ("third", new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero)),
    ];

    [Fact]
    public void AllGatesGreen_NoFindingsAndExitCodeIsZero()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");

        Result result = fixture.Gates();

        Result.Expect(result, 0);
    }

    [Fact]
    public void TheCommand_IsInTheRegistryOfTheApplication()
    {
        Assert.Contains(CommandRegistry.All, command => command is ReleaseGatesCommand && command.Name == "release-gates");
    }

    // ENG901

    [Theory]
    [InlineData("# Changelog\n\n## [0.9.0] - 2026-05-01\n\n- Older.\n", "no entry for version 1.2.0")]
    [InlineData("# Changelog\n\n## [1.2.0] - 2026-13-45\n\n- Text.\n", "no entry for version 1.2.0")]
    [InlineData("# Changelog\n\n## [1.2.0] - 2026-06-01\n\n## [0.9.0] - 2026-05-01\n\n- Older.\n", "the entry for version 1.2.0 has no text")]
    public static void Eng901_ChangelogWithoutAnEntryWithTextForTheVersion_IsReported(string changelog, string message)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write("CHANGELOG.md", changelog);

        Result.Expect(fixture.Gates(), 1, "CHANGELOG.md(1,1): error " + EngCodes.Eng901 + ": " + message);
    }

    [Fact]
    public void Eng901_EntryWithOnlyASubheadingAndItsText_Passes()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write("CHANGELOG.md", "## [1.2.0] - 2026-06-01\n\n### Added\n\n- A thing.\n");

        Result.Expect(fixture.Gates(), 0);
    }

    // ENG902

    [Fact]
    public void Eng902_TagOnACommitOfABranchThatIsNotMain_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Git("switch", "-q", "-c", "side");
        fixture.Commit("on the side");
        fixture.Git("tag", "-f", "v1.2.0");
        string side = fixture.Git("rev-parse", "HEAD");

        Result.Expect(fixture.Gates(), 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng902}: tag v1.2.0 points to {side[..8]}, which is not on main");
    }

    [Fact]
    public void Eng902_TagOnAnOlderCommitOfMain_Passes()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Commit("after the tag");
        fixture.Git("update-ref", "refs/remotes/origin/main", "HEAD");

        Result.Expect(fixture.Gates(), 0);
    }

    // ENG903

    [Theory]
    [InlineData("pending", "S2", "unresolved divergence signature: 'sig(int) we-error' (S2)")]
    [InlineData("our-bug", "S1", "open divergence of the highest severity: 'sig(int) we-error' (S1), the verdict is our-bug")]
    public static void Eng903_UnresolvedOrOpenHighestSeverityRecord_IsReported(string verdict, string severity, string message)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write(RegressionPath, RegressionListText(RegressionRow(verdict, severity)));

        Result.Expect(fixture.Gates(), 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng903}: {message}");
    }

    [Theory]
    [InlineData("our-bug", "S2")]
    [InlineData("by-design", "S1")]
    [InlineData("cel-go-bug", "S1")]
    public static void Eng903_ResolvedOrLowerSeverityRecord_Passes(string verdict, string severity)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write(RegressionPath, RegressionListText(RegressionRow(verdict, severity)));

        Result.Expect(fixture.Gates(), 0);
    }

    // ENG904

    [Theory]
    [InlineData("workflow_dispatch", "success", 110, null)]
    [InlineData("workflow_dispatch", "failure", 125, null)]
    [InlineData("schedule", "success", 130, null)]
    [InlineData("workflow_dispatch", "success", 125, "0123456789abcdef0123456789abcdef01234567")]
    public static void Eng904_NoSuccessfulLongRunOnTheCommit_IsReported(string runEvent, string conclusion, int minutes, string? headSha)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Stubs.Api.Event = runEvent;
        fixture.Stubs.Api.Conclusion = conclusion;
        fixture.Stubs.Api.Minutes = minutes;
        fixture.Stubs.Api.HeadSha = headSha;

        Result.Expect(fixture.Gates(), 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]}");
    }

    [Fact]
    public void Eng904_SuccessfulDispatchRunOfTwoHoursFiveMinutesOnTheCommit_PassesAndAsksTheRunsOfTheWorkflow()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");

        Result.Expect(fixture.Gates(), 0);
        Assert.Contains(fixture.Stubs.Api.Paths, path => path.StartsWith("repos/" + Repository + "/actions/workflows/difftest-nightly.yml/runs?", StringComparison.Ordinal));
        Assert.Contains(fixture.Stubs.Api.Paths, path => path.StartsWith("repos/" + Repository + "/actions/runs/101/jobs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("docs/guide.md")]
    [InlineData("README.md")]
    [InlineData("eng/checks/Other.cs")]
    [InlineData("eng/tests/Other.cs")]
    [InlineData(".github/workflows/release.yml")]
    public static void Eng904_RunOnAnAncestorAndNoCodePathChangedSince_Passes(string changed)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        string run = fixture.ChangeAfterTheRun(changed);
        fixture.Stubs.Api.RunCommits = [run];

        Result.Expect(fixture.Gates(), 0);
    }

    [Theory]
    [InlineData("src/Lib.cs")]
    [InlineData("eng/difftest/Other.cs")]
    [InlineData("eng/oracle/main.go")]
    [InlineData("eng/run/Other.cs")]
    [InlineData("eng/compose.yaml")]
    [InlineData("tests/Great.Parts.Cel.Conformance/Regression/extra.md")]
    [InlineData("tests/Great.Parts.Cel.Conformance/Runner/Other.cs")]
    [InlineData("tests/cel-spec/extra.txt")]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Packages.props")]
    [InlineData("global.json")]
    [InlineData(".github/workflows/difftest-nightly.yml")]
    public static void Eng904_RunOnAnAncestorAndACodePathChangedSince_IsReportedWithTheRunAndThePath(string changed)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        string run = fixture.ChangeAfterTheRun(changed);
        fixture.Stubs.Api.RunCommits = [run];

        Result.Expect(
            fixture.Gates(),
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]} or on an ancestor with no change of the code: the closest run is on {run[..8]}, and these paths changed since it: {changed}");
    }

    // A file of the package metadata (Directory.Build.props, a project under src/) whose change is only a change of the metadata of the
    // package does not cancel the run: the assembly and the program of the run stay the same.
    [Theory]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags><PackageReadmeFile>README.md</PackageReadmeFile></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project>\n  <!-- A new comment. -->\n  <PropertyGroup>\n    <Nullable>enable</Nullable>\n    <PackageTags>cel;rules</PackageTags>\n  </PropertyGroup>\n</Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description><PackageReadmeFile>README.md</PackageReadmeFile></PropertyGroup><ItemGroup><None Include=\"../../eng/packages/Lib.README.md\" Pack=\"true\" PackagePath=\"README.md\" Visible=\"false\" /></ItemGroup></Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description></PropertyGroup><PropertyGroup><PackageProjectUrl>https://example.test</PackageProjectUrl></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags><EmbedUntrackedSources>true</EmbedUntrackedSources></PropertyGroup></Project>")]
    public static void Eng904_RunOnAnAncestorAndOnlyThePackageMetadataChangedSince_Passes(string path, string before, string after)
    {
        using Fixture fixture = FixtureWithFileChangedAfterTheRun(path, before, after, out _);

        Result.Expect(fixture.Gates(), 0);
    }

    [Theory]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>disable</Nullable><PackageTags>cel</PackageTags></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><PackageTags>cel</PackageTags><PackageReadmeFile>README.md</PackageReadmeFile></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags><Version>1.0.1</Version></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags><Company>Other</Company></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags><NeutralLanguage>ru</NeutralLanguage></PropertyGroup></Project>")]
    [InlineData("Directory.Build.props", PropsBefore, "<Project><PropertyGroup><Nullable>enable</Nullable><PackageTags>cel</PackageTags></PropertyGroup>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>other</Description></PropertyGroup></Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0;net11.0</TargetFrameworks><Description>d</Description></PropertyGroup></Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description></PropertyGroup><ItemGroup><None Include=\"x.txt\" /></ItemGroup></Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description></PropertyGroup><Target Name=\"T\"><ItemGroup><None Include=\"x.txt\" Pack=\"true\" /></ItemGroup></Target></Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description></PropertyGroup><ItemGroup><Compile Include=\"x.cs\" Pack=\"true\" /></ItemGroup></Project>")]
    [InlineData("src/Lib/Lib.csproj", ProjectBefore, "this is not a project file")]
    [InlineData("src/Lib/Sub/Other.csproj", ProjectBefore, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFrameworks>net10.0</TargetFrameworks><Description>d</Description><PackageReadmeFile>README.md</PackageReadmeFile></PropertyGroup></Project>")]
    public static void Eng904_RunOnAnAncestorAndAMetadataFileChangedBeyondThePackageMetadataSince_IsReportedWithThePath(string path, string before, string after)
    {
        using Fixture fixture = FixtureWithFileChangedAfterTheRun(path, before, after, out string run);

        Result.Expect(
            fixture.Gates(),
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]} or on an ancestor with no change of the code: the closest run is on {run[..8]}, and these paths changed since it: {path}");
    }

    private static Fixture FixtureWithFileChangedAfterTheRun(string path, string before, string after, out string run)
    {
        Fixture fixture = Fixture.Create("v1.2.0");
        run = fixture.ChangeFileAfterTheRun(path, before, after);
        fixture.Stubs.Api.RunCommits = [run];
        return fixture;
    }

    [Fact]
    public void Eng904_RunOnAnAncestorAndTheMetadataFileIsNewSince_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        string run = fixture.ChangeAfterTheRun("src/Lib/Lib.csproj");
        fixture.Stubs.Api.RunCommits = [run];

        Result.Expect(
            fixture.Gates(),
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]} or on an ancestor with no change of the code: the closest run is on {run[..8]}, and these paths changed since it: src/Lib/Lib.csproj");
    }

    [Fact]
    public void Eng904_TwoRunsOnAncestors_TheClosestWithoutCodeChangesIsTaken()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        string older = fixture.ChangeAfterTheRun("src/Lib.cs");
        string nearer = fixture.ChangeAfterTheRun("docs/guide.md");
        fixture.Stubs.Api.RunCommits = [older, nearer];

        Result.Expect(fixture.Gates(), 0);
        Assert.Contains(fixture.Stubs.Api.Paths, path => path.StartsWith("repos/" + Repository + "/actions/runs/102/jobs", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Stubs.Api.Paths, path => path.StartsWith("repos/" + Repository + "/actions/runs/101/jobs", StringComparison.Ordinal));
    }

    [Fact]
    public void Eng904_TwoRunsOnAncestorsWithCodeChangesSince_TheMessageNamesTheClosest()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        string older = fixture.ChangeAfterTheRun("src/Lib.cs");
        string nearer = fixture.ChangeAfterTheRun("src/Other.cs");
        fixture.Stubs.Api.RunCommits = [older, nearer];

        Result.Expect(
            fixture.Gates(),
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]} or on an ancestor with no change of the code: the closest run is on {nearer[..8]}, and these paths changed since it: src/Other.cs");
    }

    [Fact]
    public void Eng904_ShortRunOnAnAncestorWithoutCodeChanges_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        string run = fixture.ChangeAfterTheRun("docs/guide.md");
        fixture.Stubs.Api.RunCommits = [run];
        fixture.Stubs.Api.Minutes = 110;

        Result.Expect(fixture.Gates(), 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]}");
    }

    [Fact]
    public void Eng904_RunOnACommitThatIsNotAnAncestor_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Git("switch", "-q", "-c", "side");
        fixture.Commit("on the side");
        string side = fixture.Git("rev-parse", "HEAD");
        fixture.Git("switch", "-q", "main");
        fixture.Stubs.Api.RunCommits = [side];

        Result.Expect(fixture.Gates(), 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng904}: no successful long run of difftest-nightly.yml (at least 2 h) on commit {fixture.Head[..8]}");
    }

    // ENG908

    [Theory]
    [InlineData("ci.yml")]
    [InlineData("difftest-main.yml")]
    public static void Eng908_WorkflowThatDidNotSucceedOnTheCommit_IsReported(string workflow)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Stubs.Api.RedWorkflows.Add(workflow);

        Result.Expect(fixture.Gates(), 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng908}: no successful run of {workflow} on commit {fixture.Head[..8]}");
    }

    [Fact]
    public void Eng908_RunsOfBothWorkflowsOnAnotherCommit_AreReportedInOrder()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Stubs.Api.WorkflowHeadSha = "0123456789abcdef0123456789abcdef01234567";

        Result.Expect(
            fixture.Gates(),
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng908}: no successful run of ci.yml on commit {fixture.Head[..8]}",
            $"tag/v1.2.0(1,1): error {EngCodes.Eng908}: no successful run of difftest-main.yml on commit {fixture.Head[..8]}");
    }

    [Fact]
    public void Eng908_SuccessfulPushRunsOfBothWorkflowsOnTheCommit_PassAndAskThePushRuns()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");

        Result.Expect(fixture.Gates(), 0);
        Assert.Contains(fixture.Stubs.Api.Paths, path => path.StartsWith("repos/" + Repository + "/actions/workflows/ci.yml/runs?event=push&head_sha=" + fixture.Head, StringComparison.Ordinal));
        Assert.Contains(fixture.Stubs.Api.Paths, path => path.StartsWith("repos/" + Repository + "/actions/workflows/difftest-main.yml/runs?event=push&head_sha=" + fixture.Head, StringComparison.Ordinal));
    }

    // ENG905

    [Fact]
    public void Eng905_CommitOlderThanSixWeeksAfterThePinnedOne_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write("tests/cel-spec/COMMIT", fixture.CelSpecCommits[0] + "\n");

        Result.Expect(
            fixture.Gates(),
            1,
            $"tests/cel-spec/COMMIT(1,1): error {EngCodes.Eng905}: pinned cel-spec commit {fixture.CelSpecCommits[0][..8]} does not include master commit {fixture.CelSpecCommits[1][..8]} of 2026-02-01, older than 6 weeks");
    }

    [Fact]
    public void Eng905_PinnedCommitThatIsNotInTheHistoryOfMaster_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write("tests/cel-spec/COMMIT", new string('a', 40) + "\n");

        Result.Expect(fixture.Gates(), 1, $"tests/cel-spec/COMMIT(1,1): error {EngCodes.Eng905}: pinned cel-spec commit aaaaaaaa is not in the history of master");
    }

    [Fact]
    public void Eng905_PinnedCommitIsTheNewestOneOlderThanSixWeeks_PassesWhateverIsNewer()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write("tests/cel-spec/COMMIT", fixture.CelSpecCommits[1] + "\n");

        Result.Expect(fixture.Gates(), 0);
    }

    [Fact]
    public void Eng905_TheDateOfTheReleaseMovesTheLimit()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");

        // On 2026-07-10 the commit of 2026-05-20 is older than 6 weeks too, and the pinned commit does not include it.
        Result result = fixture.Gates("--date", "2026-07-10");

        Result.Expect(result, 1, $"tests/cel-spec/COMMIT(1,1): error {EngCodes.Eng905}: pinned cel-spec commit {fixture.CelSpecCommits[1][..8]} does not include master commit {fixture.CelSpecCommits[2][..8]} of 2026-05-20, older than 6 weeks");
    }

    // ENG803

    [Theory]
    [InlineData("", IssueLink, "entry 'x/y/z' has no reason")]
    [InlineData("A reason.", "https://github.com/other/repo/issues/3", "entry 'x/y/z' has no issue link to great-parts/great-parts-cel")]
    [InlineData("A reason.", "", "entry 'x/y/z' has no issue link to great-parts/great-parts-cel")]
    public static void Eng803_EntryWithoutReasonOrWithoutLinkToTheRepository_IsReported(string reason, string issue, string message)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Root.Write(CelSpecPath, CelSpecListText(CelSpecRow(reason, "A link.", issue)));

        Result.Expect(fixture.Gates(), 1, $"{CelSpecPath}(5,1): error {EngCodes.Eng803}: {message}");
    }

    // ENG906

    [Fact]
    public void Eng906_TagV100AndAnEntryWithoutReference_IsReported()
    {
        using Fixture fixture = Fixture.Create("v1.0.0");
        fixture.Root.Write(CelSpecPath, CelSpecListText(CelSpecRow("A reason.", string.Empty, IssueLink)));

        Result.Expect(fixture.Gates(), 1, $"{CelSpecPath}(5,1): error {EngCodes.Eng906}: release 1.0.0: entry 'x/y/z' has no reference to the language definition or a minimal case");
    }

    [Fact]
    public void Eng906_TagV100AndAnEmptyList_Passes()
    {
        using Fixture fixture = Fixture.Create("v1.0.0");
        fixture.Root.Write(CelSpecPath, CelSpecListText());

        Result.Expect(fixture.Gates(), 0);
    }

    [Fact]
    public void Eng906_AnotherVersionAndAnEntryWithoutReference_Passes()
    {
        using Fixture fixture = Fixture.Create("v1.0.1");
        fixture.Root.Write(CelSpecPath, CelSpecListText(CelSpecRow("A reason.", string.Empty, IssueLink)));

        Result.Expect(fixture.Gates(), 0);
    }

    // ENG907: the check of origin

    [Fact]
    public void Eng907_ExitWithAnError_IsReportedWithTheFirstLineAfterFiveRepeats()
    {
        Stubs stubs = new();
        stubs.Gh.FailFirst = int.MaxValue;
        stubs.Gh.Text = "Error: verifying with issuer sigstore.dev\nsecond line";
        using TempDirectory packages = Packages(signed: true);

        Result result = Execute(packages.Path, stubs, "v1.2.0", "--provenance", packages.Path);

        Result.Expect(result, 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng907}: provenance check failed for {PackageName}: Error: verifying with issuer sigstore.dev");
        Assert.Equal(6, stubs.Gh.Calls.Count);
        Assert.Equal(Enumerable.Repeat(TimeSpan.FromSeconds(15), 5), stubs.Clock.Delays);
    }

    [Fact]
    public void Eng907_AttestationThatIsVisibleAfterAFewTries_Passes()
    {
        Stubs stubs = new();
        stubs.Gh.FailFirst = 2;
        using TempDirectory packages = Packages(signed: true);

        Result.Expect(Execute(packages.Path, stubs, "v1.2.0", "--provenance", packages.Path), 0);
        Assert.Equal(3, stubs.Gh.Calls.Count);
    }

    [Fact]
    public void Eng907_FileWithTheSignatureOfNuGet_ReachesGhWithoutAChange()
    {
        Stubs stubs = new();
        using TempDirectory packages = Packages(signed: true);
        string file = Path.Combine(packages.Path, PackageName);

        Result.Expect(Execute(packages.Path, stubs, "v1.2.0", "--provenance", packages.Path), 0);
        Assert.Equal(file, stubs.Gh.Calls[0][2]);
        Assert.Equal(File.ReadAllBytes(file), stubs.Gh.Files[0]);
        Assert.Contains(".signature.p7s", EntryNames(stubs.Gh.Files[0]));
    }

    [Fact]
    public void Eng907_AttestationThatIsVisibleAfterFiveFailedCalls_PassesOnTheLastCall()
    {
        Stubs stubs = new();
        stubs.Gh.FailFirst = 5;
        using TempDirectory packages = Packages(signed: true);

        Result.Expect(Execute(packages.Path, stubs, "v1.2.0", "--provenance", packages.Path), 0);
        Assert.Equal(6, stubs.Gh.Calls.Count);
        Assert.Equal(Enumerable.Repeat(TimeSpan.FromSeconds(15), 5), stubs.Clock.Delays);
    }

    // ENG907: the content of the downloaded packages against the packages of the build

    [Fact]
    public void Eng907_SameContent_PackagesThatDifferOnlyBySignatureAndZipBytes_Pass()
    {
        Result result = Compare([(".signature.p7s", "signature"), .. Entries], [.. Enumerable.Reverse(Entries)], "great.parts.cel.1.2.0.nupkg", ProtobufName);

        Result.Expect(result, 0);
    }

    [Theory]
    [InlineData("changed", "the entry 'lib/net10.0/Great.Parts.Cel.dll' differs from the built package")]
    [InlineData("missing", "the entry 'lib/net10.0/Great.Parts.Cel.dll' is missing")]
    [InlineData("extra", "the entry 'extra.txt' is not in the built package")]
    public static void Eng907_SameContent_PackageThatDiffersFromTheBuild_IsReportedWithTheFirstEntry(string kind, string message)
    {
        (string Name, string Content)[] served = kind switch
        {
            "missing" => [Entries[0]],
            "extra" => [.. Entries, ("extra.txt", "x")],
            _ => Entries,
        };
        (string Name, string Content)[] built = kind == "changed" ? [Entries[0], (Entries[1].Name, "other")] : Entries;

        Result result = Compare(served, built, PackageName, ProtobufName);

        Result.Expect(
            result,
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng907}: content check failed for {PackageName}: {message}",
            $"tag/v1.2.0(1,1): error {EngCodes.Eng907}: content check failed for {ProtobufName}: {message}");
    }

    [Fact]
    public void Eng907_SameContent_PackageWithoutACounterpartInTheBuild_IsReported()
    {
        Result result = Compare(Entries, Entries, ProtobufName);

        Result.Expect(result, 1, $"tag/v1.2.0(1,1): error {EngCodes.Eng907}: content check failed for {PackageName}: the build has no package of that name");
    }

    [Theory]
    [InlineData("great-parts/great-parts-cel", "v1.2.0")]
    [InlineData("other/library", "v2.0.0-rc.1")]
    public static void Eng907_FlagsThatGhGets_AreTheFlagsOfTheRepositoryAndTheTag(string repository, string tag)
    {
        Stubs stubs = new();
        using TempDirectory packages = Packages(signed: false);

        Result.Expect(Execute(packages.Path, stubs, tag, "--provenance", packages.Path, "--repository", repository), 0);
        Assert.Equal(ProvenanceFlags.Arguments(repository, tag), stubs.Gh.Calls[0].Skip(3));
        Assert.Equal(
            $"--repo {repository} --signer-workflow {repository}/.github/workflows/release.yml --source-ref refs/tags/{tag} --deny-self-hosted-runners",
            ProvenanceFlags.Line(repository, tag));
    }

    // ENG907: the download

    [Fact]
    public void Eng907_Download_WritesBothPackagesAsNuGetGivesThemAfterWaitingForThem()
    {
        Stubs stubs = new();
        byte[] content = Zip(signed: true);
        stubs.Source.Handler = _ => stubs.Clock.Elapsed >= TimeSpan.FromSeconds(60) ? new PackageAnswer(true, content, string.Empty) : NotFound;
        using TempDirectory target = new();

        Result.Expect(Execute(target.Path, stubs, "v1.2.0", "--nuget", Path.Combine(target.Path, "nuget")), 0);

        Assert.Equal(content, File.ReadAllBytes(Path.Combine(target.Path, "nuget", PackageName)));
        Assert.Equal(content, File.ReadAllBytes(Path.Combine(target.Path, "nuget", "Great.Parts.Cel.Protobuf.1.2.0.nupkg")));
        Assert.Equal(
            [
                "https://api.nuget.org/v3-flatcontainer/great.parts.cel/1.2.0/great.parts.cel.1.2.0.nupkg",
                "https://api.nuget.org/v3-flatcontainer/great.parts.cel.protobuf/1.2.0/great.parts.cel.protobuf.1.2.0.nupkg",
            ],
            stubs.Source.Requests.Distinct().Select(address => address.AbsoluteUri));
        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)], stubs.Clock.Delays);
    }

    [Fact]
    public void Eng907_PackageThatNuGetDoesNotHaveForFifteenMinutes_IsReported()
    {
        Stubs stubs = new();
        using TempDirectory target = new();

        Result result = Execute(target.Path, stubs, "v1.2.0", "--nuget", target.Path);

        Result.Expect(
            result,
            1,
            $"tag/v1.2.0(1,1): error {EngCodes.Eng907}: {PackageName} is not on nuget.org after 15 minutes: the status is 404",
            $"tag/v1.2.0(1,1): error {EngCodes.Eng907}: Great.Parts.Cel.Protobuf.1.2.0.nupkg is not on nuget.org after 15 minutes: the status is 404");
        Assert.Equal(TimeSpan.FromMinutes(15), stubs.Clock.Elapsed);
        Assert.False(File.Exists(Path.Combine(target.Path, PackageName)));
    }

    // The invocation

    [Fact]
    public void Tag_ThatIsNotAVersion_IsExitCodeTwo()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");

        Result result = Execute(fixture.Root.Path, fixture.Stubs, "1.2.0");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("eng/checks: the tag '1.2.0' is not v and a semantic version, such as v1.0.0", result.Error);
    }

    [Fact]
    public void MissingOriginMain_IsExitCodeTwo()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Git("update-ref", "-d", "refs/remotes/origin/main");

        Result result = fixture.Gates();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("origin/main does not exist", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingList_IsExitCodeTwo()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        File.Delete(Path.Combine(fixture.Root.Path, RegressionPath));

        Result result = fixture.Gates();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("eng/checks: the list " + RegressionPath + " does not exist", result.Error);
    }

    [Fact]
    public void ApiThatDoesNotAnswer_IsExitCodeTwoNotAMissingRun()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Stubs.Api.Down = true;

        Result result = fixture.Gates();

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("the API of GitHub did not answer", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TagThatDoesNotExist_NeedsTheCommitOfTheRun_AndThenTheGatesRunOnIt()
    {
        using Fixture fixture = Fixture.Create("v1.2.0");
        fixture.Git("tag", "-d", "v1.2.0");

        Result without = fixture.Gates();
        Result with = fixture.Gates("--commit", fixture.Head);

        Assert.Equal(2, without.ExitCode);
        Assert.Contains("--commit", without.Error, StringComparison.Ordinal);
        Result.Expect(with, 0);
    }

    [Theory]
    [InlineData("--date", "2026-6-1")]
    [InlineData("--repository", "not a repository")]
    [InlineData("--commit", "--not-a-hash")]
    public static void OptionWithAValueThatCannotBeUsed_IsExitCodeTwo(string option, string value)
    {
        using Fixture fixture = Fixture.Create("v1.2.0");

        Result result = Execute(fixture.Root.Path, fixture.Stubs, "v1.2.0", option, value);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void ModesAndTheirOptions_AreCheckedBeforeAnythingRuns()
    {
        Stubs stubs = new();
        using TempDirectory directory = new();

        Result twoModes = Execute(directory.Path, stubs, "v1.2.0", "--provenance", directory.Path, "--nuget", directory.Path);
        Result alone = Execute(directory.Path, stubs, "v1.2.0", "--same-content-as", directory.Path);
        Result empty = Execute(directory.Path, stubs, "v1.2.0", "--provenance", directory.Path);

        Assert.Equal(2, twoModes.ExitCode);
        Assert.Equal(2, alone.ExitCode);
        Assert.Equal("eng/checks: the option --same-content-as needs the option --nuget", alone.Error);
        Assert.Equal(2, empty.ExitCode);
        Assert.Empty(stubs.Gh.Calls);
    }

    // The reader of the lists

    [Fact]
    public void ListsOfTheRepository_AreReadByTheReaderOfTheGates()
    {
        string root = TestRoot.Find();

        IReadOnlyList<ListRecord> regression = MarkdownList.Read(root, RegressionListGate.ListPath, "Signature", "Verdict", "Severity");
        IReadOnlyList<ListRecord> celSpec = CelSpecList.Read(root, "Case", "Reason", "Reference", "Issue");

        Assert.NotEmpty(regression);
        Assert.All(regression, record => Assert.NotEmpty(record["Signature"]));
        Assert.NotEmpty(celSpec);
        Assert.All(celSpec, record => Assert.NotEmpty(record["Reason"]));
    }

    [Fact]
    public void BarInACell_IsWrittenWithABackslash_AndColumnsAreFoundByName()
    {
        using TempDirectory root = new();
        root.Write("list.md", "Text.\n\n| Reason | Case |\n| :- | :- |\n| a \\| b | x |\n");

        IReadOnlyList<ListRecord> records = MarkdownList.Read(root.Path, "list.md", "Case", "Reason");

        ListRecord record = Assert.Single(records);
        Assert.Equal(5, record.Line);
        Assert.Equal("a | b", record["Reason"]);
        Assert.Equal("x", record["Case"]);
    }

    // The fixture and the stubs

    private static readonly PackageAnswer NotFound = new(false, [], "the status is 404");

    private static string RegressionListText(params string[] rows) =>
        "# Regression corpus skip list\n\n| Case | Scope | Signature | Verdict | Severity | Reason | Issue |\n|---|---|---|---|---|---|---|\n" + string.Concat(rows.Select(row => row + "\n"));

    private static string RegressionRow(string verdict, string severity) =>
        $"| c/a/b | * * * | sig(int) we-error | {verdict} | {severity} | A reason. | |";

    private static string CelSpecListText(params string[] rows) =>
        "# cel-spec skip list\n\n| Case | Scope | Reason | Reference | Issue |\n|---|---|---|---|---|\n" + string.Concat(rows.Select(row => row + "\n"));

    private static string CelSpecRow(string reason, string reference, string issue) =>
        $"| x/y/z | * * * | {reason} | {reference} | {issue} |";

    private static TempDirectory Packages(bool signed)
    {
        TempDirectory directory = new();
        File.WriteAllBytes(Path.Combine(directory.Path, PackageName), Zip(signed));
        return directory;
    }

    private static byte[] Zip(bool signed)
    {
        string[] entries = signed
            ? [".signature.p7s", "Great.Parts.Cel.nuspec", "lib/net10.0/Great.Parts.Cel.dll"]
            : ["Great.Parts.Cel.nuspec", "lib/net10.0/Great.Parts.Cel.dll"];
        return Archive(entries.Select(entry => (entry, "content of " + entry)));
    }

    private static byte[] Archive(IEnumerable<(string Name, string Content)> entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                using StreamWriter writer = new(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    // nuget.org serves `served` as both packages of the version; the directory of the build has the packages of the given file names with
    // the content `built`. The result is that of the download with --same-content-as.
    private static Result Compare(IEnumerable<(string Name, string Content)> served, IEnumerable<(string Name, string Content)> built, params string[] builtNames)
    {
        Stubs stubs = new();
        byte[] content = Archive(served);
        stubs.Source.Handler = _ => new PackageAnswer(true, content, string.Empty);
        using TempDirectory root = new();
        string directory = Path.Combine(root.Path, "built");
        Directory.CreateDirectory(directory);
        foreach (string name in builtNames)
        {
            File.WriteAllBytes(Path.Combine(directory, name), Archive(built));
        }

        return Execute(root.Path, stubs, "v1.2.0", "--nuget", Path.Combine(root.Path, "nuget"), "--same-content-as", directory);
    }

    private static string[] EntryNames(byte[] package)
    {
        using ZipArchive archive = new(new MemoryStream(package), ZipArchiveMode.Read);
        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    private static Result Execute(string root, Stubs stubs, string tag, params string[] options)
    {
        ReleaseGatesCommand command = new(_ => stubs.Api, stubs.Gh, stubs.Source, stubs.Clock);
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new() { NewLine = "\n" };
        int exitCode = Program.Run(["release-gates", tag, "--root", root, .. options], [command], root, output, error);
        return new Result(exitCode, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString().Trim());
    }

    private static string RunGit(string directory, params string[] arguments)
    {
        string[] identity =
        [
            "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false",
            "-c", "core.hooksPath=" + Path.Combine(directory, "no-hooks"),
        ];
        GitResult result = GitProcess.Run(directory, [.. identity, .. arguments]);
        Assert.True(result.Succeeded, result.Error);
        return result.Output.Trim();
    }

    private sealed record Result(int ExitCode, string[] Lines, string Error)
    {
        // The exit code is 1 for findings and 0 for none; every line has the form of a finding and the lines are exactly the expected ones.
        internal static void Expect(Result actual, int exitCode, params string[] lines)
        {
            Assert.Equal(string.Empty, actual.Error);
            Assert.Equal(lines, actual.Lines);
            Assert.All(actual.Lines, line => Assert.Matches(Finding.Pattern, line));
            Assert.Equal(exitCode, actual.ExitCode);
        }
    }

    private sealed class Stubs
    {
        internal StubApi Api { get; } = new();

        internal StubGh Gh { get; } = new();

        internal StubSource Source { get; } = new();

        internal FakeClock Clock { get; } = new();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory _celSpec;

        private Fixture(string tag, TempDirectory root, TempDirectory celSpec, string[] celSpecCommits)
        {
            Tag = tag;
            Root = root;
            _celSpec = celSpec;
            CelSpecCommits = celSpecCommits;
        }

        internal string Tag { get; }

        internal TempDirectory Root { get; }

        internal string[] CelSpecCommits { get; }

        internal Stubs Stubs { get; } = new();

        internal string Head => Git("rev-parse", "HEAD");

        // A repository whose tag is on the head of main, with the files of every gate in the green form.
        internal static Fixture Create(string tag)
        {
            TempDirectory celSpec = new();
            RunGit(celSpec.Path, "init", "-q", "-b", "master");
            StringBuilder stream = new();
            foreach ((string subject, DateTimeOffset moment) in CelSpecHistory)
            {
                string message = subject + "\n";
                string time = moment.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                string length = Encoding.UTF8.GetByteCount(message).ToString(CultureInfo.InvariantCulture);
                stream.Append("commit refs/heads/master\ncommitter Test <test@example.com> ").Append(time).Append(" +0000\ndata ").Append(length).Append('\n').Append(message).Append('\n');
            }

            GitResult imported = GitProcess.RunWithInput(celSpec.Path, stream.ToString(), "fast-import", "--quiet");
            Assert.True(imported.Succeeded, imported.Error);
            string[] commits = RunGit(celSpec.Path, "log", "master", "--format=%H", "--reverse").Split('\n');

            TempDirectory root = new();
            RunGit(root.Path, "init", "-q", "-b", "main");
            root.Write("CHANGELOG.md", $"# Changelog\n\n## [{tag[1..]}] - {ChangelogDate}\n\n- Added a thing.\n\n## [0.9.0] - 2026-05-01\n\n- An older entry.\n");
            root.Write(RegressionPath, RegressionListText(RegressionRow("by-design", "S1")));
            root.Write(CelSpecPath, CelSpecListText(CelSpecRow("A reason.", "A link.", IssueLink)));
            root.Write("tests/cel-spec/COMMIT", commits[1] + "\n");
            Fixture fixture = new(tag, root, celSpec, commits);
            fixture.Git("add", "-A");
            fixture.Commit("the content of the release");
            fixture.Git("tag", tag);
            fixture.Git("update-ref", "refs/remotes/origin/main", "HEAD");
            fixture.Stubs.Api.TagCommit = fixture.Head;
            return fixture;
        }

        // All gates through the command line, on the date and the repository of cel-spec of the fixture.
        internal Result Gates(params string[] options) =>
            Execute(Root.Path, Stubs, Tag, ["--date", ChangelogDate, "--repository", Repository, "--cel-spec", _celSpec.Path, .. options]);

        internal string Git(params string[] arguments) => RunGit(Root.Path, arguments);

        // A commit after the long run that adds one file, and the tag and origin/main move to it. Returns the commit that the long run is on.
        internal string ChangeAfterTheRun(string path)
        {
            string run = Head;
            Root.Write(path, "changed\n");
            Git("add", "-A");
            Commit("change " + path);
            Git("tag", "-f", Tag);
            Git("update-ref", "refs/remotes/origin/main", "HEAD");
            return run;
        }

        // As ChangeAfterTheRun, for a file that has the text `before` on the commit of the long run and the text `after` on the tag.
        internal string ChangeFileAfterTheRun(string path, string before, string after)
        {
            Root.Write(path, before);
            Git("add", "-A");
            Commit("add " + path);
            string run = Head;
            Root.Write(path, after);
            Git("add", "-A");
            Commit("change " + path);
            Git("tag", "-f", Tag);
            Git("update-ref", "refs/remotes/origin/main", "HEAD");
            return run;
        }

        internal void Commit(string message) => Git("commit", "-q", "--allow-empty", "-m", message);

        public void Dispose()
        {
            Root.Dispose();
            _celSpec.Dispose();
        }
    }

    private sealed class StubApi : IGitHubApi
    {
        internal string Event { get; set; } = "workflow_dispatch";

        internal string Conclusion { get; set; } = "success";

        internal string? HeadSha { get; set; }

        // The commit of the tag, which a long run is on unless a test says otherwise, and the commits of the long runs when a test gives them.
        internal string TagCommit { get; set; } = string.Empty;

        internal string[]? RunCommits { get; set; }

        internal int Minutes { get; set; } = 125;

        internal bool Down { get; set; }

        // The workflows of the gate ENG908 whose run did not succeed, and the commit of their run when it is not the asked one.
        internal HashSet<string> RedWorkflows { get; } = [];

        internal string? WorkflowHeadSha { get; set; }

        internal List<string> Paths { get; } = [];

        public string Get(string path)
        {
            Paths.Add(path);
            if (Down)
            {
                throw new CheckInputException("the API of GitHub did not answer " + path);
            }

            if (path.Contains("/jobs", StringComparison.Ordinal))
            {
                DateTimeOffset start = new(2026, 5, 30, 1, 0, 0, TimeSpan.Zero);
                string started = start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                string completed = start.AddMinutes(Minutes).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                return $$"""{"jobs":[{"name":"Differential run","conclusion":"success","started_at":"{{started}}","completed_at":"{{completed}}"}]}""";
            }

            // The runs of the long run: on the commits of the test, or on the commit of the tag, or on the one that a test gives.
            if (path.Contains("/workflows/difftest-nightly.yml/", StringComparison.Ordinal))
            {
                string[] commits = RunCommits ?? [HeadSha ?? TagCommit];
                IEnumerable<string> runs = commits.Select((commit, index) =>
                    $$"""{"id":{{101 + index}},"event":"{{Event}}","head_sha":"{{commit}}","conclusion":"{{Conclusion}}"}""");
                return $$"""{"workflow_runs":[{{string.Join(",", runs)}}]}""";
            }

            // The run is on the commit that the gate asks for, unless a test gives another one.
            int at = path.IndexOf("head_sha=", StringComparison.Ordinal) + "head_sha=".Length;
            string asked = path[at..].Split('&')[0];
            if (path.Contains("/workflows/ci.yml/", StringComparison.Ordinal) || path.Contains("/workflows/difftest-main.yml/", StringComparison.Ordinal))
            {
                string workflow = path.Contains("/workflows/ci.yml/", StringComparison.Ordinal) ? "ci.yml" : "difftest-main.yml";
                string conclusion = RedWorkflows.Contains(workflow) ? "failure" : "success";
                return $$"""{"workflow_runs":[{"id":201,"event":"push","head_sha":"{{WorkflowHeadSha ?? asked}}","conclusion":"{{conclusion}}"}]}""";
            }

            return $$"""{"workflow_runs":[{"id":101,"event":"{{Event}}","head_sha":"{{HeadSha ?? asked}}","conclusion":"{{Conclusion}}"}]}""";
        }
    }

    private sealed class StubGh : IGh
    {
        internal int FailFirst { get; set; }

        internal string Text { get; set; } = "Error: no attestations found";

        internal List<IReadOnlyList<string>> Calls { get; } = [];

        internal List<byte[]> Files { get; } = [];

        public GhResult Run(IReadOnlyList<string> arguments)
        {
            Calls.Add(arguments);
            Files.Add(File.ReadAllBytes(arguments[2]));
            return Calls.Count <= FailFirst ? new GhResult(1, Text) : new GhResult(0, "Loaded digest and verified");
        }
    }

    private sealed class StubSource : IPackageSource
    {
        internal Func<Uri, PackageAnswer> Handler { get; set; } = _ => NotFound;

        internal List<Uri> Requests { get; } = [];

        public PackageAnswer Fetch(Uri address)
        {
            Requests.Add(address);
            return Handler(address);
        }
    }

    private sealed class FakeClock : IReleaseClock
    {
        internal List<TimeSpan> Delays { get; } = [];

        internal TimeSpan Elapsed { get; private set; }

        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero) + Elapsed;

        public void Delay(TimeSpan time)
        {
            Delays.Add(time);
            Elapsed += time;
        }
    }
}

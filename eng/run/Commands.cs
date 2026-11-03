using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>The commands of <c>eng/</c> and their stages.</summary>
internal static class Commands
{
    /// <summary>The solution of the repository; also the marker of the repository root.</summary>
    internal const string Solution = "Great.Parts.Cel.slnx";

    /// <summary>The names of the commands, as the messages of <see cref="Program"/> list them.</summary>
    internal const string Known = "test, docs, mutate, difftest, min-sdk";

    /// <summary>The runtime identifier of the Native AOT publish, also the label of its test count.</summary>
    private const string AotRuntime = "linux-x64";

    /// <summary>The checks of <c>eng/checks</c> that <c>test</c> runs; <c>eng/checks</c> is built once, with the solution, and run without a build.</summary>
    private static readonly string[] Checks = ["suppressions", "duplicates", "ratchet", "packages", "--no-build"];

    /// <summary>The error codes page that <c>docs</c> checks against <c>CelErrorCode</c> (<c>docs/design/eng-docs.md</c>, "Steps").</summary>
    private const string ErrorCodesPage = "docs/error-codes.md";

    /// <summary>The directory of <c>docs</c> under <c>artifacts/</c>: the report of lychee. It is deleted before a run and made again before lychee.</summary>
    private const string DocsOutput = "artifacts/docs";

    /// <summary>The report of lychee, run offline, that <c>docs</c> turns into findings with <c>eng/checks links</c>.</summary>
    private const string DocsReport = DocsOutput + "/lychee.json";

    /// <summary>The report of Stryker.NET that <c>mutate</c> reads, under the output directory of the run.</summary>
    private const string MutateReport = "artifacts/mutate/reports/mutation-report.json";

    /// <summary>The directories of <c>mutate</c> under <c>artifacts/</c>, deleted before a run so every report comes from it.</summary>
    private static readonly string[] MutateWritten = ["artifacts/mutate"];

    /// <summary>
    /// The directories of <c>test</c> under <c>artifacts/</c>, deleted before a run so every package, report and executable comes from the
    /// current build.
    /// </summary>
    private static readonly string[] TestWritten = ["artifacts/packages", "artifacts/test-results", "artifacts/aot"];

    /// <summary>The directories of <c>min-sdk</c> under <c>artifacts/</c>, deleted before a run.</summary>
    private static readonly string[] MinSdkWritten = [MinSdk.Output];

    /// <summary>The directories of <c>docs</c> under <c>artifacts/</c>, deleted before a run so the report of lychee comes from it.</summary>
    private static readonly string[] DocsWritten = [DocsOutput];

    /// <summary>Whether the name is that of a command.</summary>
    /// <param name="name">The command name.</param>
    internal static bool IsKnown(string name) => name is "test" or "docs" or "mutate" or "difftest" or "min-sdk";

    /// <summary>
    /// Returns the directories under <c>artifacts/</c> that a command writes, relative to the repository root, or an empty list when
    /// the command is unknown. The command deletes them before its first step.
    /// </summary>
    /// <param name="name">The command name.</param>
    internal static IReadOnlyList<string> Written(string name) => name switch
    {
        "test" => TestWritten,
        "docs" => DocsWritten,
        "mutate" => MutateWritten,
        "min-sdk" => MinSdkWritten,
        _ => [],
    };

    /// <summary>Returns the stages of a command, or <see langword="null"/> when the command is unknown.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="arguments">The arguments after the command name: the configuration and the commit of <c>mutate</c>, the options of <c>difftest</c>.</param>
    /// <exception cref="System.InvalidOperationException">The command reads the solution, and the solution has no test project; or the arguments of <c>mutate</c> are not valid.</exception>
    internal static IReadOnlyList<Stage>? Find(string name, IReadOnlyList<string> arguments) => name switch
    {
        "test" => TestStages(TestProjects.Find(Solution)),
        "docs" => DocsStages(),
        "mutate" => MutateStages(MutateOptions.Parse(arguments)),
        "difftest" => DifftestStages(arguments),
        "min-sdk" => [.. MinSdk.Steps.Select(Stage.Of)],
        _ => null,
    };

    /// <summary>
    /// The counters that the last line of a command shows besides its result, or <see langword="null"/> for a command that has none.
    /// </summary>
    /// <param name="name">The command name.</param>
    internal static string? Counters(string name) => name == "test" ? TestCounters.Summary() : null;

    /// <summary>
    /// <c>test</c> (<c>eng/test</c>), the one full check, in three stages. First the build of every project of the solution for both
    /// target frameworks, once: a failed build stops the command. Then at the same time the lanes of the second stage: all test modules
    /// under JIT with a report each, followed by the checks of <c>eng/checks</c>; and for every target of every project that is published
    /// under Native AOT one lane that publishes it and runs the executable. The publishes do not build the referenced projects again.
    /// Then the comparison of the tests of every published run with the JIT run of the same project and target. No step stops the
    /// others, so one run shows every failure.
    /// </summary>
    internal static List<Stage> TestStages(IReadOnlyList<TestProject> projects) =>
    [
        Stage.Of(new("dotnet", ["build", Solution, "-c", "Release"], Timeouts.Build, StopsOnFailure: true)),
        new Stage([JitLane(projects), .. projects.Where(project => project.PublishAot).SelectMany(AotLanes)]),
        new Stage([.. projects.Where(project => project.PublishAot).SelectMany(CountLanes)]),
    ];

    /// <summary>
    /// <c>docs</c> (<c>eng/docs</c>, <c>docs/design/eng-docs.md</c>, "Steps"), in three stages. First the build of <c>eng/checks</c>, which
    /// every later check needs: a failed build stops the command. Then the lane of steps 1 to 3: the build of the library for both targets
    /// with its XML documentation, the examples and the error codes page; a failed build skips the examples and the page, which need it,
    /// but not the command. Then the lane of steps 4 and 5: lychee offline with its report, and the findings of the report. The exit code
    /// of lychee is not used, because the report is the result; a report that is missing fails the last step.
    /// </summary>
    internal static List<Stage> DocsStages() =>
    [
        Stage.Of(new("dotnet", ["build", "eng/checks", "-c", "Release"], Timeouts.Build, StopsOnFailure: true)),
        new Stage([
            [
                new("dotnet", ["build", "src/Great.Parts.Cel", "-c", "Release"], Timeouts.Build, StopsOnFailure: true),
                new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", "examples"], Timeouts.Examples),
                new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", "error-codes", "--page", ErrorCodesPage], Timeouts.Check),
            ],
        ]),
        new Stage([
            [
                Step.InProcess("docs: create " + DocsOutput, () => MakeDirectory(DocsOutput), stopsOnFailure: true),
                new(
                    "lychee",
                    ["--offline", "--include-fragments", "--format", "json", "--output", DocsReport, "--config", "lychee.toml", "README.md", "docs", "src/**/*.cs"],
                    Timeouts.Lychee,
                    IgnoresExitCode: true),
                new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", "links", "--report", DocsReport], Timeouts.Check),
            ],
        ]),
    ];

    // lychee writes its report to a file, not to a directory that is not there.
    private static string? MakeDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    // The module of every test project and target runs once under JIT, with its report; the report of the two projects that are also
    // published is the reference of their test count. Then the checks.
    private static Step[] JitLane(IReadOnlyList<TestProject> projects) =>
    [
        .. projects.SelectMany(project => project.Targets.Select(target =>
            new Step("dotnet", [$"{project.Directory}/bin/Release/{target}/{project.Name}.dll", "-noColor", "-result-xml", Report("jit", project, target)], Timeouts.Jit))),
        new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", .. Checks], Timeouts.Jit),
    ];

    // The restore of the build already covers the runtime identifier of the publish; the publish reuses it with --no-restore, so no
    // lock file changes.
    private static IEnumerable<Step[]> AotLanes(TestProject project) => project.Targets.Select(target => new Step[]
    {
        new(
            "dotnet",
            [
                "publish", project.Directory, "-f", target, "-r", AotRuntime, "-c", "Release", "--no-restore",
                "-p:InvariantGlobalization=true", "-p:BuildProjectReferences=false", "-o", $"{Published(project, target)}/",
            ],
            Timeouts.Publish,
            StopsOnFailure: true),
        new($"{Published(project, target)}/{project.Name}", ["-noColor", "-result-xml", Report("aot", project, target)], Timeouts.AotRun),
    });

    private static IEnumerable<Step[]> CountLanes(TestProject project) => project.Targets.Select(target => new Step[]
    {
        new(
            "dotnet",
            [
                "run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", "test-count",
                "--jit", Report("jit", project, target), "--aot", Report("aot", project, target), "--where", $"{AotRuntime}, {target}",
            ],
            Timeouts.Check),
    });

    private static string Report(string mode, TestProject project, string target) => $"artifacts/test-results/{mode}/{project.Name}/{target}/report.xml";

    private static string Published(TestProject project, string target) => $"artifacts/aot/{project.Name}/{target}";

    /// <summary>
    /// <c>mutate</c>: the restore of the local tool Stryker.NET, the build of <c>eng/checks</c>, then the run of the tool with the
    /// configuration given as the argument, or with <c>stryker-config.json</c>, and with the changes counted from the commit of
    /// <c>--since</c>, or from the target of the configuration. The report goes to <c>artifacts/mutate/</c>. The last step reads the report
    /// with the same configuration and the same commit and prints the findings; it runs even when the run of the tool failed, so the
    /// surviving mutants are visible. The run of the tool has the time of its preparation first; when it says how many mutants it will
    /// test, the time for the mutants replaces it (<see cref="Timeouts"/>).
    /// </summary>
    private static List<Stage> MutateStages(MutateOptions options) => MutateStages(options, options.ResolveSince());

    /// <summary>The stages of <c>mutate</c> for the options and the full SHA of the commit that the changes are counted from.</summary>
    /// <param name="options">The configuration file and the commit as they were given.</param>
    /// <param name="since">The full SHA of the commit of <c>--since</c>, or <see langword="null"/> when none was given.</param>
    internal static List<Stage> MutateStages(MutateOptions options, string? since)
    {
        string[] sinceOfTool = since is null ? [] : [$"{MutateOptions.SinceOption}:{since}"];
        string[] sinceOfCheck = since is null ? [] : [MutateOptions.SinceOption, since];
        return
        [
            Stage.Of(new("dotnet", ["tool", "restore"], Timeouts.MutatePreparation, StopsOnFailure: true)),
            Stage.Of(new("dotnet", ["build", "eng/checks", "-c", "Release"], Timeouts.Build, StopsOnFailure: true)),
            Stage.Of(new("dotnet", ["stryker", "--config-file", options.Configuration, "--output", "artifacts/mutate", .. sinceOfTool], Timeouts.MutatePreparation, Extend: Timeouts.MutateChecking)),
            Stage.Of(new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", "mutation", "--report", MutateReport, "--config", options.Configuration, .. sinceOfCheck], Timeouts.Check)),
        ];
    }

    /// <summary>
    /// <c>difftest</c>: the build of <c>eng/difftest</c>, then its run with the arguments of the command (docs/design/eng-difftest.md).
    /// A failed build stops the command; after it the command returns the exit code of <c>eng/difftest</c> unchanged: 0, 1 or 2.
    /// </summary>
    private static List<Stage> DifftestStages(IReadOnlyList<string> arguments) =>
    [
        Stage.Of(new("dotnet", ["build", "eng/difftest", "-c", "Release"], Timeouts.Build, StopsOnFailure: true)),
        Stage.Of(new("dotnet", ["run", "--project", "eng/difftest", "-c", "Release", "--no-build", "--", .. arguments], Timeouts.Difftest(arguments), PassesExitCode: true)),
    ];
}

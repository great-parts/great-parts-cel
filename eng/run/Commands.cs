using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>The commands of <c>eng/</c> and their steps.</summary>
internal static class Commands
{
    /// <summary>The solution of the repository; also the marker of the repository root.</summary>
    internal const string Solution = "Great.Parts.Cel.slnx";

    /// <summary>The names of the commands, as the messages of <see cref="Program"/> list them.</summary>
    internal const string Known = "test, test-aot, mutate";

    /// <summary>
    /// <c>eng/test</c>: the build of both target frameworks of every project of the solution, then every test project of the
    /// solution under JIT, then the source and package checks of <c>eng/checks</c>. A failed build stops the command; the tests and
    /// the checks both run even when one of them fails, so a single run shows all failures.
    /// </summary>
    private static readonly Step[] Test =
    [
        new("dotnet", ["build", Solution, "-c", "Release"], StopsOnFailure: true),
        new("dotnet", ["test", "--solution", Solution, "-c", "Release", "--no-build"]),
        new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--no-build", "--", "suppressions", "duplicates", "ratchet", "packages", "--no-build"]),
    ];

    /// <summary>The configuration of <c>mutate</c> when the command gets no argument.</summary>
    private const string DefaultMutateConfiguration = "stryker-config.json";

    /// <summary>The report of Stryker.NET that <c>mutate</c> reads, under the output directory of the run.</summary>
    private const string MutateReport = "artifacts/mutate/reports/mutation-report.json";

    /// <summary>The directories of <c>mutate</c> under <c>artifacts/</c>, deleted before a run so every report comes from it.</summary>
    private static readonly string[] MutateWritten = ["artifacts/mutate"];

    /// <summary>The directories of <c>eng/test</c> under <c>artifacts/</c>, deleted before a run so every package comes from the current build.</summary>
    private static readonly string[] TestWritten = ["artifacts/packages"];

    /// <summary>
    /// The test projects that <c>test-aot</c> runs under JIT and publishes under Native AOT, one place for the list. The projects
    /// are in <c>tests/</c> and named as their directories; <c>Great.Parts.Cel.Conformance</c> joins with its increment.
    /// </summary>
    private static readonly string[] AotProjects = ["Great.Parts.Cel.Tests"];

    /// <summary>The target frameworks of the projects of <see cref="AotProjects"/>.</summary>
    private static readonly string[] AotTargets = ["net10.0", "net11.0"];

    /// <summary>The runtime identifier of the Native AOT publish, also the label of its test count.</summary>
    private const string AotRuntime = "linux-x64";

    /// <summary>The directories of <c>test-aot</c> under <c>artifacts/</c>, deleted before a run so every report and file comes from it.</summary>
    private static readonly string[] AotWritten = ["artifacts/test-results", "artifacts/aot"];

    /// <summary>
    /// <c>test-aot</c>: for every target and every project the build and the JIT run of the test module, the Native AOT publish, the
    /// run of the published executable, and the comparison of the two reports. None of the steps stops the command, so one run shows
    /// every failing pair and step.
    /// </summary>
    private static readonly Step[] TestAot = [.. BuildTestAot()];

    /// <summary>
    /// Returns the directories under <c>artifacts/</c> that a command writes, relative to the repository root, or an empty list when
    /// the command is unknown. The command deletes them before its first step.
    /// </summary>
    /// <param name="name">The command name.</param>
    internal static IReadOnlyList<string> Written(string name) => name switch
    {
        "test" => TestWritten,
        "test-aot" => AotWritten,
        "mutate" => MutateWritten,
        _ => [],
    };

    /// <summary>Returns the steps of a command, or <see langword="null"/> when the command is unknown.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="arguments">The arguments after the command name; only <c>mutate</c> uses them, for its configuration.</param>
    internal static IReadOnlyList<Step>? Find(string name, IReadOnlyList<string> arguments) => name switch
    {
        "test" => Test,
        "test-aot" => TestAot,
        "mutate" => MutateSteps(arguments),
        _ => null,
    };

    /// <summary>
    /// <c>mutate</c>: the restore of the local tool Stryker.NET, then its run with the configuration given as the argument, or with
    /// <c>stryker-config.json</c>. The report goes to <c>artifacts/mutate/</c>. The third step reads the report with the same configuration
    /// and prints the findings; it runs even when the run of the tool failed, so the surviving mutants are visible.
    /// </summary>
    private static Step[] MutateSteps(IReadOnlyList<string> arguments)
    {
        string configuration = arguments.Count > 0 ? arguments[0] : DefaultMutateConfiguration;
        return
        [
            new("dotnet", ["tool", "restore"], StopsOnFailure: true),
            new("dotnet", ["stryker", "--config-file", configuration, "--output", "artifacts/mutate"]),
            new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--", "mutation", "--report", MutateReport, "--config", configuration]),
        ];
    }

    private static IEnumerable<Step> BuildTestAot()
    {
        foreach (string target in AotTargets)
        {
            foreach (string project in AotProjects)
            {
                string directory = $"tests/{project}";
                string jitReport = $"artifacts/test-results/jit/{project}/{target}/report.xml";
                string aotReport = $"artifacts/test-results/aot/{project}/{target}/report.xml";
                string published = $"artifacts/aot/{project}/{target}";

                // The restore of the build has no runtime identifier; the publish reuses it with --no-restore, so no lock file changes.
                yield return new("dotnet", ["build", directory, "-f", target, "-c", "Release"]);
                yield return new("dotnet", [$"{directory}/bin/Release/{target}/{project}.dll", "-result-xml", jitReport]);
                yield return new("dotnet", ["publish", directory, "-f", target, "-r", AotRuntime, "-c", "Release", "--no-restore", "-p:InvariantGlobalization=true", "-o", $"{published}/"]);
                yield return new($"{published}/{project}", ["-result-xml", aotReport]);
                yield return new("dotnet", ["run", "--project", "eng/checks", "-c", "Release", "--", "test-count", "--jit", jitReport, "--aot", aotReport, "--where", $"{AotRuntime}, {target}"]);
            }
        }
    }
}

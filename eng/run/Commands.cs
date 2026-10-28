using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>The commands of <c>eng/</c> and their steps.</summary>
internal static class Commands
{
    /// <summary>The solution of the repository; also the marker of the repository root.</summary>
    internal const string Solution = "Great.Parts.Cel.slnx";

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

    /// <summary>The directories of <c>eng/test</c> under <c>artifacts/</c>, deleted before a run so every package comes from the current build.</summary>
    private static readonly string[] TestWritten = ["artifacts/packages"];

    /// <summary>
    /// Returns the directories under <c>artifacts/</c> that a command writes, relative to the repository root, or an empty list when
    /// the command is unknown. The command deletes them before its first step.
    /// </summary>
    /// <param name="name">The command name.</param>
    internal static IReadOnlyList<string> Written(string name) => name switch
    {
        "test" => TestWritten,
        _ => [],
    };

    /// <summary>Returns the steps of a command, or <see langword="null"/> when the command is unknown.</summary>
    /// <param name="name">The command name.</param>
    internal static IReadOnlyList<Step>? Find(string name) => name switch
    {
        "test" => Test,
        _ => null,
    };
}

using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>The commands of <c>eng/</c> and their steps.</summary>
internal static class Commands
{
    /// <summary>The solution of the repository; also the marker of the repository root.</summary>
    internal const string Solution = "Great.Parts.Cel.slnx";

    /// <summary>
    /// <c>eng/test</c>: the build of both target frameworks of every project of the solution, then every test project of the
    /// solution under JIT. A failed build stops the command.
    /// </summary>
    private static readonly Step[] Test =
    [
        new("dotnet", ["build", Solution, "-c", "Release"], StopsOnFailure: true),
        new("dotnet", ["test", "--solution", Solution, "-c", "Release", "--no-build"]),
    ];

    /// <summary>Returns the steps of a command, or <see langword="null"/> when the command is unknown.</summary>
    /// <param name="name">The command name.</param>
    internal static IReadOnlyList<Step>? Find(string name) => name switch
    {
        "test" => Test,
        _ => null,
    };
}

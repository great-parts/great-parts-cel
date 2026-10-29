using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>One step of a command: a program with its arguments, run as a child process.</summary>
/// <param name="FileName">The program to start.</param>
/// <param name="Arguments">The arguments of the program.</param>
/// <param name="StopsOnFailure">Whether a failure of the step stops the command, because the next steps need its result.</param>
/// <param name="PassesExitCode">Whether the command returns the exit code of the step unchanged instead of 0 or 1.</param>
internal sealed record Step(string FileName, IReadOnlyList<string> Arguments, bool StopsOnFailure = false, bool PassesExitCode = false)
{
    /// <summary>The command line of the step as it is written in the header line and in the failure summary.</summary>
    internal string CommandLine => string.Join(' ', [FileName, .. Arguments]);
}

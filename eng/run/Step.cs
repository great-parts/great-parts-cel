using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// One step of a command: a program with its arguments, run as a child process, or a piece of C# that runs in this process.
/// </summary>
/// <param name="FileName">The program to start, or the name of the step when it runs in this process.</param>
/// <param name="Arguments">The arguments of the program.</param>
/// <param name="Timeout">How long the program may run before it is killed with its process tree; <see langword="null"/> for a step
/// that runs in this process, which limits its own child processes (<see cref="Timeouts"/>).</param>
/// <param name="StopsOnFailure">Whether a failure of the step skips the next steps of its lane, because they need its result.</param>
/// <param name="PassesExitCode">Whether the command returns the exit code of the step unchanged instead of 0 or 1.</param>
/// <param name="Action">The C# of a step that runs in this process: it returns <see langword="null"/> on success, otherwise the reason.</param>
/// <param name="Extend">Gives a new limit, counted from the start of the step, when a line of the output of the program calls for it; it
/// returns <see langword="null"/> for the other lines. Only the first new limit counts.</param>
/// <param name="IgnoresExitCode">Whether the step counts as run when the program ended with any exit code, because its result is a file that
/// a later step reads: lychee. A program that did not start or ran out of time still fails the step.</param>
internal sealed record Step(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan? Timeout,
    bool StopsOnFailure = false,
    bool PassesExitCode = false,
    Func<string?>? Action = null,
    Func<string, TimeSpan, TimeSpan?>? Extend = null,
    bool IgnoresExitCode = false)
{
    /// <summary>The command line of the step as it is written in the header line and in the failure summary.</summary>
    internal string CommandLine => string.Join(' ', [FileName, .. Arguments]);

    /// <summary>A step that runs C# in this process.</summary>
    /// <param name="name">The name of the step for the header line and the failure summary.</param>
    /// <param name="action">The code of the step: <see langword="null"/> on success, otherwise the reason of the failure. A child process of
    /// the step that runs out of time fails it with <see cref="Timeouts.Reason"/>.</param>
    /// <param name="stopsOnFailure">Whether a failure of the step skips the next steps of its lane.</param>
    internal static Step InProcess(string name, Func<string?> action, bool stopsOnFailure = false) =>
        new(name, [], null, stopsOnFailure, Action: action);
}

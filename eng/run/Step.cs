using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>One step of a command: a program with its arguments, run as a child process.</summary>
/// <param name="FileName">The program to start.</param>
/// <param name="Arguments">The arguments of the program.</param>
internal sealed record Step(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>The command line of the step as it is written in the header line and in the failure summary.</summary>
    internal string CommandLine => string.Join(' ', [FileName, .. Arguments]);
}

using System;
using System.Diagnostics;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The result of one step in the session of the container.</summary>
/// <param name="ExitCode">The exit code of the command, or <see langword="null"/> when the step was stopped because it did not end within its time.</param>
/// <param name="Output">What the command wrote to standard output.</param>
/// <param name="Error">What the command wrote to standard error.</param>
internal sealed record QuickStartStepResult(int? ExitCode, string Output, string Error);

/// <summary>
/// The clean container of the quick start with its one session: the commands run in order, with a common current directory. A test gives
/// its own container, so no test needs Docker. Disposing the container removes it.
/// </summary>
internal interface IQuickStartContainer : IDisposable
{
    /// <summary>Runs a command of the session and waits for it to end.</summary>
    /// <param name="command">The command line, as it is written in the README.</param>
    /// <param name="limit">The time the command may run; after that the session is stopped.</param>
    QuickStartStepResult Run(string command, TimeSpan limit);

    /// <summary>Writes a file into the current directory of the session.</summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="text">The content of the file.</param>
    /// <param name="limit">The time the write may take.</param>
    QuickStartStepResult Write(string fileName, string text, TimeSpan limit);
}

/// <summary>The clock of the quick start: a time that only grows.</summary>
internal interface IQuickStartClock
{
    /// <summary>The time since a fixed moment, which only the differences of two readings refer to.</summary>
    TimeSpan Now { get; }
}

/// <summary>The stopwatch of the machine.</summary>
internal sealed class StopwatchClock : IQuickStartClock
{
    /// <inheritdoc/>
    public TimeSpan Now => Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp());
}

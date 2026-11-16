using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Runs the steps of the quick start in the one session of a container and judges them (<c>docs/design/eng-release.md</c>, "eng/checks
/// quick-start"). A step that fails is <c>ENG304</c> and ends the session; when every step passed and the end of the output of the last
/// command is the expected output, the time from the start of the first step to the end of the last one is judged: <c>ENG305</c>.
/// </summary>
internal static class QuickStartSession
{
    /// <summary>The time that the steps may take without <c>ENG305</c>: 300 seconds.</summary>
    internal static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(300);

    /// <summary>The time after which the session is stopped: 10 minutes.</summary>
    internal static readonly TimeSpan SessionLimit = TimeSpan.FromMinutes(10);

    /// <summary>Runs the steps of the block.</summary>
    /// <param name="block">The block of the README.</param>
    /// <param name="container">The clean container.</param>
    /// <param name="clock">The clock of the time of the steps.</param>
    /// <returns>No finding when every step passed in the time; otherwise the one finding of the first failure, or <c>ENG305</c>.</returns>
    internal static List<Finding> Run(QuickStartBlock block, IQuickStartContainer container, IQuickStartClock clock)
    {
        TimeSpan start = clock.Now;
        int last = block.Steps.ToList().FindLastIndex(step => !step.IsProgram);
        QuickStartStepResult? output = null;
        for (int index = 0; index < block.Steps.Count; index++)
        {
            QuickStartStep step = block.Steps[index];
            TimeSpan left = SessionLimit - (clock.Now - start);
            QuickStartStepResult result = step.IsProgram
                ? container.Write(QuickStartBlock.ProgramFile, step.Text, left)
                : container.Run(step.Text, left);
            if (Failure(block, step, result) is { } failure)
            {
                return [failure];
            }

            output = index == last ? result : output;
        }

        TimeSpan elapsed = clock.Now - start;
        if (OutputFailure(block, block.Steps[last], output!) is { } different)
        {
            return [different];
        }

        return elapsed > TimeLimit
            ? [new Finding(block.Path, block.BeginLine, 1, FindingSeverity.Error, EngCodes.Eng305, $"the quick start took {Seconds(elapsed)} s, the limit is {Seconds(TimeLimit)} s")]
            : [];
    }

    private static Finding? Failure(QuickStartBlock block, QuickStartStep step, QuickStartStepResult result)
    {
        string name = step.IsProgram ? $"write {QuickStartBlock.ProgramFile}" : step.Text;
        string message;
        if (result.ExitCode is not { } exitCode)
        {
            message = $"step '{name}' did not end: the session was stopped at {SessionLimit.TotalMinutes.ToString(CultureInfo.InvariantCulture)} minutes";
        }
        else if (exitCode != 0)
        {
            message = $"step '{name}' exited with code {exitCode.ToString(CultureInfo.InvariantCulture)}: {Reason(result)}";
        }
        else
        {
            return null;
        }

        return new Finding(block.Path, step.Line, 1, FindingSeverity.Error, EngCodes.Eng304, message);
    }

    // One line of the output of a failed command: the first line with "error" (any case) in the error output and then in the standard
    // output (dotnet prints some errors to the standard output); without it the last line of the error output, or of the standard output
    // when the error output is empty.
    private static string Reason(QuickStartStepResult result)
    {
        string[] error = NonEmptyLines(result.Error);
        string[] output = NonEmptyLines(result.Output);
        return error.Concat(output).FirstOrDefault(line => line.Contains("error", StringComparison.OrdinalIgnoreCase))
            ?? (error.Length > 0 ? error[^1] : output.Length > 0 ? output[^1] : "no message on standard error");
    }

    private static string[] NonEmptyLines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // The end of the output of the last command, as many lines as the example expects, is compared with the expected lines; what the
    // build printed before them is not. The finding is at the first expected line. The block has an expected output: it cannot be read without.
    private static Finding? OutputFailure(QuickStartBlock block, QuickStartStep command, QuickStartStepResult result)
    {
        IReadOnlyList<ExpectedLine> expected = block.Expected;
        string printed = result.Output.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = printed.EndsWith('\n') ? printed[..^1].Split('\n') : printed.Split('\n');
        string tail = string.Join('\n', lines.Skip(Math.Max(0, lines.Length - expected.Count)));
        Example example = new(block.Path, expected[0].Position.Line, [], Example.DefaultTarget, null, expected);
        if (ExpectedOutput.Compare(example, tail) is not { } difference)
        {
            return null;
        }

        string message = $"the end of the output of step '{command.Text}' is not the expected output: {difference.Message}";
        return new Finding(block.Path, expected[0].Position.Line, expected[0].Position.Column, FindingSeverity.Error, EngCodes.Eng304, message);
    }

    private static string Seconds(TimeSpan time) => Math.Round(time.TotalSeconds).ToString(CultureInfo.InvariantCulture);
}

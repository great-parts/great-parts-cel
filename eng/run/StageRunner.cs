using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>What the steps of a stage reported.</summary>
/// <param name="Failed">The failed steps with their reasons, in the order of the lanes.</param>
/// <param name="PassedOn">The exit code of the step that passes its exit code on, when it ran to its end.</param>
internal sealed record StageResult(IReadOnlyList<string> Failed, int? PassedOn);

/// <summary>Runs the stages of a command: the lanes of a stage at the same time, the steps of a lane one after another.</summary>
internal static class StageRunner
{
    /// <summary>Runs the lanes of a stage and returns when every lane has ended.</summary>
    /// <param name="stage">The stage.</param>
    internal static StageResult Run(Stage stage)
    {
        if (stage.Lanes.Count == 1)
        {
            return RunLane(stage.Lanes[0], Console.Out, live: true);
        }

        // The output of a lane is kept and written when the lane ends: lines of different processes are never mixed.
        object gate = new();
        Task<StageResult>[] tasks = [.. stage.Lanes.Select(lane => Task.Run(() =>
        {
            using StringWriter buffer = new(CultureInfo.InvariantCulture);
            StageResult result = RunLane(lane, buffer, live: false);
            lock (gate)
            {
                Console.Out.Write(buffer.ToString());
            }

            return result;
        }))];
        Task.WaitAll(tasks);
        return new StageResult(
            [.. tasks.SelectMany(task => task.Result.Failed)],
            tasks.Select(task => task.Result.PassedOn).FirstOrDefault(code => code is not null));
    }

    // Runs the steps of a lane up to the first failed step that stops the lane.
    private static StageResult RunLane(IReadOnlyList<Step> steps, TextWriter output, bool live)
    {
        List<string> failed = [];
        int? passedOn = null;
        foreach (Step step in steps)
        {
            output.WriteLine($"== {step.CommandLine}");
            ProcessResult result = Execute(step, live ? null : output);
            if (step.PassesExitCode && result.ExitCode is not null)
            {
                passedOn = result.ExitCode;
            }

            if (result.Succeeded)
            {
                continue;
            }

            failed.Add(Describe(step, result));
            if (step.StopsOnFailure)
            {
                break;
            }
        }

        return new StageResult(failed, passedOn);
    }

    // A step that ran out of time is named with its limit; any other failed step with its reason, when it has one.
    private static string Describe(Step step, ProcessResult result)
    {
        if (Timeouts.IsReason(result.Reason))
        {
            return $"{result.Reason} in step {step.CommandLine}";
        }

        return result.Reason is null ? step.CommandLine : $"{step.CommandLine} ({result.Reason})";
    }

    // A step in this process writes to the console itself; it belongs to a stage of one lane. The output of a program that gives a new
    // limit by a line of it passes through this process, so the lines can be read.
    private static ProcessResult Execute(Step step, TextWriter? sink)
    {
        if (step.Action is not null)
        {
            string? reason = step.Action();
            return new ProcessResult(reason is null ? 0 : 1, reason, string.Empty);
        }

        return Processes.Run(step.FileName, step.Arguments, sink: sink ?? (step.Extend is null ? null : Console.Out), timeout: step.Timeout, extend: step.Extend);
    }
}

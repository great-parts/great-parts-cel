using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>What the steps of a stage reported.</summary>
/// <param name="Failed">The failed steps with their reasons, in the order of the lanes.</param>
/// <param name="PassedOn">The exit code of the step that passes its exit code on, when it ran.</param>
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
            bool succeeded = Execute(step, live ? null : output, out int? exitCode, out string? reason);
            if (step.PassesExitCode && exitCode is not null)
            {
                passedOn = exitCode;
            }

            if (succeeded)
            {
                continue;
            }

            failed.Add(reason is null ? step.CommandLine : $"{step.CommandLine} ({reason})");
            if (step.StopsOnFailure)
            {
                break;
            }
        }

        return new StageResult(failed, passedOn);
    }

    // A step in this process writes to the console itself; it belongs to a stage of one lane.
    private static bool Execute(Step step, TextWriter? sink, out int? exitCode, out string? reason)
    {
        if (step.Action is not null)
        {
            reason = step.Action();
            exitCode = reason is null ? 0 : 1;
            return reason is null;
        }

        ProcessResult result = Processes.Run(step.FileName, step.Arguments, sink: sink);
        exitCode = result.ExitCode;
        reason = result.Reason;
        return result.Succeeded;
    }
}

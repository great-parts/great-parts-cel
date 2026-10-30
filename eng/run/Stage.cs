using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// A part of a command: lanes that run at the same time, each lane being steps that run one after another. A stage ends when
/// every lane has ended.
/// </summary>
/// <param name="Lanes">The lanes of the stage. A stage of one lane runs with the output of the steps passed through live; the output
/// of every lane of a stage of several lanes is written as a whole when the lane ends, so it is not mixed with another.</param>
/// <param name="StopsOnFailure">Whether the command stops after this stage when any of its steps failed.</param>
internal sealed record Stage(IReadOnlyList<IReadOnlyList<Step>> Lanes, bool StopsOnFailure = false)
{
    /// <summary>A stage of one step. The command stops after it when the step fails and <see cref="Step.StopsOnFailure"/> is set.</summary>
    /// <param name="step">The step.</param>
    internal static Stage Of(Step step) => new([[step]], step.StopsOnFailure);
}

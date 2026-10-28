using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Compares the numbers of the repository with the baseline <c>eng/ratchet.json</c>, so a number grows only by a change of the
/// baseline in the same change set and a baseline that is too high is lowered
/// (<c>docs/design/eng-checks-source.md</c>, "ratchet"). It uses the scans of <c>suppressions</c> and <c>duplicates</c> of the same
/// run, and makes them when they are not there. The file is read by <see cref="RatchetFile"/>, the numbers are compared by
/// <see cref="RatchetComparison"/>.
/// </summary>
internal sealed class RatchetCommand : ICheckCommand
{
    /// <inheritdoc/>
    public string Name => "ratchet";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        RatchetFile baseline = RatchetFile.Of(context);
        return baseline.Problems.Count > 0 ? baseline.Problems : RatchetComparison.Compare(context, baseline);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG908</c>: <c>ci.yml</c> and <c>difftest-main.yml</c> are green on the commit of the tag. For each of the two workflows a run of the
/// event <c>push</c> with the <c>head_sha</c> of the commit and the conclusion <c>success</c> has to exist.
/// </summary>
internal static class WorkflowsGreenGate
{
    private static readonly string[] Workflows = ["ci.yml", "difftest-main.yml"];

    /// <summary>Checks the runs of the commit.</summary>
    /// <param name="tag">The release tag.</param>
    /// <param name="commit">The full hash of the checked commit.</param>
    /// <param name="repository">The repository of the runs, <c>owner/name</c>.</param>
    /// <param name="api">The API of GitHub.</param>
    internal static IEnumerable<Finding> Check(ReleaseTag tag, string commit, string repository, IGitHubApi api) => Workflows
        .Where(workflow => !LongRunGate.Items(api.Get($"repos/{repository}/actions/workflows/{workflow}/runs?event=push&head_sha={commit}&per_page=100"), "workflow_runs")
            .Any(run => string.Equals(LongRunGate.Text(run, "head_sha"), commit, StringComparison.OrdinalIgnoreCase) && LongRunGate.Text(run, "conclusion") == "success"))
        .Select(workflow => Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng908, $"no successful run of {workflow} on commit {commit[..8]}"))
        .ToList();
}

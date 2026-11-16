using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG902</c>: the commit of the tag is reachable from <c>main</c>. The workflow has fetched <c>origin/main</c>; the check is
/// <c>git merge-base --is-ancestor</c> against it.
/// </summary>
internal static class MainAncestryGate
{
    private const string MainRef = "refs/remotes/origin/main";

    /// <summary>Checks the commit.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="tag">The release tag.</param>
    /// <param name="commit">The full hash of the checked commit.</param>
    internal static IEnumerable<Finding> Check(string root, ReleaseTag tag, string commit)
    {
        if (!Git.Run(root, "rev-parse", "--verify", "--quiet", MainRef).Succeeded)
        {
            throw new CheckInputException("origin/main does not exist in the repository: fetch it before the gates");
        }

        GitResult result = Git.Run(root, "merge-base", "--is-ancestor", commit, MainRef);
        return result.ExitCode switch
        {
            0 => [],
            1 => [Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng902, $"tag {tag.Name} points to {commit[..8]}, which is not on main")],
            _ => throw new CheckInputException($"git merge-base could not compare {commit[..8]} with origin/main: {result.Error.Trim()}"),
        };
    }
}

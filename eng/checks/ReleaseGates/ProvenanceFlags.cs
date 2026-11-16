using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The flags of <c>gh attestation verify</c> that make the check exact. They are one for the workflow and for the command of the README:
/// <c>--repo &lt;repository&gt; --signer-workflow &lt;repository&gt;/.github/workflows/release.yml --source-ref refs/tags/&lt;tag&gt; --deny-self-hosted-runners</c>.
/// </summary>
internal static class ProvenanceFlags
{
    /// <summary>The flags as arguments of the process.</summary>
    /// <param name="repository">The repository of the origin, <c>owner/name</c>.</param>
    /// <param name="tag">The name of the release tag with <c>v</c>.</param>
    internal static IReadOnlyList<string> Arguments(string repository, string tag) =>
    [
        "--repo",
        repository,
        "--signer-workflow",
        repository + "/.github/workflows/release.yml",
        "--source-ref",
        "refs/tags/" + tag,
        "--deny-self-hosted-runners",
    ];

    /// <summary>The flags as the line that the README shows.</summary>
    /// <param name="repository">The repository of the origin, <c>owner/name</c>.</param>
    /// <param name="tag">The name of the release tag with <c>v</c>.</param>
    internal static string Line(string repository, string tag) => string.Join(' ', Arguments(repository, tag));
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG904</c>: a successful long run against cel-go exists on the commit of the tag or on its ancestor. It is a run of <c>difftest-nightly.yml</c>
/// with the event <c>workflow_dispatch</c> whose job <c>Differential run</c> succeeded and took at least 7 200 seconds: the input
/// <c>budget</c> of a manual run is not in the answer of the API, so the length of the job is the evidence. A run on an ancestor counts when
/// no path that the differential run depends on (<see cref="CodePaths"/>) changed between the run and the tag; the closest such run is taken.
/// </summary>
internal static class LongRunGate
{
    private const string Workflow = "difftest-nightly.yml";
    private const string JobName = "Differential run";
    private static readonly TimeSpan MinimumLength = TimeSpan.FromSeconds(7200);

    /// <summary>
    /// The paths of the repository that the differential run depends on: a path ending with <c>/</c> is a directory, any other is a file or a
    /// prefix of file names. The library and the paths that build it, the program, the launcher, the oracle and their images and the
    /// <c>compose.yaml</c>, the proto files and the sources of the runner that the program takes, the registry of known signatures, the files
    /// that set the packages and the SDK, and the workflows of the differential run. The gate itself (<c>eng/checks</c>, <c>eng/tests</c>) and
    /// <c>release.yml</c> are not there: they do not change what the run does.
    /// </summary>
    internal static readonly string[] CodePaths =
    [
        "src/",
        "eng/difftest/",
        "eng/findings/",
        "eng/run/",
        "eng/oracle/",
        "eng/docker/",
        "eng/compose.yaml",
        "tests/cel-spec/",
        "tests/Great.Parts.Cel.Conformance/Runner/",
        "tests/Great.Parts.Cel.Conformance/Regression/",
        "Directory.Build.props",
        "Directory.Packages.props",
        "global.json",
        "NuGet.config",
        ".github/workflows/difftest-",
    ];

    /// <summary>Checks the runs of the commit and of its ancestors.</summary>
    /// <param name="tag">The release tag.</param>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="commit">The full hash of the checked commit.</param>
    /// <param name="repository">The repository of the runs, <c>owner/name</c>.</param>
    /// <param name="api">The API of GitHub.</param>
    internal static IEnumerable<Finding> Check(ReleaseTag tag, string root, string commit, string repository, IGitHubApi api)
    {
        string runs = api.Get($"repos/{repository}/actions/workflows/{Workflow}/runs?event=workflow_dispatch&status=success&per_page=100");
        List<(string Sha, string Id, int Distance)> candidates = [];
        foreach (JsonElement run in Items(runs, "workflow_runs"))
        {
            string sha = Text(run, "head_sha");
            if (Text(run, "event") == "workflow_dispatch" && Text(run, "conclusion") == "success" && sha.Length == 40
                && Distance(root, sha, commit) is int distance)
            {
                candidates.Add((sha, Id(run), distance));
            }
        }

        (string Sha, List<string> Paths)? closest = null;
        foreach ((string sha, string id, int _) in candidates.OrderBy(candidate => candidate.Distance))
        {
            if (!HasLongJob(api.Get($"repos/{repository}/actions/runs/{id}/jobs?per_page=100")))
            {
                continue;
            }

            List<string> changed = ChangedCodePaths(root, sha, commit);
            if (changed.Count == 0)
            {
                return [];
            }

            closest ??= (sha, changed);
        }

        string message = closest is null
            ? $"no successful long run of {Workflow} (at least 2 h) on commit {commit[..8]}"
            : $"no successful long run of {Workflow} (at least 2 h) on commit {commit[..8]} or on an ancestor with no change of the code: the closest run is on {closest.Value.Sha[..8]}, and these paths changed since it: {string.Join(", ", closest.Value.Paths)}";
        return [Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng904, message)];
    }

    // The number of commits from the run to the checked commit, 0 for the commit itself; no value for a commit that is not its ancestor
    // or is not in the repository (a run of a branch that was not fetched).
    private static int? Distance(string root, string sha, string commit)
    {
        if (Git.Run(root, "merge-base", "--is-ancestor", sha, commit).ExitCode != 0)
        {
            return null;
        }

        GitResult count = Git.Run(root, "rev-list", "--count", sha + ".." + commit);
        return count.Succeeded && int.TryParse(count.Output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int distance)
            ? distance
            : throw new CheckInputException($"git rev-list could not count the commits from {sha[..8]} to {commit[..8]}: {count.Error.Trim()}");
    }

    private static List<string> ChangedCodePaths(string root, string sha, string commit)
    {
        GitResult diff = Git.Run(root, "diff", "--name-only", "--no-renames", "-z", sha, commit);
        return diff.Succeeded
            ? [.. diff.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => CodePaths.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))]
            : throw new CheckInputException($"git diff could not compare {sha[..8]} with {commit[..8]}: {diff.Error.Trim()}");
    }

    private static bool HasLongJob(string jobs)
    {
        foreach (JsonElement job in Items(jobs, "jobs"))
        {
            if (Text(job, "name") == JobName
                && Text(job, "conclusion") == "success"
                && DateTimeOffset.TryParse(Text(job, "started_at"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset started)
                && DateTimeOffset.TryParse(Text(job, "completed_at"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset completed)
                && completed - started >= MinimumLength)
            {
                return true;
            }
        }

        return false;
    }

    // The elements of the array of an answer; an answer that is not JSON or has no such array is an input that cannot be read.
    internal static List<JsonElement> Items(string answer, string name)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(answer);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(name, out JsonElement array)
                || array.ValueKind != JsonValueKind.Array)
            {
                throw new CheckInputException($"the answer of the API of GitHub has no array {name}");
            }

            List<JsonElement> items = [];
            foreach (JsonElement item in array.EnumerateArray())
            {
                items.Add(item.Clone());
            }

            return items;
        }
        catch (JsonException exception)
        {
            throw new CheckInputException($"the answer of the API of GitHub is not JSON: {exception.Message}", exception);
        }
    }

    internal static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Id(JsonElement run) =>
        run.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.Number
            ? id.GetInt64().ToString(CultureInfo.InvariantCulture)
            : throw new CheckInputException("a run in the answer of the API of GitHub has no id");
}

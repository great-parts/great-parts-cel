using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG904</c>: a successful long run against cel-go exists on the commit of the tag. It is a run of <c>difftest-nightly.yml</c> with the
/// event <c>workflow_dispatch</c> and the <c>head_sha</c> of the commit whose job <c>Differential run</c> succeeded and took at least 7 200
/// seconds: the input <c>budget</c> of a manual run is not in the answer of the API, so the length of the job is the evidence.
/// </summary>
internal static class LongRunGate
{
    private const string Workflow = "difftest-nightly.yml";
    private const string JobName = "Differential run";
    private static readonly TimeSpan MinimumLength = TimeSpan.FromSeconds(7200);

    /// <summary>Checks the runs of the commit.</summary>
    /// <param name="tag">The release tag.</param>
    /// <param name="commit">The full hash of the checked commit.</param>
    /// <param name="repository">The repository of the runs, <c>owner/name</c>.</param>
    /// <param name="api">The API of GitHub.</param>
    internal static IEnumerable<Finding> Check(ReleaseTag tag, string commit, string repository, IGitHubApi api)
    {
        string runs = api.Get($"repos/{repository}/actions/workflows/{Workflow}/runs?event=workflow_dispatch&head_sha={commit}&per_page=100");
        foreach (JsonElement run in Items(runs, "workflow_runs"))
        {
            if (Text(run, "event") == "workflow_dispatch"
                && string.Equals(Text(run, "head_sha"), commit, StringComparison.OrdinalIgnoreCase)
                && Text(run, "conclusion") == "success"
                && HasLongJob(api.Get($"repos/{repository}/actions/runs/{Id(run)}/jobs?per_page=100")))
            {
                return [];
            }
        }

        return [Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng904, $"no successful long run of {Workflow} (at least 2 h) on commit {commit[..8]}")];
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
    private static List<JsonElement> Items(string answer, string name)
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

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Id(JsonElement run) =>
        run.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.Number
            ? id.GetInt64().ToString(CultureInfo.InvariantCulture)
            : throw new CheckInputException("a run in the answer of the API of GitHub has no id");
}

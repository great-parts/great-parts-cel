using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One failed link of the lychee report: the file, the address, what lychee says about it and the place it gives.</summary>
/// <param name="File">The key of the input file in the report, as lychee was given it, such as <c>README.md</c>.</param>
/// <param name="Url">The address as lychee resolved it: <c>https://…</c>, <c>file:///…</c> for a relative link, or <c>error:</c> for a link that cannot be resolved.</param>
/// <param name="Text">The text of the status, such as <c>Rejected status code: 404 Not Found</c>.</param>
/// <param name="Details">The short reason of the status, such as <c>Cannot find fragment</c>; the text when the report gives none.</param>
/// <param name="StatusCode">The HTTP status code, or <see langword="null"/> when the status has none.</param>
/// <param name="Span">
/// The line and the column of the link in the file, both from 1 as lychee counts them — the column counts Unicode code points —
/// or <see langword="null"/> when the report gives none.
/// </param>
/// <param name="TimedOut">Whether the link is in the list of timeouts of the report.</param>
internal sealed record ReportedLink(string File, string Url, string Text, string Details, int? StatusCode, Position? Span, bool TimedOut);

/// <summary>
/// The JSON report of lychee 0.24.2 (<c>--format json</c>): the failed links of <c>error_map</c> and <c>timeout_map</c>, each with its
/// input file, address, status and span (<c>docs/design/eng-docs.md</c>, "links"). The other maps — successes, excluded
/// addresses, redirects and suggestions — are not read.
/// </summary>
internal sealed class LycheeReport
{
    private LycheeReport(IReadOnlyList<ReportedLink> failures) => Failures = failures;

    /// <summary>The failed links, in the order of the report: the errors first, then the timeouts.</summary>
    internal IReadOnlyList<ReportedLink> Failures { get; }

    /// <summary>Reads a report from a file.</summary>
    /// <param name="path">The full path of the report.</param>
    /// <exception cref="CheckInputException">The file cannot be read or is not a lychee report.</exception>
    internal static LycheeReport Read(string path) => JsonReport.Read(path, "lychee report", Parse);

    private static LycheeReport Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error_map", out JsonElement errors) || errors.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("it has no \"error_map\" object");
        }

        List<ReportedLink> failures = [];
        ReadMap(errors, timedOut: false, failures);
        if (root.TryGetProperty("timeout_map", out JsonElement timeouts) && timeouts.ValueKind == JsonValueKind.Object)
        {
            ReadMap(timeouts, timedOut: true, failures);
        }

        return new LycheeReport(failures);
    }

    private static void ReadMap(JsonElement map, bool timedOut, List<ReportedLink> failures)
    {
        foreach (JsonProperty file in map.EnumerateObject())
        {
            foreach (JsonElement link in file.Value.EnumerateArray())
            {
                failures.Add(ParseLink(file.Name, link, timedOut));
            }
        }
    }

    private static ReportedLink ParseLink(string file, JsonElement link, bool timedOut)
    {
        string? text = null;
        string? details = null;
        int? code = null;
        if (link.TryGetProperty("status", out JsonElement status) && status.ValueKind == JsonValueKind.Object)
        {
            text = Text(status, "text");
            details = Text(status, "details");
            if (status.TryGetProperty("code", out JsonElement number) && number.ValueKind == JsonValueKind.Number)
            {
                code = number.GetInt32();
            }
        }

        text ??= string.Empty;
        return new ReportedLink(file, Text(link, "url") ?? string.Empty, text, details ?? text, code, ParseSpan(link), timedOut);
    }

    // A span without a line or a column, or with 0, is no span: the place is then searched in the file.
    private static Position? ParseSpan(JsonElement link)
    {
        if (!link.TryGetProperty("span", out JsonElement span) || span.ValueKind != JsonValueKind.Object
            || !span.TryGetProperty("line", out JsonElement line) || line.ValueKind != JsonValueKind.Number
            || !span.TryGetProperty("column", out JsonElement column) || column.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        int lineNumber = line.GetInt32();
        int columnNumber = column.GetInt32();
        return lineNumber >= 1 && columnNumber >= 1 ? new Position(lineNumber, columnNumber) : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

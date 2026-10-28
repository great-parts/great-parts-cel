using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Compares the actual numbers of the repository with the baseline <c>eng/ratchet.json</c>: <c>ENG602</c> and <c>ENG603</c>
/// when a number grows, <c>ENG605</c> when it falls, <c>ENG604</c> when the thresholds of <c>CodeMetricsConfig.txt</c> differ
/// (<c>docs/design/eng-checks-source.md</c>, "ratchet"). The baseline of a file with problems is not compared.
/// </summary>
internal static class RatchetComparison
{
    /// <summary>The file of the thresholds of the code metrics, in the root of the repository.</summary>
    internal const string CodeMetricsPath = "CodeMetricsConfig.txt";

    /// <summary>Compares the three kinds of numbers.</summary>
    /// <param name="context">The context of the run: the root and the scans that other commands of the run share.</param>
    /// <param name="baseline">The baseline file.</param>
    internal static IEnumerable<Finding> Compare(CheckContext context, RatchetFile baseline)
    {
        AddedLines added = AddedLines.Of(context);
        SuppressionScan suppressions = SuppressionScan.Of(context);
        DuplicateScan duplicates = DuplicateScan.Of(context);
        return [.. Suppressions(baseline, suppressions, added), .. Duplicates(baseline, duplicates, added), .. CodeMetrics(context.Root, baseline)];
    }

    // ---- suppressions ----

    private static IEnumerable<Finding> Suppressions(RatchetFile baseline, SuppressionScan scan, AddedLines added)
    {
        Dictionary<string, RatchetEntry> entries = baseline.Suppressions.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        foreach (string key in entries.Keys.Union(scan.Counts.Keys, StringComparer.Ordinal))
        {
            int actual = scan.Counts.GetValueOrDefault(key);
            bool written = entries.TryGetValue(key, out RatchetEntry entry);
            int allowed = written ? entry.Value : 0;
            Position at = written ? entry.Position : baseline.SuppressionsAt;
            string head = $"suppressions of {key}: {actual}, baseline {allowed}";
            if (actual > allowed)
            {
                List<SuppressionPlace> places = [.. scan.Places.Where(place => place.Key == key)];
                List<SuppressionPlace> fresh = [.. places.Where(place => added.IsAdded(place.Path, place.Position.Line))];
                yield return Error(EngCodes.Eng602, at, $"{head}; new: {Names((fresh.Count > 0 ? fresh : places).Select(Describe))}");
            }
            else if (actual < allowed)
            {
                yield return Error(EngCodes.Eng605, at, $"{head}; " + (actual == 0 ? "remove the key" : $"lower the baseline to {actual}"));
            }
        }
    }

    private static string Describe(SuppressionPlace place) => $"{place.Path}({place.Position.Line},{place.Position.Column})";

    // ---- duplicates ----

    private static IEnumerable<Finding> Duplicates(RatchetFile baseline, DuplicateScan scan, AddedLines added)
    {
        int actual = scan.Pairs.Count;
        string head = $"duplicate pairs: {actual}, baseline {baseline.Duplicates}";
        if (actual > baseline.Duplicates)
        {
            List<DuplicatePair> fresh = [.. scan.Pairs.Where(pair => added.IsAdded(pair.FirstPath, pair.First.Line) || added.IsAdded(pair.SecondPath, pair.Second.Line))];
            IEnumerable<string> names = (fresh.Count > 0 ? fresh : scan.Pairs).Select(pair => $"{Name(pair.FirstPath, pair.First)} with {Name(pair.SecondPath, pair.Second)}");
            yield return Error(EngCodes.Eng603, baseline.DuplicatesAt, $"{head}; new: {Names(names)}");
        }
        else if (actual < baseline.Duplicates)
        {
            yield return Error(EngCodes.Eng605, baseline.DuplicatesAt, $"{head}; lower the baseline to {actual}");
        }
    }

    private static string Name(string path, Position position) => $"{path}({position.Line},{position.Column})";

    private static string Names(IEnumerable<string> places) => string.Join(", ", places);

    // ---- codeMetrics ----

    private static IEnumerable<Finding> CodeMetrics(string root, RatchetFile baseline)
    {
        Dictionary<string, int> actual = ReadThresholds(root);
        Dictionary<string, int> written = [];
        foreach (RatchetEntry entry in baseline.CodeMetrics)
        {
            written[entry.Key] = entry.Value;
        }

        foreach (string key in actual.Keys.Union(written.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            bool inFile = actual.TryGetValue(key, out int fromFile);
            bool inBaseline = written.TryGetValue(key, out int fromBaseline);
            if (inFile && inBaseline && fromFile == fromBaseline)
            {
                continue;
            }

            string message = (inFile, inBaseline) switch
            {
                (true, true) => $"{CodeMetricsPath} sets {key} to {fromFile}, baseline {fromBaseline}",
                (true, false) => $"{CodeMetricsPath} sets {key} to {fromFile}, the baseline has no {key}",
                _ => $"baseline sets {key} to {fromBaseline}, {CodeMetricsPath} has no {key}",
            };
            yield return Error(EngCodes.Eng604, baseline.CodeMetricsAt, $"code metrics threshold differs: {message}");
        }
    }

    // A line is "<rule>: <threshold>" or "<rule>(<symbol kind>): <threshold>"; the key is the part before the colon. The compiler
    // checks the format of the lines, so a line of another form is not read here.
    private static Dictionary<string, int> ReadThresholds(string root)
    {
        Dictionary<string, int> thresholds = new(StringComparer.Ordinal);
        string path = Path.Combine(root, CodeMetricsPath);
        if (!File.Exists(path))
        {
            throw new CheckInputException($"{CodeMetricsPath} does not exist");
        }

        foreach (string line in File.ReadAllLines(path))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && int.TryParse(line[(colon + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int threshold))
            {
                thresholds[line[..colon].Trim()] = threshold;
            }
        }

        return thresholds;
    }

    private static Finding Error(string code, Position at, string message) =>
        new(RatchetFile.Path, at.Line, at.Column, FindingSeverity.Error, code, message);
}

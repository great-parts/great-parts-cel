using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A place with a valid reason, counted by the ratchet under a key.</summary>
/// <param name="Key">The key: a rule ID, or the name of the form without a rule ID such as <c>nullable</c> or <c>generated</c>.</param>
/// <param name="Path">The path of the file, relative to the root, with <c>/</c>.</param>
/// <param name="Position">The line of the form and the column of its first character.</param>
internal readonly record struct SuppressionPlace(string Key, string Path, Position Position);

/// <summary>
/// The result of the scan of the forms: the findings of the places without a valid reason, and the places with one, which the
/// ratchet counts. The scan is made once per run and kept in <see cref="CheckContext"/>, so <c>suppressions</c> and
/// <c>ratchet</c> of one run share it.
/// </summary>
internal sealed class SuppressionScan
{
    private const string ScanName = "suppressions";

    private SuppressionScan(IReadOnlyList<Finding> findings, IReadOnlyList<SuppressionPlace> places)
    {
        Findings = findings;
        Places = places;
        Counts = places
            .GroupBy(place => place.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    /// <summary><c>ENG101</c> and <c>ENG102</c>, one per place without a valid reason.</summary>
    internal IReadOnlyList<Finding> Findings { get; }

    /// <summary>Every counted place, one per key of a form, in the order of the scanners and of the files.</summary>
    internal IReadOnlyList<SuppressionPlace> Places { get; }

    /// <summary>The number of counted places by key.</summary>
    internal IReadOnlyDictionary<string, int> Counts { get; }

    /// <summary>Returns the scan of the run, making it on the first call.</summary>
    /// <param name="context">The context of the run.</param>
    internal static SuppressionScan Of(CheckContext context) =>
        context.Scan(ScanName, () => Run(context, SuppressionsCommand.Scanners));

    /// <summary>Reads the forms with the scanners and judges every reason.</summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="scanners">The scanners of the forms.</param>
    internal static SuppressionScan Run(CheckContext context, IEnumerable<ISuppressionScanner> scanners)
    {
        List<Finding> findings = [];
        List<SuppressionPlace> places = [];
        foreach (SuppressionForm form in scanners.SelectMany(scanner => scanner.Scan(context)))
        {
            switch (SuppressionReason.Judge(form.Reason, form.RuleIds))
            {
                case ReasonState.Valid:
                    places.AddRange(form.Keys.Distinct(StringComparer.Ordinal).Select(key => new SuppressionPlace(key, form.Path, form.Position)));
                    break;
                case ReasonState.Missing:
                    findings.Add(Error(form, EngCodes.Eng101, $"{form.Description} has no {form.ReasonPlace}"));
                    break;
                default:
                    findings.Add(Error(form, EngCodes.Eng102, $"{form.Description} has a {form.ReasonPlace} that only repeats the rule ID"));
                    break;
            }
        }

        return new SuppressionScan(findings, places);
    }

    private static Finding Error(SuppressionForm form, string code, string message) =>
        new(form.Path, form.Position.Line, form.Position.Column, FindingSeverity.Error, code, message);
}

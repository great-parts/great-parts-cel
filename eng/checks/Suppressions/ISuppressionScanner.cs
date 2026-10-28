using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One place that turns a check or a rule off or lowers its level, with the reason that the scanner read next to it.</summary>
/// <param name="Path">The path of the file, relative to the root, with <c>/</c>.</param>
/// <param name="Position">The line of the form and the column of its first character.</param>
/// <param name="Description">
/// What the finding names, quoted where it is source text, such as <c>'#pragma warning disable CA1502'</c>; the message reads
/// <c>&lt;description&gt; has no &lt;reason place&gt;</c>.
/// </param>
/// <param name="ReasonPlace">Where the reason is, as the end of the message: <c>reason comment</c>, <c>reason in the Justification argument</c>.</param>
/// <param name="Keys">The keys of the ratchet that the place counts under; empty when the form has none.</param>
/// <param name="RuleIds">The rule IDs of the form; they are removed from the reason before it is judged.</param>
/// <param name="Reason">The text next to the form, as written; empty when there is none.</param>
internal sealed record SuppressionForm(
    string Path,
    Position Position,
    string Description,
    string ReasonPlace,
    IReadOnlyList<string> Keys,
    IReadOnlyList<string> RuleIds,
    string Reason);

/// <summary>Reads the forms of one kind of file. <see cref="SuppressionScan"/> judges the reasons, the scanner only reads.</summary>
internal interface ISuppressionScanner
{
    /// <summary>Returns every form of the files that the scanner reads.</summary>
    /// <param name="context">The root, the file set and the C# sources of the run.</param>
    IEnumerable<SuppressionForm> Scan(CheckContext context);
}

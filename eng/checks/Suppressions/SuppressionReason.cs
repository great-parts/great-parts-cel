using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>How good the reason next to a form is.</summary>
internal enum ReasonState
{
    /// <summary>The reason has text of its own.</summary>
    Valid,

    /// <summary>There is no reason: no text, or only punctuation and white space.</summary>
    Missing,

    /// <summary>The reason is only the rule ID.</summary>
    OnlyRuleId,
}

/// <summary>
/// Judges a reason: it is the text that remains after the rule IDs, punctuation and white space are removed. Nothing was
/// written, or only punctuation — <see cref="ReasonState.Missing"/>; something was written and nothing remains after the IDs
/// — only the code — <see cref="ReasonState.OnlyRuleId"/>.
/// </summary>
internal static class SuppressionReason
{
    private const string CompilerPrefix = "CS";

    /// <summary>Judges the reason of a form.</summary>
    /// <param name="reason">The text next to the form.</param>
    /// <param name="ruleIds">The rule IDs of the form.</param>
    internal static ReasonState Judge(string reason, IReadOnlyList<string> ruleIds)
    {
        if (Remaining(reason, []).Length == 0)
        {
            return ReasonState.Missing;
        }

        return Remaining(reason, ruleIds).Length == 0 ? ReasonState.OnlyRuleId : ReasonState.Valid;
    }

    /// <summary>The compiler warning ID of a bare number: <c>1591</c> is <c>CS1591</c>. Other IDs are returned as they are.</summary>
    /// <param name="id">The ID as written in the form.</param>
    internal static string NormalizeId(string id) =>
        id.Length > 0 && id.All(char.IsAsciiDigit) ? CompilerPrefix + id : id;

    // The longest ID goes first, so that a longer ID is not cut by a shorter one. A compiler ID also goes without CS: the
    // comment of 'disable 1591' may say CS1591 or 1591.
    private static string Remaining(string reason, IReadOnlyList<string> ruleIds)
    {
        string text = reason;
        foreach (string id in ruleIds.SelectMany(Spellings).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(id => id.Length))
        {
            text = text.Replace(id, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        StringBuilder rest = new(text.Length);
        foreach (char character in text.Where(character => !char.IsWhiteSpace(character) && !char.IsPunctuation(character) && !char.IsSymbol(character)))
        {
            rest.Append(character);
        }

        return rest.ToString();
    }

    private static IEnumerable<string> Spellings(string id)
    {
        yield return id;
        if (id.Length > CompilerPrefix.Length && id.StartsWith(CompilerPrefix, StringComparison.Ordinal) && id[CompilerPrefix.Length..].All(char.IsAsciiDigit))
        {
            yield return id[CompilerPrefix.Length..];
        }
    }
}

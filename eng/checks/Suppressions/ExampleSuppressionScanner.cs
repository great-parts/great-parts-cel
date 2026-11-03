using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the examples that are not run (<c>docs/design/eng-checks-source.md</c>, "suppressions"): a block of <c>README.md</c> or
/// <c>docs/**/*.md</c> with the option <c>skip: &lt;reason&gt;</c>, and a <c>&lt;code&gt;</c> element of the XML documentation with the attribute
/// <c>skip</c>. The reason is in the same line; the key is <c>example</c>. The codes of the checks that the example escapes,
/// <c>ENG301</c> and <c>ENG302</c>, are the rule IDs of the form: a reason that only repeats them is <c>ENG102</c>.
/// </summary>
internal sealed class ExampleSuppressionScanner : ISuppressionScanner
{
    private const string Key = "example";
    private static readonly string[] RuleIds = [EngCodes.Eng301, EngCodes.Eng302];

    /// <inheritdoc/>
    public IEnumerable<SuppressionForm> Scan(CheckContext context)
    {
        foreach (Example example in ExampleSources.Of(context))
        {
            if (example.Skip is { } skip)
            {
                yield return new SuppressionForm(example.Path, skip.Position, skip.Description, skip.ReasonPlace, [Key], RuleIds, skip.Reason);
            }
        }
    }
}

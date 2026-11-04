using System;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Conformance.CelSpec;
using Great.Parts.Cel.Conformance.Regression;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Conformance.Environment;

// The suites do not depend on the culture of the process (docs/design/tests.md, "Culture of the process"): under tr-TR the
// cel-spec suite and the regression corpus run with their skip lists, as their suite tests do, and fail on the same findings.
// Under invariant globalization, where tr-TR cannot be created, they run in the invariant culture.
[Trait("Requirement", "R-5")]
public sealed class CultureFitnessTests
{
    private static readonly bool InvariantGlobalization =
        AppContext.TryGetSwitch("System.Globalization.Invariant", out bool on) && on;

    [Theory]
    [InlineData("tr-TR")]
    public static void CelSpecSuite_PassesOrIsSkippedInTheCulture(string culture) =>
        FailOnErrors(InCulture(culture, () => CelSpecSuite.Run(new LibraryEvaluator(), RunCell.Current)));

    [Theory]
    [InlineData("tr-TR")]
    public static void RegressionSuite_PassesOrIsSkippedInTheCulture(string culture) =>
        FailOnErrors(InCulture(culture, () => RegressionSuite.Run(new LibraryEvaluator(), RunCell.Current)));

    // The findings with the severity error, without the summary line of the suite tests.
    private static void FailOnErrors(SuiteResult result)
    {
        string[] errors = [.. result.Findings.Where(finding => finding.Severity == FindingSeverity.Error).Select(finding => finding.ToString())];
        Assert.True(errors.Length == 0, string.Join('\n', errors));
    }

    // Runs the suite with the culture of this thread set, and restores it; the culture must exist without invariant globalization.
    private static SuiteResult InCulture(string name, Func<SuiteResult> run)
    {
        CultureInfo culture = InvariantGlobalization ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
        (CultureInfo current, CultureInfo currentUI) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, culture);
            return run();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (current, currentUI);
        }
    }
}

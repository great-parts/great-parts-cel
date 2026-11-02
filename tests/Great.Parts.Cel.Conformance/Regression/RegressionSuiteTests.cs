using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Regression;

// The regression corpus on the library (docs/design/conformance.md, "Core and suites").
public sealed class RegressionSuiteTests
{
    [Fact]
    public void AllCases_PassOrAreSkipped() =>
        SuiteAssert.PassOrAreSkipped(RegressionSuite.Run(new LibraryEvaluator(), RunCell.Current), RegressionSuite.Source);
}

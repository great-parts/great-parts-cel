using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Environment;

// Results do not depend on the culture of the process (docs/design/tests.md, "Culture of the process"): under tr-TR — decimal
// comma, dotted and dotless i — the expectations are the constants of the invariant culture. A test sets the culture of its own
// thread and restores it. Under invariant globalization, where tr-TR cannot be created, the test runs in the invariant culture
// with the same expectations.
[Trait("Requirement", "R-5")]
public sealed class CultureFitnessTests
{
    private static readonly bool InvariantGlobalization =
        AppContext.TryGetSwitch("System.Globalization.Invariant", out bool on) && on;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Theory]
    [InlineData("tr-TR", "string(1.5)", "1.5")]
    [InlineData("tr-TR", "string(-1)", "-1")]
    [InlineData("tr-TR", "string(1e21)", "1e+21")]
    [InlineData("tr-TR", "string(-0.25)", "-0.25")]
    [InlineData("tr-TR", "string(123456.0)", "123456")]
    [InlineData("tr-TR", "string(18446744073709551615u)", "18446744073709551615")]
    [InlineData("tr-TR", "string(true)", "true")]
    public static void NumberToText_IsInvariant(string culture, string expression, string expected) =>
        InCulture(culture, () => Assert.Equal(expected, Environment.Evaluate<string>(expression, new CelBindings()).GetValueOrThrow()));

    [Theory]
    [InlineData("tr-TR", "double('1.5')", 1.5)]
    [InlineData("tr-TR", "double('-2.5e3')", -2500.0)]
    [InlineData("tr-TR", "double('1_000.5')", 1000.5)]
    [InlineData("tr-TR", "double(int('-42'))", -42.0)]
    [InlineData("tr-TR", "double(uint('7'))", 7.0)]
    public static void TextToNumber_IsInvariant(string culture, string expression, double expected) =>
        InCulture(culture, () => Assert.Equal(expected, Environment.Evaluate<double>(expression, new CelBindings()).GetValueOrThrow()));

    [Theory]
    [InlineData("tr-TR", "'a' < 'B'", false)]
    [InlineData("tr-TR", "'I' < 'i'", true)]
    [InlineData("tr-TR", "'i' < 'ı'", true)]
    [InlineData("tr-TR", "'İ' > 'Z'", true)]
    [InlineData("tr-TR", "'TITLE' == 'title'", false)]
    [InlineData("tr-TR", "'file'.startsWith('FI')", false)]
    [InlineData("tr-TR", "'kIlIç'.contains('ılı')", false)]
    [InlineData("tr-TR", "size('İıi') == 3", true)]
    [InlineData("tr-TR", "bool('TRUE')", true)]
    public static void Strings_AreComparedByCodePoints(string culture, string expression, bool expected) =>
        InCulture(culture, () => Assert.Equal(expected, Environment.Evaluate<bool>(expression, new CelBindings()).GetValueOrThrow()));

    // The folding of case in matches is that of the tables of the library (the simple folding of Unicode), whatever the culture: the
    // dotted capital I and the dotless small i do not fold to i under tr-TR, and the Kelvin sign and the long s do fold.
    [Theory]
    [InlineData("tr-TR", "'I'.matches('(?i)i')", true)]
    [InlineData("tr-TR", "'i'.matches('(?i)I')", true)]
    [InlineData("tr-TR", "'ı'.matches('(?i)i')", false)]
    [InlineData("tr-TR", "'İ'.matches('(?i)i')", false)]
    [InlineData("tr-TR", "'i'.matches('(?i)İ')", false)]
    [InlineData("tr-TR", "'TITLE'.matches('(?i)^title$')", true)]
    [InlineData("tr-TR", "'\u212A'.matches('(?i)^k$')", true)]
    [InlineData("tr-TR", "'\u017F'.matches('(?i)^[a-z]$')", true)]
    [InlineData("tr-TR", "'ı'.matches('(?i)^[a-z]$')", false)]
    [InlineData("tr-TR", "matches('1.5', '^[0-9]+[.][0-9]+$')", true)]
    public static void Matches_IgnoresTheCulture(string culture, string expression, bool expected) =>
        InCulture(culture, () => Assert.Equal(expected, Environment.Evaluate<bool>(expression, new CelBindings()).GetValueOrThrow()));

    [Theory]
    [InlineData("tr-TR", "[1, 2][1234]", null, CelErrorCode.IndexOutOfRange, "index 1234 out of range [0, 2)")]
    [InlineData("tr-TR", "int(1.5e19)", null, CelErrorCode.ConversionRange, "1.5e+19 is out of the range of int")]
    [InlineData("tr-TR", "[1, 2, 3].map(v, v * 1000) == []", 3UL, CelErrorCode.CostLimitExceeded, "evaluation cost limit of 3 exceeded")]
    public static void ErrorMessage_IsInvariant(string culture, string expression, ulong? limit, CelErrorCode code, string expected) =>
        InCulture(culture, () =>
        {
            CelEvalResult<CelValue> result = Environment.Evaluate<CelValue>(
                expression, new CelBindings(), null, new CelEvalOptions { CostLimit = limit });
            CelError error = Assert.Single(result.Errors);
            Assert.Equal((code, expected), (error.Code, error.Message));
        });

    [Theory]
    [InlineData("tr-TR", "1.5", "expression of type 'double' is not assignable to the expected type 'int'")]
    public static void CompileErrorMessage_IsInvariant(string culture, string expression, string expected) =>
        InCulture(culture, () =>
        {
            CelError error = Assert.Single(Environment.Compile<long>(expression).Errors);
            Assert.Equal((CelErrorCode.ExpectedTypeMismatch, expected), (error.Code, error.Message));
        });

    // Runs the check with the culture and the user interface culture of this thread set to the culture, or to the invariant
    // culture under invariant globalization, and restores them. Without invariant globalization the culture must exist.
    private static void InCulture(string name, Action check)
    {
        CultureInfo culture = InvariantGlobalization ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
        CultureInfo current = CultureInfo.CurrentCulture;
        CultureInfo currentUI = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            check();
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
            CultureInfo.CurrentUICulture = currentUI;
        }
    }
}

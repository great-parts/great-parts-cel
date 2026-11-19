using System.Linq;
using CsCheck;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Properties.Syntax;

// QuoteString and the parser: the literal that QuoteString writes for any string without a lone surrogate parses back into that
// string, in an expression that is only the literal. The strings have the controls, the printable ASCII, the two-byte and three-byte
// code points and the pairs of surrogates.
public sealed class QuoteStringTests
{
    private const ulong Seed = 20261119;
    private const int Cases = 800;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow();

    private static readonly Gen<int> CodePoint = Gen.OneOf(
        Gen.Int[0, 0x1F],
        Gen.Int[0x20, 0x7F],
        Gen.Int[0x80, 0x7FF],
        Gen.Int[0x800, 0xD7FF],
        Gen.Int[0xE000, 0xFFFD],
        Gen.Int[0x10000, 0x10FFFD]);

    private static readonly Gen<string> Strings = CodePoint.List[0, 24].Select(points => string.Concat(points.Select(char.ConvertFromUtf32)));

    [Fact]
    [Trait("Requirement", "F-14")]
    public void QuotedString_ParsesBackIntoTheSameString() =>
        PropertyLoop.Check(nameof(QuotedString_ParsesBackIntoTheSameString), Strings, Seed, Cases, Violation, Shown);

    private static string? Violation(string value)
    {
        string literal = CelSyntax.QuoteString(value);
        CelCompileResult<string> compiled = Environment.Compile<string>(literal);
        if (!compiled.IsSuccess)
        {
            CelError error = compiled.Errors[0];
            return $"compile error {error.Code}: {error.Message}";
        }

        CelEvalResult<string> result = compiled.Program.Evaluate(new CelBindings());
        if (!result.IsSuccess)
        {
            return $"evaluation error {result.Errors[0].Code}";
        }

        return string.Equals(result.Value, value, System.StringComparison.Ordinal) ? null : "parsed into another string";
    }

    // The UTF-16 units in hexadecimal, so that a control or a surrogate shows.
    private static string Shown(string value) =>
        string.Join(' ', value.Select(unit => ((int)unit).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
}

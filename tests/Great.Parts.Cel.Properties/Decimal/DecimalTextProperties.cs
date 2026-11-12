#if NET11_0_OR_GREATER
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Decimals;

// The text of decimal against the reference: string(x) is the to-scientific-string of the value; decimal(string(x)) is x in
// sign, coefficient and exponent; decimal(s) for a text of 1 to 40 digits, with and without a point and an exponent, rounds to 34
// digits half to even and is InvalidArgument for a text outside the grammar and ConversionRange for an exponent out of the range
// after the rounding; and decimal(x) and decimal(dyn(x)) are x itself.
public sealed class DecimalTextProperties
{
    private const ulong FormatSeed = 20261211;
    private const ulong ParseSeed = 20261212;
    private const ulong IdentitySeed = 20261213;
    private const int Cases = 3000;

    private static readonly CelProgram<string> Text = DecimalHarness.Program<string>("string(a)");
    private static readonly CelProgram<Decimal128> ReadBack = DecimalHarness.Program<Decimal128>("decimal(string(a))");
    private static readonly CelProgram<Decimal128> FromText = DecimalHarness.Program<Decimal128>("decimal(s)");
    private static readonly CelProgram<Decimal128> Same = DecimalHarness.Program<Decimal128>("decimal(a)");
    private static readonly CelProgram<Decimal128> SameThroughDyn = DecimalHarness.Program<Decimal128>("decimal(dyn(a))");

    [Fact]
    [Trait("Requirement", "F-5")]
    public void TextOfAValue_IsTheScientificStringAndReadsBack() =>
        PropertyLoop.Check(nameof(TextOfAValue_IsTheScientificStringAndReadsBack), DecimalGenerators.Values, FormatSeed, Cases, FormatViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void TextOfTheBoundaries_IsTheScientificStringAndReadsBack() =>
        DecimalHarness.CheckAll(nameof(TextOfTheBoundaries_IsTheScientificStringAndReadsBack), DecimalGenerators.Edges, FormatViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfAString_EqualsTheReference() =>
        PropertyLoop.Check(nameof(DecimalOfAString_EqualsTheReference), DecimalGenerators.Texts, ParseSeed, Cases, ParseViolation, text => "'" + text + "'");

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfADecimal_IsTheValueItself() =>
        PropertyLoop.Check(nameof(DecimalOfADecimal_IsTheValueItself), DecimalGenerators.Values, IdentitySeed, Cases, IdentityViolation, value => value.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void DecimalOfTheBoundaries_IsTheValueItself() =>
        DecimalHarness.CheckAll(nameof(DecimalOfTheBoundaries_IsTheValueItself), DecimalGenerators.Edges, IdentityViolation, value => value.ToString());

    private static string? FormatViolation(RefDecimal value)
    {
        CelBindings bindings = DecimalHarness.Bind("a", value);
        string text = ReferenceDecimalText.Format(value);
        return DecimalHarness.Differs("string(a) for the text " + text, text, DecimalHarness.Observe(Text.Evaluate(bindings), static actual => actual))
            ?? DecimalHarness.Differs("decimal(string(a))", value.ToString(), DecimalHarness.Observe(ReadBack.Evaluate(bindings), DecimalHarness.Parts));
    }

    private static string? ParseViolation(string text)
    {
        ParsedDecimal parsed = ReferenceDecimalText.Parse(text);
        string expected = parsed.Outcome switch
        {
            ParseOutcome.Parsed => parsed.Value.ToString(),
            ParseOutcome.NotANumber => DecimalHarness.ExpectedError(CelErrorCode.InvalidArgument),
            _ => DecimalHarness.ExpectedError(CelErrorCode.ConversionRange),
        };
        CelBindings bindings = new CelBindings().Add("s", text);
        return DecimalHarness.Differs("decimal(s)", expected, DecimalHarness.Observe(FromText.Evaluate(bindings), DecimalHarness.Parts));
    }

    private static string? IdentityViolation(RefDecimal value)
    {
        CelBindings bindings = DecimalHarness.Bind("a", value);
        string expected = value.ToString();
        return DecimalHarness.Differs("decimal(a)", expected, DecimalHarness.Observe(Same.Evaluate(bindings), DecimalHarness.Parts))
            ?? DecimalHarness.Differs("decimal(dyn(a))", expected, DecimalHarness.Observe(SameThroughDyn.Evaluate(bindings), DecimalHarness.Parts))
            ?? RoundTripViolation(value);
    }

    // The value of the library that holds the decimal gives its parts back through TryGetDecimal.
    private static string? RoundTripViolation(RefDecimal value)
    {
        CelValue held = DecimalHarness.Value(value);
        if (held.Kind != CelValueKind.Decimal || !held.TryGetDecimal(out Decimal128 back))
        {
            return "CelValue.FromDecimal of " + value.ToString() + " is not a decimal value";
        }

        return DecimalHarness.Differs("TryGetDecimal", value.ToString(), DecimalHarness.Parts(back));
    }
}
#endif

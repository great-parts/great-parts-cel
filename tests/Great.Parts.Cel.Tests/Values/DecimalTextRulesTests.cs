#if NET11_0_OR_GREATER
using System;
using System.Globalization;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The text of a decimal: the grammar of a literal and of a string, the rounding to 34 digits by the last dropped digit and by the
// parity, the range of the exponent, the to-scientific-string, and double to a decimal and back.
public sealed class DecimalTextRulesTests
{
    private const string Digits34 = "1234567890123456789012345678901234";

    private const string Nines34 = "9999999999999999999999999999999999";

    private static readonly UInt128 Coefficient34 = UInt128.Parse(Digits34, CultureInfo.InvariantCulture);

    [Fact]
    public static void Grammar_TakesTheNumbersOfItsForms_AndKeepsTheCohortAsWritten()
    {
        (string Text, bool Negative, ulong Coefficient, int Exponent)[] cases =
        [
            ("1.50", false, 150, -2),
            ("1.5", false, 15, -1),
            (".5", false, 5, -1),
            ("5.", false, 5, 0),
            ("-0", true, 0, 0),
            ("-0.0", true, 0, -1),
            ("+1", false, 1, 0),
            ("1e2", false, 1, 2),
            ("1E+2", false, 1, 2),
            ("1e002", false, 1, 2),
            ("100", false, 100, 0),
            ("0.000", false, 0, -3),
            ("000", false, 0, 0),
            ("00012", false, 12, 0),
            ("-.5e-3", true, 5, -4),
            ("0e0", false, 0, 0),
            ("12.3400e1", false, 123400, -3),
        ];

        foreach ((string text, bool negative, ulong coefficient, int exponent) in cases)
        {
            Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(negative, coefficient, exponent)), Parse(text, exact: false));
            Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(negative, coefficient, exponent)), Parse(text, exact: true));
        }
    }

    [Fact]
    public static void Grammar_RefusesWhatIsNotOfItsForms_AlsoDigitsOfOtherScripts()
    {
        string[] refused =
        [
            "", "+", "-", ".", "-.", "e2", ".e2", "1e", "1e+", "1e-", "1.2.3", " 1", "1 ", "1\n", "1_0", "1,5", "0x10", "Infinity", "NaN",
            "inf", "--1", "+-1", "1e2.5", "1e2e3", "1m", "1d", "1e 2", "１２", "١٢", "1٢",
        ];

        foreach (string text in refused)
        {
            Assert.Equal(DecimalParseResult.NotANumber, Parse(text, exact: false).Result);
            Assert.Equal(DecimalParseResult.NotANumber, Parse(text, exact: true).Result);
        }
    }

    // A tie goes to the even digit, and the digit that goes decides otherwise; the exponent grows by the digits that went.
    [Fact]
    public static void MoreThan34Digits_AreRoundedHalfToEven_ByTheDroppedDigitsAndTheParity()
    {
        (string Text, string Coefficient, int Exponent)[] cases =
        [
            (Digits34 + "4", Digits34, 1),
            (Digits34 + "5", Digits34, 1),
            (Digits34 + "6", "1234567890123456789012345678901235", 1),
            ("1234567890123456789012345678901235" + "5", "1234567890123456789012345678901236", 1),
            ("1234567890123456789012345678901235" + "4", "1234567890123456789012345678901235", 1),
            (Digits34 + "50", Digits34, 2),
            (Digits34 + "51", "1234567890123456789012345678901235", 2),
            (Digits34 + "5" + new string('0', 20) + "1", "1234567890123456789012345678901235", 22),
            (Digits34 + "5" + new string('0', 21), Digits34, 22),
            (Digits34 + "49" + new string('9', 10), Digits34, 12),
            (Nines34 + "4", Nines34, 1),
            ("000" + Digits34 + "5", Digits34, 1),
            (Digits34 + ".5", Digits34, 0),
            ("1234567890123456789012345678901235.5", "1234567890123456789012345678901236", 0),
            (Digits34 + ".00", Digits34, 0),
            ("0." + Digits34 + "5", Digits34, -34),
        ];

        foreach ((string text, string coefficient, int exponent) in cases)
        {
            Assert.Equal(
                (DecimalParseResult.Parsed, new DecimalParts(false, UInt128.Parse(coefficient, CultureInfo.InvariantCulture), exponent)),
                Parse(text, exact: false));
            Assert.Equal(DecimalParseResult.TooManyDigits, Parse(text, exact: true).Result);
        }
    }

    // A carry that makes 10^34 gives 10^33 and one more exponent.
    [Fact]
    public static void RoundingUp_ThroughTheLast34Nines_CarriesIntoTheExponent()
    {
        UInt128 power = DecimalParts.Powers[DecimalParts.MaxDigits - 1];

        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(false, power, 2)), Parse(new string('9', 35), exact: false));
        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(true, power, 2)), Parse("-" + Nines34 + "5", exact: false));
        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(false, power, 6)), Parse("1" + new string('0', 39), exact: false));
        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(false, power, 5)), Parse("1" + new string('0', 33) + "e5", exact: false));
    }

    [Fact]
    public static void ExactMode_AcceptsTheCohortsOf34DigitsAndRefusesMoreAsLongAsTheyAreWritten()
    {
        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(false, Coefficient34, 0)), Parse(Digits34, exact: true));
        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(false, UInt128.One, 39)), Parse("1e39", exact: true));
        Assert.Equal(DecimalParseResult.TooManyDigits, Parse("1" + new string('0', 39), exact: true).Result);
        Assert.Equal(DecimalParseResult.TooManyDigits, Parse(Digits34 + "0", exact: true).Result);
        Assert.Equal(DecimalParseResult.TooManyDigits, Parse(Digits34 + ".0", exact: true).Result);
        Assert.Equal((DecimalParseResult.Parsed, new DecimalParts(false, Coefficient34, 0)), Parse("0000" + Digits34, exact: true));
    }

    // The exponent after the rounding must be from -6176 to 6111, a zero too, and there is no clamp.
    [Fact]
    public static void ExponentOutsideTheRange_IsRefused_AfterTheRoundingAndAlsoForAZero()
    {
        (string Text, bool Parsed)[] cases =
        [
            ("1e-6176", true),
            ("1e-6177", false),
            ("1e6111", true),
            ("1e6112", false),
            ("0e6111", true),
            ("0e6112", false),
            ("0e-6176", true),
            ("0e-6177", false),
            ("1.0e6111", true),
            ("10e6111", true),
            ("10e6112", false),
            (Nines34 + "e6111", true),
            (Nines34 + "e6112", false),
            ("1e" + new string('9', 40), false),
            ("1e-" + new string('9', 40), false),
            ("0." + new string('0', 7_000) + "1", false),
            ("1" + new string('0', 7_000), false),
            (Digits34 + "5e6111", false),
            ("1" + new string('0', 34) + "e6110", true),
            ("1" + new string('0', 35) + "e-6177", true),
        ];

        foreach ((string text, bool parsed) in cases)
        {
            Assert.Equal(parsed ? DecimalParseResult.Parsed : DecimalParseResult.ExponentOutOfRange, Parse(text, exact: false).Result);
        }
    }

    [Fact]
    public static void TheGrammar_IsCheckedBeforeTheRange_AndTheDigitsBeforeTheExponent()
    {
        Assert.Equal(DecimalParseResult.NotANumber, Parse("1e99999999999e", exact: false).Result);
        Assert.Equal(DecimalParseResult.NotANumber, Parse("1e", exact: true).Result);
        Assert.Equal(DecimalParseResult.ExponentOutOfRange, Parse("1e99999999999", exact: false).Result);
        Assert.Equal(DecimalParseResult.TooManyDigits, Parse(Digits34 + "5e9999", exact: true).Result);
        Assert.Equal(DecimalParseResult.ExponentOutOfRange, Parse(Digits34 + "5e9999", exact: false).Result);
    }

    [Fact]
    public static void Format_WritesTheToScientificString()
    {
        (bool Negative, ulong Coefficient, int Exponent, string Text)[] cases =
        [
            (false, 150, -2, "1.50"),
            (false, 15, -1, "1.5"),
            (false, 1000, 0, "1000"),
            (false, 1, 3, "1E+3"),
            (false, 1, 2, "1E+2"),
            (false, 5, -6, "0.000005"),
            (false, 5, -7, "5E-7"),
            (false, 12, -7, "0.0000012"),
            (false, 12, -8, "1.2E-7"),
            (true, 0, 0, "-0"),
            (false, 0, 0, "0"),
            (false, 0, -1, "0.0"),
            (false, 0, 3, "0E+3"),
            (false, 0, -8, "0E-8"),
            (false, 0, -6, "0.000000"),
            (false, 0, -7, "0E-7"),
            (true, 25, -2, "-0.25"),
            (false, 120, 1, "1.20E+3"),
            (false, 1, -6176, "1E-6176"),
            (true, 1, 6111, "-1E+6111"),
        ];

        foreach ((bool negative, ulong coefficient, int exponent, string text) in cases)
        {
            Assert.Equal(text, Format(new DecimalParts(negative, coefficient, exponent)));
        }
    }

    [Fact]
    public static void Format_OfThe34DigitsAtTheEdges_IsPlainUpToTheLastPlaceAndScientificAfter()
    {
        UInt128 max = DecimalParts.MaxCoefficient;

        string mantissa = "9." + new string('9', 33);

        Assert.Equal(mantissa + "E+6144", Format(new DecimalParts(false, max, DecimalParts.MaxExponent)));
        Assert.Equal("-" + Nines34, Format(new DecimalParts(true, max, 0)));
        Assert.Equal(mantissa, Format(new DecimalParts(false, max, -33)));
        Assert.Equal("0." + Nines34, Format(new DecimalParts(false, max, -34)));
        Assert.Equal("0.0" + Nines34, Format(new DecimalParts(false, max, -35)));
        Assert.Equal(mantissa + "E-6143", Format(new DecimalParts(false, max, DecimalParts.MinExponent)));
        Assert.Equal(Digits34, Format(new DecimalParts(false, Coefficient34, 0)));
    }

    // The longest text has 42 characters, and nothing is written beyond them.
    [Fact]
    public static void Format_NeverWritesMoreThan42Characters()
    {
        UInt128 max = DecimalParts.MaxCoefficient;
        DecimalParts[] longest =
        [
            new(true, max, DecimalParts.MaxExponent),
            new(true, max, DecimalParts.MinExponent),
        ];

        foreach (DecimalParts parts in longest)
        {
            var buffer = new char[42];

            Assert.Equal(42, DecimalTextRules.Format(parts, buffer));
        }

        Assert.True(DecimalTextRules.BufferLength >= 42);
    }

    // What Format writes, the grammar reads back to the same coefficient, exponent and sign: the cohort is kept.
    [Fact]
    public static void FormatThenParse_GivesTheSamePartsOfEveryCohort()
    {
        UInt128 max = DecimalParts.MaxCoefficient;
        DecimalParts[] cases =
        [
            new(false, 150UL, -2),
            new(false, 15UL, -1),
            new(true, 0UL, 0),
            new(false, 0UL, -8),
            new(false, 0UL, 3),
            new(false, 5UL, -7),
            new(true, max, DecimalParts.MaxExponent),
            new(false, max, DecimalParts.MinExponent),
            new(false, Coefficient34, -17),
            new(false, 100UL, 0),
            new(false, 1UL, 2),
        ];

        foreach (DecimalParts parts in cases)
        {
            Assert.Equal((DecimalParseResult.Parsed, parts), Parse(Format(parts), exact: true));
        }
    }

    // The digits of string(double) laid out as that text lays them out: the exponent form below 1e-4 and from 1e6 with the digits as the
    // coefficient, the plain form otherwise, where the digits followed by the zeros that the point asks for are the coefficient.
    [Fact]
    public static void FromDouble_LaysTheDigitsOut_AsStringOfADoubleDoes()
    {
        (double Value, bool Negative, ulong Coefficient, int Exponent)[] cases =
        [
            (100.0, false, 100, 0),
            (1e6, false, 1, 6),
            (1.5e10, false, 15, 9),
            (0.001, false, 1, -3),
            (0.0001, false, 1, -4),
            (1e-5, false, 1, -5),
            (123456.0, false, 123456, 0),
            (999999.0, false, 999999, 0),
            (12345678.0, false, 12345678, 0),
            (1.25, false, 125, -2),
            (-2.5, true, 25, -1),
            (0.1, false, 1, -1),
            (1e100, false, 1, 100),
            (0.0, false, 0, 0),
            (double.Epsilon, false, 5, -324),
            (double.MaxValue, false, 17976931348623157, 292),
            (-double.MaxValue, true, 17976931348623157, 292),
        ];

        foreach ((double value, bool negative, ulong coefficient, int exponent) in cases)
        {
            DecimalTextRules.FromDouble(value, out DecimalParts parts);

            Assert.Equal(new DecimalParts(negative, coefficient, exponent), parts);
        }
    }

    [Fact]
    public static void FromDouble_OfANegativeZero_IsANegativeZero()
    {
        DecimalTextRules.FromDouble(-0.0, out DecimalParts parts);

        Assert.Equal(new DecimalParts(true, UInt128.Zero, 0), parts);
    }

    // The shortest digits read back to the same double, so decimal and back is the identity on every finite double.
    [Fact]
    public static void DoubleToDecimalAndBack_IsTheSameDouble_OnTheEdgesAndOnManyDoubles()
    {
        double[] edges =
        [
            0.0, -0.0, 1.0, -1.0, 0.1, 0.3, 1.0 / 3.0, 1e-5, 1e-4, 1e5, 1e6, 123456.0, 1234567.0, 1e21, 1e22, 1e23, 9007199254740992.0,
            9007199254740993.0, double.Epsilon, -double.Epsilon, double.MaxValue, double.MinValue, 2.2250738585072014E-308,
            4.9406564584124654E-324, Math.PI, Math.E, 5e-324, 1.7976931348623157E+308, 123456789.0, 0.000123,
        ];
        foreach (double value in edges)
        {
            AssertDoubleRoundTrip(value);
        }

        ulong state = 0x9E3779B97F4A7C15UL;
        for (int count = 0; count < 3_000; count++)
        {
            double value = BitConverter.UInt64BitsToDouble(Next(ref state));
            if (double.IsFinite(value))
            {
                AssertDoubleRoundTrip(value);
            }
        }
    }

    [Fact]
    public static void ToDouble_RoundsHalfToEven_AndTheFastPathAgreesWithTheText()
    {
        Assert.Equal((true, 9007199254740992.0), ToDouble(new DecimalParts(false, 9007199254740993UL, 0)));
        Assert.Equal((true, 9007199254740996.0), ToDouble(new DecimalParts(false, 9007199254740995UL, 0)));
        Assert.Equal((true, 1e23), ToDouble(new DecimalParts(false, 1UL, 23)));
        Assert.Equal((true, 0.1), ToDouble(new DecimalParts(false, 1UL, -1)));
        Assert.Equal((true, 1.5), ToDouble(new DecimalParts(false, 15UL, -1)));
        Assert.Equal((true, 1.5), ToDouble(new DecimalParts(false, 1500UL, -3)));

        ulong state = 0xD1B54A32D192ED03UL;
        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        for (int count = 0; count < 2_000; count++)
        {
            ulong coefficient = Next(ref state) & ((1UL << 53) - 1);
            int exponent = (int)(Next(ref state) % 45) - 22;
            var parts = new DecimalParts((Next(ref state) & 1) != 0, coefficient, exponent);
            string written = new(text[..DecimalTextRules.Format(parts, text)]);

            Assert.True(DecimalTextRules.ToDouble(parts, out double fast));
            Assert.Equal(BitConverter.DoubleToInt64Bits(double.Parse(written, CultureInfo.InvariantCulture)), BitConverter.DoubleToInt64Bits(fast));
        }
    }

    [Fact]
    public static void ToDouble_OfAValueBeyondTheRangeOfDouble_IsRefused_AndOfOneThatVanishesIsAZero()
    {
        UInt128 max = DecimalParts.MaxCoefficient;

        Assert.False(DecimalTextRules.ToDouble(new DecimalParts(false, max, DecimalParts.MaxExponent), out _));
        Assert.False(DecimalTextRules.ToDouble(new DecimalParts(true, 1UL, 309), out _));
        Assert.True(DecimalTextRules.ToDouble(new DecimalParts(false, 1UL, 308), out double largest));
        Assert.Equal(1e308, largest);

        Assert.Equal((true, 0.0), ToDouble(new DecimalParts(false, 1UL, -400)));
        Assert.Equal((true, 0.0), ToDouble(new DecimalParts(false, max, DecimalParts.MinExponent)));
        Assert.False(double.IsNegative(ToDouble(new DecimalParts(false, 1UL, -400)).Value));

        // The sign of a zero is kept, also when a nonzero value vanishes.
        Assert.True(double.IsNegative(ToDouble(new DecimalParts(true, 1UL, -400)).Value));
        Assert.True(double.IsNegative(ToDouble(new DecimalParts(true, UInt128.Zero, 5)).Value));
        Assert.True(double.IsNegative(ToDouble(new DecimalParts(true, UInt128.Zero, -300)).Value));
    }

    private static void AssertDoubleRoundTrip(double value)
    {
        DecimalTextRules.FromDouble(value, out DecimalParts parts);

        Assert.True(DecimalTextRules.ToDouble(parts, out double back));
        Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(back));
    }

    private static (bool Result, double Value) ToDouble(DecimalParts parts)
    {
        bool result = DecimalTextRules.ToDouble(parts, out double value);
        return (result, value);
    }

    private static (DecimalParseResult Result, DecimalParts Value) Parse(string text, bool exact)
    {
        DecimalParseResult result = DecimalTextRules.TryParse(text, exact, out DecimalParts value);
        return (result, result == DecimalParseResult.Parsed ? value : default);
    }

    private static string Format(DecimalParts parts)
    {
        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        return new string(text[..DecimalTextRules.Format(parts, text)]);
    }

    // A fixed sequence of numbers for the tests that walk many values: no clock and no shared generator.
    private static ulong Next(ref ulong state)
    {
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        return state;
    }
}
#endif

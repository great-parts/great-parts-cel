using System;
using System.Globalization;
using Great.Parts.Cel.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// Numbers to text and text to numbers without culture: the double text of Go %g, the integer forms of Go
// strconv with base 10, the twelve texts of bool and the forms of Go strconv.ParseFloat.
public sealed class NumberTextTests
{
    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(-0.0, "-0")]
    [InlineData(1.5, "1.5")]
    [InlineData(-0.0045, "-0.0045")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(0.00001, "1e-05")]
    [InlineData(2.5e-7, "2.5e-07")]
    [InlineData(100000.0, "100000")]
    [InlineData(123456.0, "123456")]
    [InlineData(123456.7, "123456.7")]
    [InlineData(1000000.0, "1e+06")]
    [InlineData(1234567.0, "1.234567e+06")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1e100, "1e+100")]
    [InlineData(9223372036854775807.0, "9.223372036854776e+18")]
    [InlineData(double.MaxValue, "1.7976931348623157e+308")]
    [InlineData(double.Epsilon, "5e-324")]
    [InlineData(2.9802322387695312e-08, "2.9802322387695312e-08")]
    [InlineData(4.1045368012983762e-289, "4.1045368012983762e-289")]
    [InlineData(double.PositiveInfinity, "+Inf")]
    [InlineData(double.NegativeInfinity, "-Inf")]
    [InlineData(double.NaN, "NaN")]
    public static void FormatDouble_IsGoPercentG(double value, string text)
    {
        Assert.Equal(text, NumberText.FormatDouble(value));
    }

    // Every text of 20,000 doubles reads back to its double, and its form is the one %g chooses by the exponent of the first digit.
    [Fact]
    public static void FormatDouble_OfRandomDoubles_RoundTripsInItsForm()
    {
        for (int i = 0; i < 20_000; i++)
        {
            double value = Spread(i);
            if (!double.IsFinite(value))
            {
                continue;
            }

            string text = NumberText.FormatDouble(value);
            Assert.Equal(value, double.Parse(text, CultureInfo.InvariantCulture));
            int exponent = value == 0 ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value)));
            bool exponentForm = text.Contains('e', StringComparison.Ordinal);

            // Log10 can be off by one at a power of ten; the form is checked away from them.
            if (Math.Abs(Math.Log10(Math.Abs(value)) - Math.Round(Math.Log10(Math.Abs(value)))) > 1e-9)
            {
                Assert.True(exponentForm == (exponent < -4 || exponent >= 6), text);
            }
        }
    }

    // Every power of two reads back to itself, though the gap to the double below it is half the gap above.
    [Fact]
    public static void FormatDouble_OfEveryPowerOfTwo_RoundTrips()
    {
        for (int exponent = -1074; exponent <= 1023; exponent++)
        {
            double value = Math.ScaleB(1.0, exponent);
            Assert.Equal(value, double.Parse(NumberText.FormatDouble(value), CultureInfo.InvariantCulture));
        }
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("+5", 5L)]
    [InlineData("-42", -42L)]
    [InlineData("007", 7L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public static void ParseInt64_TakesASignedDecimal(string text, long value)
    {
        Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseInt64(text, out long parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("", "Invalid")]
    [InlineData("+", "Invalid")]
    [InlineData("1_0", "Invalid")]
    [InlineData(" 5", "Invalid")]
    [InlineData("5 ", "Invalid")]
    [InlineData("0x10", "Invalid")]
    [InlineData("1.0", "Invalid")]
    [InlineData("١", "Invalid")]
    [InlineData("9223372036854775808", "OutOfRange")]
    [InlineData("-9223372036854775809", "OutOfRange")]
    [InlineData("99999999999999999999999", "OutOfRange")]
    public static void ParseInt64_RejectsOtherTexts(string text, string result)
    {
        Assert.Equal(Enum.Parse<NumberText.ParseResult>(result), NumberText.ParseInt64(text, out _));
    }

    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("42", 42UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public static void ParseUInt64_TakesDigits(string text, ulong value)
    {
        Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseUInt64(text, out ulong parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("+5", "Invalid")]
    [InlineData("-0", "Invalid")]
    [InlineData("1_0", "Invalid")]
    [InlineData("", "Invalid")]
    [InlineData("18446744073709551616", "OutOfRange")]
    public static void ParseUInt64_RejectsASignAndOtherTexts(string text, string result)
    {
        Assert.Equal(Enum.Parse<NumberText.ParseResult>(result), NumberText.ParseUInt64(text, out _));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("t", true)]
    [InlineData("T", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("0", false)]
    [InlineData("f", false)]
    [InlineData("F", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("False", false)]
    public static void TryParseBool_TakesTheTwelveTexts(string text, bool value)
    {
        Assert.True(NumberText.TryParseBool(text, out bool parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("tRUE")]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData(" true")]
    public static void TryParseBool_RejectsOtherTexts(string text)
    {
        Assert.False(NumberText.TryParseBool(text, out _));
    }

    [Theory]
    [InlineData("2", 2.0)]
    [InlineData("7.", 7.0)]
    [InlineData(".25", 0.25)]
    [InlineData("-3.5", -3.5)]
    [InlineData("+2e3", 2000.0)]
    [InlineData("4E-2", 0.04)]
    [InlineData("2_500.0_5", 2500.05)]
    [InlineData("1e1_0", 1e10)]
    [InlineData("0x1p0", 1.0)]
    [InlineData("0x.8p1", 1.0)]
    [InlineData("-0XAp-1", -5.0)]
    [InlineData("0x_1_0p0", 16.0)]
    [InlineData("0x1p-1074", double.Epsilon)]
    [InlineData("0x1.fffffffffffffp1023", double.MaxValue)]
    [InlineData("1e-400", 0.0)]
    [InlineData("INF", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    [InlineData("+inf", double.PositiveInfinity)]
    public static void ParseDouble_TakesTheFormsOfGo(string text, double value)
    {
        Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseDouble(text, out double parsed));
        Assert.Equal(value, parsed);
    }

    [Fact]
    public static void ParseDouble_TakesNanWithoutASign()
    {
        Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseDouble("nAn", out double parsed));
        Assert.True(double.IsNaN(parsed));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("+")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("1e_5")]
    [InlineData("1.2.3")]
    [InlineData("_5")]
    [InlineData("5_")]
    [InlineData("5__0")]
    [InlineData("5_.0")]
    [InlineData("5._0")]
    [InlineData("0x10")]
    [InlineData("0x")]
    [InlineData("0xp1")]
    [InlineData("0x1e2")]
    [InlineData("0b101")]
    [InlineData("0o17")]
    [InlineData(" 1")]
    [InlineData("1,5")]
    [InlineData("-nan")]
    [InlineData("+NaN")]
    [InlineData("infinit")]
    [InlineData("infinityy")]
    [InlineData("١")]
    public static void ParseDouble_RejectsOtherTexts(string text)
    {
        Assert.Equal(NumberText.ParseResult.Invalid, NumberText.ParseDouble(text, out _));
    }

    [Theory]
    [InlineData("1e309")]
    [InlineData("-1e400")]
    [InlineData("1.7976931348623159e308")]
    [InlineData("0x1p1024")]
    [InlineData("0x1.fffffffffffff8p1023")]
    public static void ParseDouble_BeyondTheLargestDouble_IsOutOfRange(string text)
    {
        Assert.Equal(NumberText.ParseResult.OutOfRange, NumberText.ParseDouble(text, out _));
    }

    // The rounding of hexadecimal mantissas to 53 bits: a half step goes to the even neighbor, a sticky digit
    // beyond it rounds up; subnormal numbers keep fewer bits.
    [Theory]
    [InlineData("0x1.fffffffffffff8p0", 2.0)]
    [InlineData("0x1.00000000000008p0", 1.0)]
    [InlineData("0x1.00000000000018p0", 1.0000000000000004)]
    [InlineData("0x1.00000000000009p0", 1.0000000000000002)]
    [InlineData("0x1.000000000000080000000001p0", 1.0000000000000002)]
    [InlineData("0x1p-1075", 0.0)]
    [InlineData("0x1.8p-1075", double.Epsilon)]
    [InlineData("0x1p-1076", 0.0)]
    [InlineData("0x0.0000000000001p-1022", double.Epsilon)]
    [InlineData("0x100000000000000000000p-80", 1.0)]
    public static void ParseDouble_RoundsAHexadecimalToNearestTiesToEven(string text, double value)
    {
        Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseDouble(text, out double parsed));
        Assert.Equal(value, parsed);
    }

    // Each of 20,000 finite doubles reads back from its decimal text and from its exact hexadecimal text, which the test
    // writes from the bits of the double.
    [Fact]
    public static void ParseDouble_OfTheTextsOfRandomDoubles_IsTheDouble()
    {
        for (int i = 0; i < 20_000; i++)
        {
            double value = Spread(i);
            if (!double.IsFinite(value))
            {
                continue;
            }

            Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseDouble(value.ToString("R", CultureInfo.InvariantCulture), out double fromDecimal));
            Assert.Equal(value, fromDecimal);
            Assert.Equal(NumberText.ParseResult.Parsed, NumberText.ParseDouble(Hexadecimal(value), out double fromHexadecimal));
            Assert.Equal(value, fromHexadecimal);
        }
    }

    [Theory]
    [InlineData(0UL, 0.0)]
    [InlineData(9007199254740993UL, 9007199254740992.0)]
    [InlineData(9223372036854775808UL, 9223372036854775808.0)]
    [InlineData(9223372036854776833UL, 9223372036854777856.0)]
    [InlineData(18446744073709550591UL, 18446744073709549568.0)]
    [InlineData(18446744073709550593UL, 18446744073709551616.0)]
    [InlineData(ulong.MaxValue, 18446744073709551616.0)]
    public static void ToDouble_OfAUInt_IsTheNearestTiesToEven(ulong value, double nearest)
    {
        Assert.Equal(nearest, NumberText.ToDouble(value));
    }

    // The bits of the i-th double of a Weyl sequence, spread over the whole range of bit patterns.
    private static double Spread(int i) => BitConverter.UInt64BitsToDouble(unchecked((ulong)(i + 1) * 0x9E3779B97F4A7C15UL));

    // [-]0x1.<13 hex digits>p<exponent>, or 0x0.<digits>p-1022 for a subnormal number.
    private static string Hexadecimal(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        string sign = bits < 0 ? "-" : string.Empty;
        long exponent = (bits >> 52) & 0x7FF;
        long fraction = bits & 0xF_FFFF_FFFF_FFFF;
        return exponent == 0
            ? sign + "0x0." + fraction.ToString("x13", CultureInfo.InvariantCulture) + "p-1022"
            : sign + "0x1." + fraction.ToString("x13", CultureInfo.InvariantCulture) + "p" + (exponent - 1023).ToString(CultureInfo.InvariantCulture);
    }
}

using System;
using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.FormatEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The fixed and scientific forms of %f and %e against a reference written here over BigInteger: the exact value of the double
// and the rounding to the nearest digit, an exact tie to the even digit, as Go rounds. The doubles are those where the forms of .NET
// and the exact value could differ: ties, values that no short decimal text holds, the largest and the smallest, with a precision
// from 0 to 100.
public sealed class FormatFloatTests
{
    private static readonly double[] Values =
    [
        0.0,
        -0.0,
        0.5,
        1.5,
        2.5,
        0.125,
        0.375,
        0.0625,
        1.0 / 1024,
        1.0 / 3,
        2.0 / 3,
        0.1,
        0.7,
        1.005,
        2.675,
        123456.789,
        -987654.321,
        1e15,
        1e16,
        1e21,
        1e23,
        1.2345678901234567e19,
        9007199254740993.0,
        4503599627370497.5,
        1e-7,
        1.5e-10,
        -3.25e-5,
        1e300,
        -1.7976931348623157e308,
        double.Epsilon,
        2.2250738585072014e-308,
        1e-300,
    ];

    private static readonly int[] Precisions = [0, 1, 2, 3, 6, 10, 17, 20, 50, 99, 100];

    public static TheoryData<int> PrecisionRows => Data(Precisions);

    [Theory]
    [MemberData(nameof(PrecisionRows))]
    public static void Fixed_IsTheExactValueRoundedToTheNearestDigitWithTiesToEven(int precision)
    {
        foreach (double value in Values)
        {
            string expected = ExactFixed(value, precision);
            string actual = Format("%." + precision.ToString(CultureInfo.InvariantCulture) + "f", CelValue.FromDouble(value));

            Assert.True(expected == actual, value.ToString("R", CultureInfo.InvariantCulture) + " with " + precision + " digits: expected " + expected + " but got " + actual);
        }
    }

    [Theory]
    [MemberData(nameof(PrecisionRows))]
    public static void Scientific_IsTheExactValueRoundedToTheNearestDigitWithTiesToEven(int precision)
    {
        foreach (double value in Values)
        {
            string expected = ExactScientific(value, precision);
            string actual = Format("%." + precision.ToString(CultureInfo.InvariantCulture) + "e", CelValue.FromDouble(value));

            Assert.True(expected == actual, value.ToString("R", CultureInfo.InvariantCulture) + " with " + precision + " digits: expected " + expected + " but got " + actual);
        }
    }

    // The shortest form reads back to the same double and has no digit that is not needed: a double and the text that the clause gives
    // for it are the same number.
    [Fact]
    public static void Shortest_ReadsBackToTheSameDouble()
    {
        foreach (double value in Values)
        {
            string text = Format("%s", CelValue.FromDouble(value));

            Assert.True(
                BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(double.Parse(text, CultureInfo.InvariantCulture)),
                value.ToString("R", CultureInfo.InvariantCulture) + " became " + text);
            Assert.False(text.Contains('e', StringComparison.OrdinalIgnoreCase), text);
        }
    }

    // value = mantissa × 2^exponent with an integer mantissa; the sign is separate.
    private static (bool Negative, BigInteger Mantissa, int Exponent) Decompose(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        bool negative = bits < 0;
        int biased = (int)((bits >> 52) & 0x7FF);
        long fraction = bits & 0xFFFFFFFFFFFFFL;
        return biased == 0 ? (negative, fraction, -1074) : (negative, fraction | (1L << 52), biased - 1075);
    }

    // The digits of round(value × 10^scale) with the ties to even, for a scale that is negative or not.
    private static BigInteger Scaled(double value, int scale)
    {
        (_, BigInteger mantissa, int exponent) = Decompose(value);
        BigInteger numerator = mantissa;
        BigInteger denominator = BigInteger.One;
        if (exponent >= 0)
        {
            numerator <<= exponent;
        }
        else
        {
            denominator <<= -exponent;
        }

        if (scale >= 0)
        {
            numerator *= BigInteger.Pow(10, scale);
        }
        else
        {
            denominator *= BigInteger.Pow(10, -scale);
        }

        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        int comparison = (remainder * 2).CompareTo(denominator);
        return comparison > 0 || (comparison == 0 && !quotient.IsEven) ? quotient + 1 : quotient;
    }

    private static string ExactFixed(double value, int precision)
    {
        string digits = Scaled(value, precision).ToString(CultureInfo.InvariantCulture).PadLeft(precision + 1, '0');
        string sign = double.IsNegative(value) ? "-" : string.Empty;
        return precision == 0
            ? sign + digits
            : sign + digits[..^precision] + "." + digits[^precision..];
    }

    // d.ddd e±XX: the decimal exponent is that of the value, raised by one when the rounding carries to a new digit.
    private static string ExactScientific(double value, int precision)
    {
        string sign = double.IsNegative(value) ? "-" : string.Empty;
        if (value == 0)
        {
            return sign + "0" + (precision == 0 ? string.Empty : "." + new string('0', precision)) + "e+00";
        }

        int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        BigInteger scaled = Scaled(value, precision - exponent);
        BigInteger low = BigInteger.Pow(10, precision);
        while (scaled < low)
        {
            exponent--;
            scaled = Scaled(value, precision - exponent);
        }

        while (scaled >= low * 10)
        {
            exponent++;
            scaled = Scaled(value, precision - exponent);
        }

        string digits = scaled.ToString(CultureInfo.InvariantCulture);
        string mantissa = precision == 0 ? digits : digits[..1] + "." + digits[1..];
        string exponentText = Math.Abs(exponent).ToString("D2", CultureInfo.InvariantCulture);
        return sign + mantissa + "e" + (exponent < 0 ? "-" : "+") + exponentText;
    }

    private static TheoryData<int> Data(int[] items)
    {
        TheoryData<int> data = [];
        foreach (int item in items)
        {
            data.Add(item);
        }

        return data;
    }
}

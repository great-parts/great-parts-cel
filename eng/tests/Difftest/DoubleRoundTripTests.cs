using System;
using System.Globalization;
using Great.Parts.Cel.Eng.Difftest.Numbers;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The text of a double in eng/difftest: the shortest "G" text that reads back to the same bits, without "R", which on .NET 10
// writes 2^-25 (0x3E60000000000000) as the text of 0x3E5FFFFFFFFFFFFF. This project builds for net10.0, so the tests run there.
public sealed class DoubleRoundTripTests
{
    private const long TwoToTheMinus25 = 0x3E60000000000000;

    private const int RandomSamples = 200_000;

    [Theory]
    [InlineData(TwoToTheMinus25, "2.9802322387695312E-08")]
    [InlineData(0x3FB999999999999A, "0.1")]
    [InlineData(0x3FF8000000000000, "1.5")]
    [InlineData(0x4097700000000000, "1.5E+03")]
    [InlineData(0L, "0")]
    [InlineData(long.MinValue, "-0")]
    [InlineData(1L, "5E-324")]
    [InlineData(0x7FEFFFFFFFFFFFFF, "1.7976931348623157E+308")]
    public static void Format_GivesTheShortestTextThatReadsBack(long bits, string text)
    {
        Assert.Equal(text, DoubleRoundTrip.Format(BitConverter.Int64BitsToDouble(bits)));
    }

    [Fact]
    public void Format_ReadsBackForEveryPowerOfTwoAndItsNegation()
    {
        for (int exponent = -1074; exponent <= 1023; exponent++)
        {
            double power = Math.ScaleB(1, exponent);
            AssertReadsBack(power);
            AssertReadsBack(-power);
        }
    }

    [Fact]
    public void Format_ReadsBackForRandomBits()
    {
        ulong state = 20261102;
        int finite = 0;
        while (finite < RandomSamples)
        {
            double value = BitConverter.Int64BitsToDouble((long)SplitMix64(ref state));
            if (double.IsFinite(value))
            {
                AssertReadsBack(value);
                finite++;
            }
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public static void Format_RefusesAValueThatIsNotFinite(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DoubleRoundTrip.Format(value));
    }

    [Fact]
    public void PrintedLiteralOfTwoToTheMinus25_ReadsBackToItsBitsInEveryNotation()
    {
        double value = BitConverter.Int64BitsToDouble(TwoToTheMinus25);

        Assert.Equal(("29802322387695312", -8), ExprPrinter.ShortestDecimal(value));
        foreach (DoubleForm form in Enum.GetValues<DoubleForm>())
        {
            string text = ExprPrinter.Print(new DoubleLiteral(value, form));
            Assert.Equal(TwoToTheMinus25, BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)));
        }
    }

    // A fixed sequence of 64-bit patterns, so a failure repeats.
    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15;
        ulong mixed = state;
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EB;
        return mixed ^ (mixed >> 31);
    }

    private static void AssertReadsBack(double value)
    {
        string text = DoubleRoundTrip.Format(value);
        long expected = BitConverter.DoubleToInt64Bits(value);
        long read = BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
        Assert.True(read == expected, string.Create(CultureInfo.InvariantCulture, $"{text} reads back as 0x{read:X16}, not 0x{expected:X16}"));
    }
}

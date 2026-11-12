#if NET11_0_OR_GREATER
using System;
using System.Globalization;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// int, uint and System.Decimal to and from the parts of a decimal: the truncation toward zero and the check of the range at the
// edges of the two integers, and the exact and the rounded conversions of System.Decimal on every branch.
public sealed class DecimalNumberRulesTests
{
    private const string MaxSystem = "79228162514264337593543950335";

    // 10^28, the largest power of ten that System.Decimal holds.
    private static readonly decimal TenToThe28 = decimal.Parse("1" + new string('0', 28), CultureInfo.InvariantCulture);

    [Fact]
    public static void FromInt64_IsTheMagnitudeWithExponentZero_AndTheSignOfTheNumber()
    {
        Assert.Equal(new DecimalParts(false, UInt128.Zero, 0), DecimalNumberRules.FromInt64(0));
        Assert.Equal(new DecimalParts(true, 1UL, 0), DecimalNumberRules.FromInt64(-1));
        Assert.Equal(new DecimalParts(false, (ulong)long.MaxValue, 0), DecimalNumberRules.FromInt64(long.MaxValue));
        Assert.Equal(new DecimalParts(true, 1UL << 63, 0), DecimalNumberRules.FromInt64(long.MinValue));
        Assert.Equal(new DecimalParts(false, ulong.MaxValue, 0), DecimalNumberRules.FromUInt64(ulong.MaxValue));
        Assert.Equal(new DecimalParts(false, UInt128.Zero, 0), DecimalNumberRules.FromUInt64(0));
    }

    [Fact]
    public static void ToInt64_TruncatesTowardZero_AndRefusesWhatIsOutsideTheRange()
    {
        (bool Negative, string Coefficient, int Exponent, long? Expected)[] cases =
        [
            (false, "9223372036854775807", 0, long.MaxValue),
            (false, "9223372036854775808", 0, null),
            (false, "92233720368547758079", -1, long.MaxValue),
            (false, "92233720368547758080", -1, null),
            (true, "9223372036854775808", 0, long.MinValue),
            (true, "9223372036854775809", 0, null),
            (true, "92233720368547758089", -1, long.MinValue),
            (true, "92233720368547758090", -1, null),
            (false, "15", -1, 1),
            (true, "15", -1, -1),
            (true, "5", -1, 0),
            (false, "0", 0, 0),
            (true, "0", 7, 0),
            (false, "25", 2, 2500),
            (false, "1", 18, 1_000_000_000_000_000_000),
            (false, "1", 19, null),
            (false, "1", 20, null),
            (false, "1", 6111, null),
            (true, "1", 19, null),
            (false, "9999999999999999999999999999999999", -39, 0),
            (false, "9999999999999999999999999999999999", -38, 0),
            (false, "9999999999999999999999999999999999", -34, 0),
            (false, "9999999999999999999999999999999999", -33, 9),
            (true, "9999999999999999999999999999999999", -6176, 0),
        ];

        foreach ((bool negative, string coefficient, int exponent, long? expected) in cases)
        {
            var parts = new DecimalParts(negative, UInt128.Parse(coefficient, CultureInfo.InvariantCulture), exponent);

            Assert.Equal(expected is not null, DecimalNumberRules.ToInt64(parts, out long result));
            Assert.Equal(expected ?? 0, expected is null ? 0 : result);
        }
    }

    // The sign is tested after the truncation, unlike uint(double): -0.5 is 0, -1.5 is out of range.
    [Fact]
    public static void ToUInt64_TruncatesTowardZero_AndRefusesANegativeThatIsNotZeroAfterTheTruncation()
    {
        (bool Negative, string Coefficient, int Exponent, ulong? Expected)[] cases =
        [
            (false, "18446744073709551615", 0, ulong.MaxValue),
            (false, "18446744073709551616", 0, null),
            (false, "184467440737095516159", -1, ulong.MaxValue),
            (false, "184467440737095516160", -1, null),
            (false, "1", 19, 10_000_000_000_000_000_000),
            (false, "2", 19, null),
            (false, "1", 20, null),
            (true, "5", -1, 0),
            (true, "9", -1, 0),
            (true, "0", 0, 0),
            (true, "0", -3, 0),
            (true, "15", -1, null),
            (true, "1", 0, null),
            (false, "15", -1, 1),
            (false, "7", -1, 0),
            (false, "9999999999999999999999999999999999", -39, 0),
            (false, "1", 6111, null),
        ];

        foreach ((bool negative, string coefficient, int exponent, ulong? expected) in cases)
        {
            var parts = new DecimalParts(negative, UInt128.Parse(coefficient, CultureInfo.InvariantCulture), exponent);

            Assert.Equal(expected is not null, DecimalNumberRules.ToUInt64(parts, out ulong result));
            Assert.Equal(expected ?? 0, expected is null ? 0 : result);
        }
    }

    [Fact]
    public static void FromSystemDecimal_IsExact_WithTheScaleAsTheNegatedExponentAndTheSignOfAZero()
    {
        UInt128 maxSystem = UInt128.Parse(MaxSystem, CultureInfo.InvariantCulture);

        Assert.Equal(new DecimalParts(false, 150UL, -2), DecimalNumberRules.FromSystemDecimal(1.50m));
        Assert.Equal(new DecimalParts(false, 15UL, -1), DecimalNumberRules.FromSystemDecimal(1.5m));
        Assert.Equal(new DecimalParts(false, 100UL, 0), DecimalNumberRules.FromSystemDecimal(100m));
        Assert.Equal(new DecimalParts(false, maxSystem, 0), DecimalNumberRules.FromSystemDecimal(decimal.MaxValue));
        Assert.Equal(new DecimalParts(true, maxSystem, 0), DecimalNumberRules.FromSystemDecimal(decimal.MinValue));
        Assert.Equal(new DecimalParts(false, 1UL, -28), DecimalNumberRules.FromSystemDecimal(0.0000000000000000000000000001m));
        Assert.Equal(new DecimalParts(false, maxSystem, -28), DecimalNumberRules.FromSystemDecimal(7.9228162514264337593543950335m));
        Assert.Equal(new DecimalParts(true, UInt128.Zero, -2), DecimalNumberRules.FromSystemDecimal(new decimal(0, 0, 0, true, 2)));
        Assert.Equal(new DecimalParts(false, UInt128.Zero, -5), DecimalNumberRules.FromSystemDecimal(new decimal(0, 0, 0, false, 5)));
        Assert.Equal(new DecimalParts(false, UInt128.One << 64, 0), DecimalNumberRules.FromSystemDecimal(18446744073709551616m));
        Assert.Equal(new DecimalParts(false, UInt128.One << 32, 0), DecimalNumberRules.FromSystemDecimal(4294967296m));
    }

    [Fact]
    public static void ToSystemDecimal_IsExactWhenTheValueFits_AndKeepsTheScaleAndTheSign()
    {
        decimal[] values =
        [
            1.50m, 1.5m, 100m, 0m, decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m, 7.9228162514264337593543950335m,
            -123.456m, 18446744073709551616m, 4294967296m, TenToThe28,
        ];

        foreach (decimal value in values)
        {
            Assert.True(DecimalNumberRules.ToSystemDecimal(DecimalNumberRules.FromSystemDecimal(value), out decimal result));
            Assert.Equal(Bits(value), Bits(result));
        }

        Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(true, UInt128.Zero, -2), out decimal negativeZero));
        Assert.Equal((true, 2), SignAndScale(negativeZero));
        Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(false, UInt128.Zero, 6111), out decimal zero));
        Assert.Equal((false, 0), SignAndScale(zero));
        Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(true, UInt128.Zero, 6111), out decimal negativeIntegerZero));
        Assert.Equal((true, 0), SignAndScale(negativeIntegerZero));
    }

    // The digits beyond a scale of 28 are rounded away, half to even, and the scale is the largest that keeps the value.
    [Fact]
    public static void ToSystemDecimal_RoundsHalfToEvenBeyondAScaleOf28()
    {
        // The digits that remain in the 28 places after the point.
        (ulong Coefficient, int Exponent, string Places)[] cases =
        [
            (15, -29, "2"),
            (25, -29, "2"),
            (35, -29, "4"),
            (5, -29, "0"),
            (1, -30, "0"),
            (51, -30, "1"),
            (123, -29, "12"),
        ];

        foreach ((ulong coefficient, int exponent, string places) in cases)
        {
            Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(false, coefficient, exponent), out decimal result));
            Assert.Equal("0." + places.PadLeft(28, '0'), result.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(28, SignAndScale(result).Scale);
        }

        Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(true, UInt128.Zero, -40), out decimal negativeZero));
        Assert.Equal((true, 28), SignAndScale(negativeZero));
    }

    // A coefficient above 96 bits loses one more digit of the original coefficient, rounded from it, and so the scale; when the scale
    // would be negative the integer part is too large.
    [Fact]
    public static void ToSystemDecimal_DropsMoreDigits_WhileTheCoefficientDoesNotFit96Bits()
    {
        var fiveMore = new DecimalParts(false, UInt128.Parse("792281625142643375935439503354", CultureInfo.InvariantCulture), -1);
        var tooLarge = new DecimalParts(false, UInt128.Parse("792281625142643375935439503356", CultureInfo.InvariantCulture), -1);
        var nines = new DecimalParts(false, DecimalParts.MaxCoefficient, -6);

        Assert.True(DecimalNumberRules.ToSystemDecimal(fiveMore, out decimal result));
        Assert.Equal(decimal.MaxValue, result);
        Assert.Equal(0, SignAndScale(result).Scale);

        Assert.False(DecimalNumberRules.ToSystemDecimal(tooLarge, out _));

        Assert.True(DecimalNumberRules.ToSystemDecimal(nines, out decimal rounded));
        Assert.Equal(TenToThe28, rounded);
        Assert.Equal(0, SignAndScale(rounded).Scale);
    }

    [Fact]
    public static void ToSystemDecimal_RefusesWhatHasMoreDigitsOrMoreThanTheRangeInTheIntegerPart()
    {
        DecimalParts[] refused =
        [
            new(false, 1UL, 29),
            new(false, 80UL, 27),
            new(false, UInt128.Parse("79228162514264337593543950336", CultureInfo.InvariantCulture), 0),
            new(true, UInt128.Parse("79228162514264337593543950336", CultureInfo.InvariantCulture), 0),
            new(false, DecimalParts.MaxCoefficient, 0),
            new(false, DecimalParts.MaxCoefficient, -1),
            new(false, 1UL, DecimalParts.MaxExponent),
        ];

        foreach (DecimalParts parts in refused)
        {
            Assert.False(DecimalNumberRules.ToSystemDecimal(parts, out decimal result));
            Assert.Equal(0m, result);
        }

        Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(false, 79UL, 27), out decimal edge));
        Assert.Equal(decimal.Parse("79" + new string('0', 27), CultureInfo.InvariantCulture), edge);
        Assert.True(DecimalNumberRules.ToSystemDecimal(new DecimalParts(false, 1UL, 28), out decimal tenToThe28));
        Assert.Equal(TenToThe28, tenToThe28);
    }

    private static int[] Bits(decimal value) => decimal.GetBits(value);

    private static (bool Negative, int Scale) SignAndScale(decimal value)
    {
        int flags = decimal.GetBits(value)[3];
        return (flags < 0, (flags >> 16) & 0xFF);
    }
}
#endif

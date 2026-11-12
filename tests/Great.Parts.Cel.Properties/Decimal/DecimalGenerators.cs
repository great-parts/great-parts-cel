#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using CsCheck;

namespace Great.Parts.Cel.Properties.Decimals;

// The values of the properties of decimal and the boundaries they must meet, set explicitly: coefficients of 1, 34 and 35 digits,
// nines, ten to a power and values just below and above a tie; the exponents -6176, -6175, 6110, 6111 and the range between; the
// results that vanish below -6176 and those that overflow; both zeros in every position; 1 / 3; cohorts of one value; the
// boundaries of the integers, of the double and of System.Decimal. A generator builds case i from the stream i and the seed by the
// PCG of CsCheck (the loop of PropertyLoop), so a case is the same in every run.
internal static class DecimalGenerators
{
    private static readonly BigInteger Nines = ReferenceDecimal.MaxCoefficient;

    private static readonly int[] ExtremeExponents = [-6176, -6175, 6110, 6111, 0, 1, -1];

    private static readonly string[] InvalidTexts =
    [
        string.Empty, ".", "+", "-", "e5", "1e", "1e+", "1.2.3", " 1", "1 ", "1_0", "0x10", "Infinity", "NaN", "-inf", "1,5",
        "١٢٣", "1e5.5", "--1", "+-1", ".e5", "1.e", "\n1", "1 ", "1E+-5", "1e 5", "m", "1m", "0b1", "1f",
    ];

    private static readonly string[] SpoilingCharacters = [" ", "_", ",", "x", "١", "$", "\n"];

    private static readonly int[] TieShifts = [0, 1, 2, 7, 30, 59];

    // Every boundary value of the arithmetic.
    internal static readonly RefDecimal[] Edges =
    [
        // zeros of both signs: the exponent 0, a fraction, a positive exponent, the largest and the smallest exponent
        Make(0, 0), Make(0, 0, true), Make(0, -1), Make(0, -1, true), Make(0, 3), Make(0, 3, true),
        Make(0, 6111), Make(0, 6111, true), Make(0, -6176), Make(0, -6176, true),
        // one digit, ones and fives, and cohorts of one value
        Make(1, 0), Make(1, 0, true), Make(1, 1), Make(1, -1), Make(5, -1), Make(5, -1, true), Make(15, -1), Make(150, -2), Make(25, -1),
        Make(3, 0), Make(7, 0), Make(10, 0), Make(100, -2),
        // the extreme exponents; a coefficient of 34 digits below 10^-6143 is a subnormal value
        Make(1, -6176), Make(1, -6176, true), Make(1, -6175), Make(1, 6111), Make(1, 6110), Make(1, -6143), Make(5, -6176), Make(15, -6176),
        Make(123, -6176), new(false, Nines, -6176), new(true, Nines, -6176), new(false, Nines, -6175), new(false, Ten(32), -6176),
        // 34 digits: nines and ten to the 33rd, in the middle and at the extreme exponents
        new(false, Nines, 0), new(true, Nines, 0), new(false, Nines, 6111), new(true, Nines, 6111), new(false, Nines, 6110),
        new(false, Ten(33), 0), new(false, Ten(33), 6111), new(false, Ten(33), 6110), new(false, Ten(33) + 1, 0), new(false, Nines - 1, 0),
        new(false, Nines, -1), new(false, Nines, -34),
    ];

    // Pairs that the generator of pairs takes as they are: quotients with and without a finite expansion, a tie that carries into a
    // 35th digit, an overflow of a product, a quotient that vanishes, and the zeros of both signs.
    internal static readonly RefPair[] FixedPairs =
    [
        new(Make(1, 0), Make(3, 0)), new(Make(2, 0), Make(3, 0)), new(Make(1, 0), Make(7, 0)), new(Make(10, 0), Make(3, 0)),
        new(Make(100, -2), Make(4, 0)), new(Make(10, 0), Make(4, 0)), new(Make(6, 0), Make(2, 0)), new(Make(40, -1), Make(2, 0)),
        new(Make(0, -2), Make(5, 0)), new(Make(1, 2), Make(1, 1)), new(Make(1, 0), Make(8, 0)), new(Make(1, 0, true), Make(3, 0)),
        new(Make(1, 6111), Make(1, -1)), new(Make(1, -6176), Make(1, 1)), new(Make(1, -6176), Make(1, -1)),
        new(Make(1, 6111), Make(1, 1)), new(Make(1, 6111), Make(1, 34)),
        new(new RefDecimal(false, Nines, 0), Make(5, -1)), new(new RefDecimal(false, Nines, 6111), new RefDecimal(false, Nines, 6111)),
        new(new RefDecimal(false, Nines, 0), Make(1, 0)), new(new RefDecimal(false, Nines, 6111), Make(1, 6111)),
        new(Make(0, 0, true), Make(0, 0, true)), new(Make(0, 0, true), Make(0, 0)), new(Make(0, 0), Make(0, 0, true)),
        new(Make(15, -1), Make(150, -2)), new(Make(0, 0), Make(0, -5)),
    ];

    // The boundaries of int() and uint(): the values around 2^63 and 2^64 with a fraction, and the truncation of a small fraction.
    internal static readonly RefDecimal[] IntegerEdges =
    [
        Make(0, 0), Make(0, 0, true), Make(5, -1), Make(5, -1, true), Make(15, -1, true), Make(1, 0), Make(1, 0, true),
        Make(999, -3), Make(999, -3, true), Make(1, 19), Make(1, 19, true), Make(1, 20), Make(1, 5000),
        Make(9223372036854775807, 0), Make(9223372036854775807, 0, true), Make(9223372036854775808, 0), Make(9223372036854775808, 0, true),
        Make(9223372036854775809, 0, true), new(false, Big("92233720368547758075"), -1), new(true, Big("92233720368547758075"), -1),
        new(true, Big("92233720368547758089"), -1), new(false, Big("92233720368547758079"), -1),
        Make(18446744073709551615, 0), new(false, Big("18446744073709551616"), 0), new(false, Big("184467440737095516159"), -1),
        new(false, Big("184467440737095516155"), -1), new(true, Big("184467440737095516155"), -1),
    ];

    // The boundaries of the conversion to System.Decimal: the largest value with a fraction, the digits that are rounded away, the
    // value 1E+29, and the places 28 and 29.
    internal static readonly RefDecimal[] SystemRangeEdges =
    [
        Make(1, 0), Make(150, -2), Make(150, -2, true), Make(0, -30), Make(0, -30, true), Make(0, 3), Make(0, -5, true),
        new(false, Big("79228162514264337593543950335"), 0), new(false, Big("79228162514264337593543950336"), 0),
        new(true, Big("79228162514264337593543950335"), 0), new(true, Big("79228162514264337593543950336"), 0),
        new(false, Big("792281625142643375935439503354"), -1), new(false, Big("792281625142643375935439503355"), -1),
        new(false, Big("792281625142643375935439503356"), -1), new(true, Big("792281625142643375935439503355"), -1),
        new(false, Big("79228162514264337593543950335"), -28), new(false, Big("792281625142643375935439503350"), -29),
        new(false, Big("792281625142643375935439503355"), -29), new(false, Big("792281625142643375935439503351"), -29),
        Make(15, -29), Make(25, -29), Make(35, -29), Make(5, -29), Make(51, -30), Make(1, -30), Make(1, -28), Make(1, 28), Make(1, 29),
        Make(79228162514264337, 12), new(false, Nines, -34),
    ];

    internal static readonly decimal[] SystemDecimalEdges =
    [
        decimal.MaxValue, decimal.MinValue, 0m, new decimal(0, 0, 0, true, 0), new decimal(0, 0, 0, true, 28), new decimal(0, 0, 0, false, 28),
        1.5m, 1.50m, -1.50m, 79228162514264337593543950334m, 0.0000000000000000000000000001m, 7.9228162514264337593543950335m,
        12345.6789m, new decimal(-1, 0, 0, false, 28), 1m, -1m, new decimal(1, 0, 0, true, 28),
    ];

    internal static readonly long[] LongEdges =
    [
        0, 1, -1, long.MaxValue, long.MinValue, long.MinValue + 1, long.MaxValue - 1, 1_000_000_000_000_000_000, 999_999_999_999_999_999,
        int.MaxValue, int.MinValue, uint.MaxValue, 10, -10, 99, 100,
    ];

    internal static readonly ulong[] ULongEdges =
    [
        0, 1, ulong.MaxValue, ulong.MaxValue - 1, long.MaxValue, (ulong)long.MaxValue + 1, 10_000_000_000_000_000_000, 9_999_999_999_999_999_999,
        1_000_000_000_000_000_000, uint.MaxValue, 10, 99, 100,
    ];

    internal static readonly double[] DoubleEdges =
    [
        0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon, -double.Epsilon, double.MaxValue,
        double.MinValue, 2.2250738585072014E-308, 1.0, -1.0, 100.0, 999999.0, 1000000.0, 100000.0, 0.0001, 0.00001, 0.000123456, 123456789.0,
        1.5e10, 1e15, 1e16, 1e21, 1e22, 1e23, 0.5, 0.1, 9007199254740993.0, 1.7976931348623157E+308 / 3,
    ];

    // The decimal values around the places where a double rounds: the largest double and the first value that is an infinity, the
    // ties between two doubles, the half of the smallest subnormal value, and the values that are far outside.
    internal static readonly RefDecimal[] DoubleDecimalEdges = BuildDoubleDecimalEdges();

    internal static readonly Gen<RefDecimal> Values = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Value(new PCG(stream, seed)));

    internal static readonly Gen<RefPair> Pairs = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Pair(new PCG(stream, seed)));

    internal static readonly Gen<string> Texts = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Text(new PCG(stream, seed)));

    internal static readonly Gen<RefDecimal> IntegerValues = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => IntegerValue(new PCG(stream, seed)));

    internal static readonly Gen<RefDecimal> DoubleValues = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => DoubleValue(new PCG(stream, seed)));

    internal static readonly Gen<RefDecimal> SystemRangeValues = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => SystemRangeValue(new PCG(stream, seed)));

    internal static readonly Gen<long> Longs = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Long(new PCG(stream, seed)));

    internal static readonly Gen<ulong> ULongs = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => ULong(new PCG(stream, seed)));

    internal static readonly Gen<double> Doubles = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Double(new PCG(stream, seed)));

    internal static readonly Gen<decimal> SystemDecimals = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => SystemDecimal(new PCG(stream, seed)));

    // The pairs of the boundaries: every boundary value with every other, and the fixed pairs.
    internal static IEnumerable<RefPair> EdgePairs()
    {
        foreach (RefDecimal first in Edges)
        {
            foreach (RefDecimal second in Edges)
            {
                yield return new RefPair(first, second);
            }
        }

        foreach (RefPair pair in FixedPairs)
        {
            yield return pair;
        }
    }

    internal static RefDecimal Make(ulong coefficient, int exponent, bool negative = false) => new(negative, coefficient, exponent);

    internal static BigInteger Ten(int power) => ReferenceDecimal.Pow10(power);

    private static BigInteger Big(string digits) => ReferenceDecimalText.FromDigits(digits);

    private static int Pick(PCG random, int count) => (int)random.Next((uint)count);

    private static ulong Bits64(PCG random) => ((ulong)random.Next(uint.MaxValue) << 32) | (ulong)random.Next(uint.MaxValue);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    // A number of exactly the given number of digits, the first one not zero.
    private static BigInteger Digits(PCG random, int count)
    {
        BigInteger value = 1 + Pick(random, 9);
        for (int digit = 1; digit < count; digit++)
        {
            value = (value * 10) + Pick(random, 10);
        }

        return value;
    }

    // A coefficient of 1 to 34 digits: random digits, nines, a one with zeros, a value with trailing zeros (a cohort), a value that
    // ends in 5, a value just below, at and above a half, and zero.
    private static BigInteger Coefficient(PCG random)
    {
        int digits = 1 + Pick(random, ReferenceDecimal.Digits);
        return Pick(random, 10) switch
        {
            0 => BigInteger.Zero,
            1 => Ten(digits) - 1,
            2 => Ten(digits - 1),
            3 => WithTrailingZeros(random, digits),
            4 => (Digits(random, Math.Max(digits - 1, 1)) * 10) + 5,
            5 => (5 * Ten(digits - 1)) + Pick(random, 3) - 1,
            _ => Digits(random, digits),
        };
    }

    private static BigInteger WithTrailingZeros(PCG random, int digits)
    {
        int zeros = Pick(random, digits);
        return Digits(random, digits - zeros) * Ten(zeros);
    }

    // An exponent: small, anywhere in the range, one of the extremes, within 40 of either end of the range.
    private static int Exponent(PCG random) => Pick(random, 10) switch
    {
        0 or 1 or 2 => Pick(random, 81) - 40,
        3 => Pick(random, 5) - 2,
        4 or 5 => Pick(random, 12288) + ReferenceDecimal.MinExponent,
        6 => ExtremeExponents[Pick(random, ExtremeExponents.Length)],
        7 => ReferenceDecimal.MinExponent + Pick(random, 40),
        8 => ReferenceDecimal.MaxExponent - Pick(random, 40),
        _ => Pick(random, 21) - 10,
    };

    internal static RefDecimal Value(PCG random) =>
        Pick(random, 8) == 0
            ? Edges[Pick(random, Edges.Length)]
            : new RefDecimal(Pick(random, 2) == 0, Coefficient(random), Exponent(random));

    // A value of 28 to 34 digits within three of either end of the range, where the sums and the products overflow and vanish.
    private static RefDecimal AtTheEnd(PCG random)
    {
        int exponent = Pick(random, 2) == 0
            ? ReferenceDecimal.MaxExponent - Pick(random, 4)
            : ReferenceDecimal.MinExponent + Pick(random, 4);
        return new RefDecimal(Pick(random, 2) == 0, Digits(random, 28 + Pick(random, 7)), exponent);
    }

    // A value whose exponent is within four of the exponent of another.
    private static RefDecimal Neighbour(RefDecimal value, PCG random)
    {
        int exponent = Math.Clamp(value.Exponent + Pick(random, 9) - 4, ReferenceDecimal.MinExponent, ReferenceDecimal.MaxExponent);
        return new RefDecimal(Pick(random, 2) == 0, Coefficient(random), exponent);
    }

    // The same number with another cohort: the coefficient times a power of ten and the exponent lowered by the power, when it fits.
    private static RefDecimal Recohort(RefDecimal value, PCG random)
    {
        int shift = 1 + Pick(random, 6);
        BigInteger more = value.Coefficient * Ten(shift);
        return more <= ReferenceDecimal.MaxCoefficient && value.Exponent - shift >= ReferenceDecimal.MinExponent
            ? value with { Coefficient = more, Exponent = value.Exponent - shift }
            : value;
    }

    internal static RefPair Pair(PCG random)
    {
        RefDecimal first = Value(random);
        return Pick(random, 9) switch
        {
            0 => new RefPair(first, first),
            1 => new RefPair(first, ReferenceDecimal.Negate(first)),
            2 => new RefPair(first, Neighbour(first, random)),
            3 => new RefPair(AtTheEnd(random), AtTheEnd(random)),
            4 => FixedPairs[Pick(random, FixedPairs.Length)],
            5 => new RefPair(first, AtTheEnd(random)),
            6 => new RefPair(first, Recohort(first, random)),
            _ => new RefPair(first, Value(random)),
        };
    }

    // ASCII digits, the first one maybe zero; now and then all of them zeros.
    private static string DigitText(PCG random, int count)
    {
        bool zeros = Pick(random, 10) == 0;
        char[] digits = new char[count];
        for (int index = 0; index < count; index++)
        {
            digits[index] = zeros ? '0' : (char)('0' + Pick(random, 10));
        }

        return new string(digits);
    }

    private static string Sign(PCG random) => Pick(random, 3) switch
    {
        0 => string.Empty,
        1 => "+",
        _ => "-",
    };

    // The digits with a point somewhere (or none), as a text that the grammar may read.
    private static string WithPoint(string digits, int point) =>
        point < 0 ? digits : digits[..point] + "." + digits[point..];

    private static string ExponentText(PCG random) => Pick(random, 5) switch
    {
        2 => "e" + Sign(random) + Number(Pick(random, 41)),
        3 => "E" + Sign(random) + Number(Pick(random, 12288)),
        4 => "e" + Sign(random) + (Pick(random, 2) == 0 ? "99999999999" : "0000" + Number(Pick(random, 8000))),
        _ => string.Empty,
    };

    // A text of the grammar: a sign, 1 to 40 digits with leading zeros now and then, a point or none, and an exponent or none.
    private static string ValidText(PCG random)
    {
        int count = 1 + Pick(random, 40);
        string digits = DigitText(random, count);
        int point = Pick(random, 3) == 0 ? -1 : Pick(random, count + 1);
        return Sign(random) + WithPoint(digits, point) + ExponentText(random);
    }

    // A text whose exponent, after the rounding to 34 digits, is within four of an end of the range.
    private static string AtTheRangeText(PCG random)
    {
        int count = 1 + Pick(random, 40);
        string digits = DigitText(random, count);
        int point = Pick(random, count + 1);
        int target = (Pick(random, 2) == 0 ? ReferenceDecimal.MinExponent : ReferenceDecimal.MaxExponent) + Pick(random, 9) - 4;
        return Sign(random) + WithPoint(digits, point) + "e" + Number(target + (count - point));
    }

    // 34 digits (now and then nines) and a 5 with zeros after it, or with a 1 after the zeros: a tie, and just above a tie.
    private static string TieText(PCG random)
    {
        string head = Pick(random, 6) == 0 ? new string('9', ReferenceDecimal.Digits) : Digits(random, ReferenceDecimal.Digits).ToString(CultureInfo.InvariantCulture);
        string tail = Pick(random, 3) switch
        {
            0 => "5",
            1 => "5" + new string('0', 1 + Pick(random, 4)),
            _ => "5" + new string('0', Pick(random, 3)) + "1",
        };
        return Sign(random) + head + tail + (Pick(random, 2) == 0 ? string.Empty : "e" + Number(Pick(random, 21) - 10));
    }

    private static string Spoiled(string text, PCG random)
    {
        int position = Pick(random, text.Length + 1);
        return text[..position] + SpoilingCharacters[Pick(random, SpoilingCharacters.Length)] + text[position..];
    }

    internal static string Text(PCG random) => Pick(random, 12) switch
    {
        0 => InvalidTexts[Pick(random, InvalidTexts.Length)],
        1 => Spoiled(ValidText(random), random),
        2 => AtTheRangeText(random),
        3 => TieText(random),
        4 => "1" + new string('0', 33 + Pick(random, 8)),
        _ => ValidText(random),
    };

    // A value next to a boundary of an integer type or of System.Decimal: the boundary with one more or less, and a fraction that is
    // nothing, a half, all nines or random digits.
    private static RefDecimal Boundary(PCG random, BigInteger bound, int extraPlaces)
    {
        BigInteger edge = bound + Pick(random, 3) - 1;
        int places = Pick(random, 6);
        BigInteger tail = places == 0 ? BigInteger.Zero : Pick(random, 4) switch
        {
            0 => 5 * Ten(places - 1),
            1 => Ten(places) - 1,
            2 => BigInteger.One,
            _ => Digits(random, places),
        };
        return new RefDecimal(Pick(random, 2) == 0, (edge * Ten(places)) + tail, -(places + extraPlaces));
    }

    private static RefDecimal IntegerValue(PCG random)
    {
        BigInteger bound = Pick(random, 2) == 0 ? BigInteger.One << 63 : BigInteger.One << 64;
        return Pick(random, 8) switch
        {
            0 or 1 or 2 => new RefDecimal(Pick(random, 2) == 0, Coefficient(random), Pick(random, 46) - 40),
            3 or 4 or 5 => Boundary(random, bound, 0),
            6 => IntegerEdges[Pick(random, IntegerEdges.Length)],
            _ => Value(random),
        };
    }

    // A value whose magnitude is between 10^-400 and 10^320, so the double of it is normal, subnormal, zero or an infinity.
    private static RefDecimal Magnitude(PCG random)
    {
        BigInteger coefficient = Coefficient(random);
        int digits = Math.Max(ReferenceDecimal.DigitCount(coefficient), 1);
        return new RefDecimal(Pick(random, 2) == 0, coefficient, Pick(random, 721) - 400 - digits);
    }

    private static RefDecimal DoubleValue(PCG random) => Pick(random, 8) switch
    {
        0 or 1 or 2 or 3 => Magnitude(random),
        4 or 5 => DoubleDecimalEdges[Pick(random, DoubleDecimalEdges.Length)],
        6 => Value(random),
        _ => new RefDecimal(Pick(random, 2) == 0, Digits(random, 16 + Pick(random, 4)), Pick(random, 21) - 10),
    };

    private static RefDecimal SystemRangeValue(PCG random) => Pick(random, 8) switch
    {
        0 or 1 => new RefDecimal(Pick(random, 2) == 0, Coefficient(random), Pick(random, 46) - 40),
        2 or 3 => Boundary(random, (BigInteger.One << 96) - 1, Pick(random, 4)),
        4 => new RefDecimal(Pick(random, 2) == 0, Digits(random, 1 + Pick(random, 3)), Pick(random, 8) - 32),
        5 => new RefDecimal(Pick(random, 2) == 0, BigInteger.Zero, Pick(random, 39) - 35),
        6 => SystemRangeEdges[Pick(random, SystemRangeEdges.Length)],
        _ => Value(random),
    };

    private static long Long(PCG random) => Pick(random, 6) switch
    {
        0 => LongEdges[Pick(random, LongEdges.Length)],
        1 => Pick(random, 2001) - 1000,
        2 => unchecked((long)(Bits64(random) >> Pick(random, 64))),
        _ => unchecked((long)Bits64(random)),
    };

    private static ulong ULong(PCG random) => Pick(random, 6) switch
    {
        0 => ULongEdges[Pick(random, ULongEdges.Length)],
        1 => (ulong)Pick(random, 2001),
        2 => Bits64(random) >> Pick(random, 64),
        _ => Bits64(random),
    };

    private static double Double(PCG random) => Pick(random, 10) switch
    {
        0 => DoubleEdges[Pick(random, DoubleEdges.Length)],
        1 or 2 => Pick(random, 2_000_001) / Math.Pow(10, Pick(random, 9)),
        3 => Math.Pow(10, Pick(random, 50) - 25),
        4 => (Pick(random, 2001) - 1000) * 1000.0,
        _ => BitConverter.UInt64BitsToDouble(Bits64(random)),
    };

    private static int Part(PCG random) => Pick(random, 6) switch
    {
        0 => 0,
        1 => -1,
        _ => unchecked((int)random.Next(uint.MaxValue)),
    };

    private static decimal SystemDecimal(PCG random)
    {
        int scale = Pick(random, 4) == 0 ? 28 : Pick(random, 29);
        int high = Pick(random, 3) == 0 ? 0 : Part(random);
        return new decimal(Part(random), Part(random), high, Pick(random, 2) == 0, (byte)scale);
    }

    private static RefDecimal[] BuildDoubleDecimalEdges()
    {
        List<RefDecimal> edges =
        [
            Make(17976931348623157, 292), Make(17976931348623159, 292), Make(1, 309), Make(1, 6111, true), Make(1, -400), Make(1, -324),
            Make(49, -325), Make(0, 0), Make(0, 0, true), Make(1, 0), Make(1, 22), Make(5, -324), Make(1, -323),
        ];

        // The midpoint between the largest double and the next power of two: 2^1024 - 2^970.
        AddAround(edges, (BigInteger.One << 1024) - (BigInteger.One << 970), 0);

        // The half of the smallest subnormal value: 2^-1075 = 5^1075 / 10^1075.
        AddAround(edges, BigInteger.Pow(5, 1075), -1075);

        // The ties between two doubles that are above 2^53: (2^53 + 1) * 2^k is a half of an ulp above 2^(53 + k).
        foreach (int shift in TieShifts)
        {
            AddAround(edges, ((BigInteger.One << 53) + 1) << shift, 0);
        }

        return [.. edges];
    }

    // Adds the first 34 digits of an exact value, with one more and one less in the last digit, for both signs.
    private static void AddAround(List<RefDecimal> edges, BigInteger exact, int scale)
    {
        string digits = exact.ToString(CultureInfo.InvariantCulture);
        int kept = Math.Min(digits.Length, ReferenceDecimal.Digits);
        BigInteger head = Big(digits[..kept]);
        int exponent = digits.Length - kept + scale;
        for (int delta = -1; delta <= 1; delta++)
        {
            edges.Add(new RefDecimal(false, head + delta, exponent));
            edges.Add(new RefDecimal(true, head + delta, exponent));
        }
    }
}
#endif

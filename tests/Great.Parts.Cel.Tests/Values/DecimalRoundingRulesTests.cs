using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// round and quantize against an exact reference over BigInteger, on the target that the tests are built for: the rules of the library
// work on the parts of a value, and the reference works on the signed whole number that the parts stand for, so that each of the
// seven modes is defined on the signed quotient (floor and ceiling by the sign of the number, the half modes by the comparison of
// twice the remainder with the divisor) and not by the code of the library. The result is then fitted to the platform by the same
// rule in both: more digits than the platform holds are an overflow, and an exponent above the largest one is folded into the
// coefficient when that fits, with a zero at the largest exponent. The values are the edges of the digits (10^k - 1, 5 * 10^k and its
// neighbours up to 38 digits), of the precision of the platform (34 digits, or 2^96 - 1 and 29 digits), and of the exponents; the
// places are those around the digits of a coefficient, around the exponents of the platform, around int and long.
public sealed class DecimalRoundingRulesTests
{
    private const int MaxReferenceShift = 45;

#if NET11_0_OR_GREATER
    private const int MaxExponent = 6111;
    private static readonly BigInteger MaxCoefficient = BigInteger.Pow(10, 34) - 1;
    private static readonly int[] Exponents = [0, -1, -2, -5, -28, -33, -34, -6176, 1, 5, 6111];
#else
    private const int MaxExponent = 0;
    private static readonly BigInteger MaxCoefficient = (BigInteger.One << 96) - 1;
    private static readonly int[] Exponents = [0, -1, -2, -5, -14, -27, -28];
#endif

    private static readonly BigInteger[] Powers = CreatePowers();

    private static readonly BigInteger[] Coefficients = CreateCoefficients();

    private static readonly long[] Places =
    [
        long.MinValue, int.MinValue - 1L, int.MinValue, -1_000_001, -7000, -6145, -6112, -6111, -100, -39, -35, -34, -29, -28, -5, -2, -1,
        0, 1, 2, 5, 27, 28, 29, 33, 34, 35, 39, 100, 6176, 6177, int.MaxValue, int.MaxValue + 1L, long.MaxValue,
    ];

    private readonly record struct Outcome(bool Held, bool Negative, BigInteger Coefficient, int Exponent);

    [Theory]
    [InlineData("half_even")]
    [InlineData("half_up")]
    [InlineData("half_down")]
    [InlineData("down")]
    [InlineData("up")]
    [InlineData("floor")]
    [InlineData("ceiling")]
    public static void Round_AtEveryPlace_IsTheReferenceInEveryMode(string name)
    {
        Assert.True(DecimalRoundingRules.TryParseMode(name, out DecimalRoundingMode mode));
        var failures = new List<string>();
        int cases = 0;
        foreach (bool negative in new[] { false, true })
        {
            foreach (BigInteger coefficient in Coefficients)
            {
                foreach (int exponent in Exponents)
                {
                    foreach (long place in Places)
                    {
                        cases++;
                        Outcome expected = ReferenceRound(negative, coefficient, exponent, place, mode);
                        Outcome actual = ActualRound(negative, coefficient, exponent, place, mode);
                        if (expected != actual && failures.Count < 20)
                        {
                            failures.Add(Describe(name, negative, coefficient, exponent, "round(" + place.ToString(CultureInfo.InvariantCulture) + ")", expected, actual));
                        }
                    }
                }
            }
        }

        Assert.Empty(failures);
        Assert.True(cases > 50_000);
    }

    [Theory]
    [InlineData("half_even")]
    [InlineData("half_up")]
    [InlineData("half_down")]
    [InlineData("down")]
    [InlineData("up")]
    [InlineData("floor")]
    [InlineData("ceiling")]
    public static void Quantize_ToEveryExponent_IsTheReferenceInEveryMode(string name)
    {
        Assert.True(DecimalRoundingRules.TryParseMode(name, out DecimalRoundingMode mode));
        var failures = new List<string>();
        int cases = 0;
        foreach (bool negative in new[] { false, true })
        {
            foreach (BigInteger coefficient in Coefficients)
            {
                foreach (int exponent in Exponents)
                {
                    foreach (int quantum in Exponents)
                    {
                        cases++;
                        Outcome expected = ReferenceQuantize(negative, coefficient, exponent, quantum, mode);
                        Outcome actual = ActualQuantize(negative, coefficient, exponent, quantum, mode);
                        if (expected != actual && failures.Count < 20)
                        {
                            failures.Add(Describe(name, negative, coefficient, exponent, "quantize(e" + quantum.ToString(CultureInfo.InvariantCulture) + ")", expected, actual));
                        }
                    }
                }
            }
        }

        Assert.Empty(failures);
        Assert.True(cases > 10_000);
    }

    // The names are compared by their units, ordinally: case, a space, a prefix, a longer name and the other rounding modes of the
    // decimal specification are no names.
    [Theory]
    [InlineData("")]
    [InlineData("HALF_EVEN")]
    [InlineData("Half_Even")]
    [InlineData("half-even")]
    [InlineData("half_even ")]
    [InlineData(" up")]
    [InlineData("half")]
    [InlineData("banker")]
    [InlineData("round_05up")]
    [InlineData("half_up_xyz")]
    [InlineData("ceilingceiling")]
    [InlineData("ı")]
    public static void TryParseMode_OfAnythingElse_IsFalse(string name)
    {
        Assert.False(DecimalRoundingRules.TryParseMode(name, out _));
    }

    private static Outcome ActualRound(bool negative, BigInteger coefficient, int exponent, long place, DecimalRoundingMode mode)
    {
        DecimalParts value = DecimalPlatform.ToParts(DecimalPlatform.FromParts(new DecimalParts(negative, (UInt128)coefficient, exponent)));
        DecimalParts rounded = DecimalRoundingRules.Round(value, place, mode);
        return Finish(rounded, true);
    }

    private static Outcome ActualQuantize(bool negative, BigInteger coefficient, int exponent, int quantum, DecimalRoundingMode mode)
    {
        DecimalParts value = DecimalPlatform.ToParts(DecimalPlatform.FromParts(new DecimalParts(negative, (UInt128)coefficient, exponent)));
        return DecimalRoundingRules.TryQuantize(value, quantum, mode, out DecimalParts rounded) ? Finish(rounded, true) : Finish(rounded, false);
    }

    // The value that the platform holds for the parts, read back; no value when the platform refuses the parts.
    private static Outcome Finish(DecimalParts rounded, bool fits)
    {
        if (!fits || !DecimalPlatform.TryFromRounded(rounded, out var result))
        {
            return new Outcome(false, false, BigInteger.Zero, 0);
        }

        DecimalParts parts = DecimalPlatform.ToParts(result);
        return new Outcome(true, parts.Negative, (BigInteger)parts.Coefficient, parts.Exponent);
    }

    private static Outcome ReferenceRound(bool negative, BigInteger coefficient, int exponent, long place, DecimalRoundingMode mode) =>
        RoundAt(negative, coefficient, exponent, BigInteger.Max(exponent, -(BigInteger)place), mode);

    private static Outcome ReferenceQuantize(bool negative, BigInteger coefficient, int exponent, int quantum, DecimalRoundingMode mode)
    {
        if (quantum >= exponent)
        {
            return RoundAt(negative, coefficient, exponent, quantum, mode);
        }

        int shift = exponent - quantum;
        if (coefficient.IsZero)
        {
            return Fit(negative, coefficient, quantum);
        }

        // 10^46 is above the largest coefficient of every platform.
        return shift > MaxReferenceShift ? new Outcome(false, false, BigInteger.Zero, 0) : Fit(negative, coefficient * Powers[shift], quantum);
    }

    // The signed number divided by ten to the digits that go and rounded to a whole number by the mode.
    private static Outcome RoundAt(bool negative, BigInteger coefficient, int exponent, BigInteger target, DecimalRoundingMode mode)
    {
        if (target <= exponent)
        {
            return Fit(negative, coefficient, exponent);
        }

        // A coefficient has at most 39 digits, so a divisor of 60 digits and a larger one give the same quotient and remainder.
        BigInteger divisor = Powers[(int)BigInteger.Min(target - exponent, 60)];
        BigInteger rounded = RoundQuotient(negative ? -coefficient : coefficient, divisor, mode);
        return Fit(negative, BigInteger.Abs(rounded), target);
    }

    private static BigInteger RoundQuotient(BigInteger signed, BigInteger divisor, DecimalRoundingMode mode)
    {
        BigInteger truncated = BigInteger.DivRem(signed, divisor, out BigInteger remainder);
        if (remainder.IsZero)
        {
            return truncated;
        }

        BigInteger away = truncated + signed.Sign;
        BigInteger floor = signed.Sign > 0 ? truncated : away;
        BigInteger ceiling = signed.Sign > 0 ? away : truncated;
        int half = (BigInteger.Abs(remainder) * 2).CompareTo(divisor);
        return mode switch
        {
            DecimalRoundingMode.HalfUp => half >= 0 ? away : truncated,
            DecimalRoundingMode.HalfDown => half > 0 ? away : truncated,
            DecimalRoundingMode.HalfEven => half > 0 ? away : half < 0 ? truncated : truncated.IsEven ? truncated : away,
            DecimalRoundingMode.Down => truncated,
            DecimalRoundingMode.Up => away,
            DecimalRoundingMode.Floor => floor,
            _ => ceiling,
        };
    }

    // What the platform holds for the number: itself, or with the zeros of an exponent above the largest folded into the coefficient;
    // a zero has the largest exponent then; no value when the coefficient is too large.
    private static Outcome Fit(bool negative, BigInteger coefficient, BigInteger exponent)
    {
        if (exponent > MaxExponent)
        {
            if (coefficient.IsZero)
            {
                return new Outcome(true, negative, BigInteger.Zero, MaxExponent);
            }

            BigInteger shift = exponent - MaxExponent;
            if (shift > MaxReferenceShift)
            {
                return new Outcome(false, false, BigInteger.Zero, 0);
            }

            coefficient *= Powers[(int)shift];
            exponent = MaxExponent;
        }

        return coefficient > MaxCoefficient ? new Outcome(false, false, BigInteger.Zero, 0) : new Outcome(true, negative, coefficient, (int)exponent);
    }

    private static string Describe(string mode, bool negative, BigInteger coefficient, int exponent, string call, Outcome expected, Outcome actual) =>
        mode + " " + (negative ? "-" : "+") + coefficient.ToString(CultureInfo.InvariantCulture) + "e" + exponent.ToString(CultureInfo.InvariantCulture)
        + " " + call + ": expected " + expected + ", actual " + actual;

    private static BigInteger[] CreatePowers()
    {
        var powers = new BigInteger[61];
        powers[0] = BigInteger.One;
        for (int index = 1; index < powers.Length; index++)
        {
            powers[index] = powers[index - 1] * 10;
        }

        return powers;
    }

    // The coefficients that the platform holds: small ones with their ties, the edges of the digits and the largest.
    private static BigInteger[] CreateCoefficients()
    {
        var values = new SortedSet<BigInteger>();
        foreach (int small in new[] { 0, 1, 2, 3, 4, 5, 6, 9, 10, 11, 15, 24, 25, 26, 49, 50, 51, 95, 99, 100, 101, 125, 150, 995, 1234, 1245, 1250, 1255, 9999 })
        {
            values.Add(small);
        }

        for (int digits = 1; digits <= 38; digits++)
        {
            BigInteger power = Powers[digits];
            values.Add(power - 1);
            values.Add(power);
            values.Add(5 * power);
            values.Add((5 * power) - 1);
            values.Add((5 * power) + 1);
        }

        values.Add(MaxCoefficient - 1);
        values.Add(MaxCoefficient);
        values.Add(MaxCoefficient / 2);
        values.Add((MaxCoefficient / 2) + 1);
        values.RemoveWhere(static value => value > MaxCoefficient);
        values.RemoveWhere(static value => value < 0);
        return [.. values];
    }
}

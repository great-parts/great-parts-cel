using System;

namespace Great.Parts.Cel.Values;

// The rounding modes of round and quantize, named as the set decimal names them, in lower case with an underscore.
internal enum DecimalRoundingMode : byte
{
    // To the nearest, a tie to the even digit; the default.
    HalfEven,

    // To the nearest, a tie away from zero.
    HalfUp,

    // To the nearest, a tie toward zero.
    HalfDown,

    // Toward zero.
    Down,

    // Away from zero.
    Up,

    // Toward the lesser value.
    Floor,

    // Toward the greater value.
    Ceiling,
}

// round and quantize of the set decimal, version 2, over the parts of a decimal: the sign, the coefficient and the exponent. The
// rules do not call the arithmetic of the platform, so one code serves both targets; the result is checked against the precision of
// the platform by DecimalPlatform.TryFromRounded. Digits that go are cut by one division by a power of ten, never by a loop over the
// digits.
internal static class DecimalRoundingRules
{
    // The length of the longest mode, half_even and half_down: a longer string is no mode and is not compared.
    private const int LongestMode = 9;

    // A target exponent above this is the same as this one: the digits that go are more than a coefficient has, so the kept part is
    // zero or one, and the exponent of a nonzero result is above what any platform holds.
    private const int FarExponent = 1_000_000;

    // How the digits that go compare with half of the unit of the kept place.
    private enum Rest
    {
        None,
        BelowHalf,
        Half,
        AboveHalf,
    }

    // The mode of its name; false for a name that is not one of the seven, a string longer than nine units included.
    internal static bool TryParseMode(string name, out DecimalRoundingMode mode)
    {
        mode = DecimalRoundingMode.HalfEven;
        if (name.Length > LongestMode)
        {
            return false;
        }

        switch (name)
        {
            case "half_even":
                return true;
            case "half_up":
                mode = DecimalRoundingMode.HalfUp;
                return true;
            case "half_down":
                mode = DecimalRoundingMode.HalfDown;
                return true;
            case "down":
                mode = DecimalRoundingMode.Down;
                return true;
            case "up":
                mode = DecimalRoundingMode.Up;
                return true;
            case "floor":
                mode = DecimalRoundingMode.Floor;
                return true;
            case "ceiling":
                mode = DecimalRoundingMode.Ceiling;
                return true;
            default:
                return false;
        }
    }

    // round(n): the value to n digits after the point, n below zero to tens, hundreds and so on. No zeros are added: when the place is
    // at or below the exponent of the value, the value is returned as it is. A place beyond the range of int is the same as a place far
    // above, since every digit goes.
    internal static DecimalParts Round(DecimalParts value, long digits, DecimalRoundingMode mode)
    {
        if (digits >= -(long)value.Exponent)
        {
            return value;
        }

        return Drop(value, digits < -FarExponent ? FarExponent : (int)-digits, mode);
    }

    // quantize(q): the value with the exponent of q. Digits that go are rounded by the mode; a smaller exponent multiplies the
    // coefficient by ten to the difference, which is exact; false when it does not fit 128 bits, and the platform check then has
    // no number to refuse.
    internal static bool TryQuantize(DecimalParts value, int exponent, DecimalRoundingMode mode, out DecimalParts result)
    {
        if (exponent > value.Exponent)
        {
            result = Drop(value, exponent, mode);
            return true;
        }

        result = value with { Exponent = exponent };
        int shift = value.Exponent - exponent;
        if (shift == 0 || value.Coefficient == UInt128.Zero)
        {
            return true;
        }

        if (shift >= DecimalParts.Powers.Length || value.Coefficient > UInt128.MaxValue / DecimalParts.Powers[shift])
        {
            return false;
        }

        result = result with { Coefficient = value.Coefficient * DecimalParts.Powers[shift] };
        return true;
    }

    // The value at a target exponent above its own: the coefficient without its last digits, one more when the mode says so by the
    // digits that went, the sign and the last digit kept. A zero keeps its sign.
    private static DecimalParts Drop(DecimalParts value, int target, DecimalRoundingMode mode)
    {
        int count = target - value.Exponent;
        UInt128 kept = UInt128.Zero;
        Rest rest;
        if (count >= DecimalParts.Powers.Length)
        {
            // A coefficient has at most 34 digits, so it is below half of ten to 39 or more.
            rest = value.Coefficient == UInt128.Zero ? Rest.None : Rest.BelowHalf;
        }
        else
        {
            UInt128 power = DecimalParts.Powers[count];
            (kept, UInt128 gone) = UInt128.DivRem(value.Coefficient, power);
            UInt128 half = power >> 1;
            rest = gone == UInt128.Zero ? Rest.None : gone < half ? Rest.BelowHalf : gone == half ? Rest.Half : Rest.AboveHalf;
        }

        bool increment = mode switch
        {
            DecimalRoundingMode.HalfEven => rest == Rest.AboveHalf || (rest == Rest.Half && (kept & UInt128.One) != UInt128.Zero),
            DecimalRoundingMode.HalfUp => rest >= Rest.Half,
            DecimalRoundingMode.HalfDown => rest == Rest.AboveHalf,
            DecimalRoundingMode.Down => false,
            DecimalRoundingMode.Up => rest != Rest.None,
            DecimalRoundingMode.Floor => rest != Rest.None && value.Negative,
            _ => rest != Rest.None && !value.Negative,
        };

        return new DecimalParts(value.Negative, increment ? kept + UInt128.One : kept, target);
    }
}

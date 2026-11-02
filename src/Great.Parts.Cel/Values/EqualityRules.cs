using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// CEL equality of map keys: <c>int</c>, <c>uint</c> and <c>double</c> compare on one number line,
/// strings by their UTF-16 code units, and values of different non-numeric kinds are not equal.
/// </summary>
internal static class EqualityRules
{
    /// <summary>2^63 as a double, exact: the first double above the range of <c>int</c>.</summary>
    internal const double TwoToThe63 = 9223372036854775808.0;

    /// <summary>2^64 as a double, exact: the first double above the range of <c>uint</c>.</summary>
    internal const double TwoToThe64 = 18446744073709551616.0;

    /// <summary>
    /// Tells whether two values are the same map key by CEL equality.
    /// </summary>
    /// <param name="left">A key or a probe of any kind.</param>
    /// <param name="right">A key or a probe of any kind.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    internal static bool KeysEqual(CelValue left, CelValue right)
    {
        if (left.TryGetString(out string? leftString))
        {
            return right.TryGetString(out string? rightString) && string.Equals(leftString, rightString, StringComparison.Ordinal);
        }

        if (left.TryGetBoolean(out bool leftBool))
        {
            return right.TryGetBoolean(out bool rightBool) && leftBool == rightBool;
        }

        if (left.TryGetInt64(out long leftInt))
        {
            return IntEquals(leftInt, right);
        }

        if (left.TryGetUInt64(out ulong leftUInt))
        {
            return UIntEquals(leftUInt, right);
        }

        if (left.TryGetDouble(out double leftDouble))
        {
            return right.TryGetDouble(out double rightDouble) ? leftDouble == rightDouble : NumberEquals(right, leftDouble);
        }

        return false;
    }

    // An int equals an int, a uint or a double with the same value.
    private static bool IntEquals(long left, CelValue right)
    {
        if (right.TryGetInt64(out long rightInt))
        {
            return left == rightInt;
        }

        if (right.TryGetUInt64(out ulong rightUInt))
        {
            return left >= 0 && (ulong)left == rightUInt;
        }

        return right.TryGetDouble(out double rightDouble) && DoubleEqualsInt(rightDouble, left);
    }

    // A uint equals an int, a uint or a double with the same value.
    private static bool UIntEquals(ulong left, CelValue right)
    {
        if (right.TryGetUInt64(out ulong rightUInt))
        {
            return left == rightUInt;
        }

        if (right.TryGetInt64(out long rightInt))
        {
            return rightInt >= 0 && (ulong)rightInt == left;
        }

        return right.TryGetDouble(out double rightDouble) && DoubleEqualsUInt(rightDouble, left);
    }

    // A double equals an int or a uint with the same value.
    private static bool NumberEquals(CelValue integer, double number)
    {
        if (integer.TryGetInt64(out long intValue))
        {
            return DoubleEqualsInt(number, intValue);
        }

        return integer.TryGetUInt64(out ulong uintValue) && DoubleEqualsUInt(number, uintValue);
    }

    // Exact: the double is integral and in the range of long, so the conversion loses nothing. NaN fails every comparison.
    private static bool DoubleEqualsInt(double number, long integer) =>
        number >= -TwoToThe63 && number < TwoToThe63 && Math.Floor(number) == number && (long)number == integer;

    // Exact: the double is integral and in the range of ulong, so the conversion loses nothing. NaN fails every comparison.
    private static bool DoubleEqualsUInt(double number, ulong integer) =>
        number >= 0 && number < TwoToThe64 && Math.Floor(number) == number && (ulong)number == integer;
}

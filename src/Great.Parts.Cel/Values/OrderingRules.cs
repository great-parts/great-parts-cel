using System;
#if NET11_0_OR_GREATER
using System.Numerics;
#endif

namespace Great.Parts.Cel.Values;

/// <summary>
/// CEL ordering: <c>bool</c> with <c>false</c> first; <c>int</c>, <c>uint</c> and <c>double</c> on one number line,
/// as cel-go v0.32.0 <c>common/types/compare.go</c> compares them; strings by code points; bytes by unsigned byte
/// values with a prefix first.
/// </summary>
internal static class OrderingRules
{
    private const double TwoToThe63 = EqualityRules.TwoToThe63;
    private const double TwoToThe64 = EqualityRules.TwoToThe64;

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The ordering; <see cref="ValueOrdering.None"/> for kinds without an ordering between them.</returns>
    internal static ValueOrdering Compare(CelValue left, CelValue right)
    {
        if (IsNumber(left.Kind) && IsNumber(right.Kind))
        {
            return CompareNumbers(left, right);
        }

        if (left.TryGetBoolean(out bool leftBool) && right.TryGetBoolean(out bool rightBool))
        {
            return Sign(leftBool.CompareTo(rightBool));
        }

        if (left.TryGetString(out string? leftString) && right.TryGetString(out string? rightString))
        {
            return CompareStrings(leftString, rightString);
        }

        if (left.TryGetBytes(out ReadOnlyMemory<byte> leftBytes) && right.TryGetBytes(out ReadOnlyMemory<byte> rightBytes))
        {
            return Sign(leftBytes.Span.SequenceCompareTo(rightBytes.Span));
        }

        return CompareOther(left, right);
    }

    // Timestamps, durations and decimals.
    private static ValueOrdering CompareOther(CelValue left, CelValue right)
    {
        if (left.TryGetTimestamp(out CelTimestamp leftTimestamp) && right.TryGetTimestamp(out CelTimestamp rightTimestamp))
        {
            return Sign(leftTimestamp.CompareTo(rightTimestamp));
        }

        if (left.TryGetDuration(out CelDuration leftDuration) && right.TryGetDuration(out CelDuration rightDuration))
        {
            return Sign(leftDuration.CompareTo(rightDuration));
        }

#if NET11_0_OR_GREATER
        if (left.TryGetDecimal(out Decimal128 leftDecimal) && right.TryGetDecimal(out Decimal128 rightDecimal))
        {
            // By value: two cohorts of one number, and 0 and -0, are equal.
            return leftDecimal < rightDecimal ? ValueOrdering.Less : leftDecimal > rightDecimal ? ValueOrdering.Greater : ValueOrdering.Equal;
        }
#endif

        return ValueOrdering.None;
    }

    /// <summary>
    /// Tells whether a value of <paramref name="kind"/> is a number of one of the three kinds that share the number line.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for <c>int</c>, <c>uint</c> and <c>double</c>.</returns>
    internal static bool IsNumber(CelValueKind kind) => kind is CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double;

    /// <summary>
    /// Compares two numbers of any of the three kinds: <c>int</c> with <c>uint</c> exactly, an integer with a
    /// <c>double</c> as the integer rounded to the nearest <c>double</c> inside the range of the integer kind.
    /// </summary>
    /// <param name="left">A number.</param>
    /// <param name="right">A number.</param>
    /// <returns>The ordering; <see cref="ValueOrdering.Unordered"/> when either is a NaN.</returns>
    internal static ValueOrdering CompareNumbers(CelValue left, CelValue right)
    {
        if (left.TryGetInt64(out long leftInt))
        {
            if (right.TryGetInt64(out long rightInt))
            {
                return Sign(leftInt.CompareTo(rightInt));
            }

            return right.TryGetUInt64(out ulong rightUInt) ? IntWithUInt(leftInt, rightUInt) : IntWithDouble(leftInt, Double(right));
        }

        if (left.TryGetUInt64(out ulong leftUInt))
        {
            if (right.TryGetUInt64(out ulong rightUInt))
            {
                return Sign(leftUInt.CompareTo(rightUInt));
            }

            return right.TryGetInt64(out long rightInt) ? Reverse(IntWithUInt(rightInt, leftUInt)) : UIntWithDouble(leftUInt, Double(right));
        }

        double leftDouble = Double(left);
        if (right.TryGetInt64(out long otherInt))
        {
            return Reverse(IntWithDouble(otherInt, leftDouble));
        }

        return right.TryGetUInt64(out ulong otherUInt) ? Reverse(UIntWithDouble(otherUInt, leftDouble)) : Doubles(leftDouble, Double(right));
    }

    /// <summary>
    /// Compares two strings by code points: by UTF-16 code units up to the first difference, where two units at or
    /// above U+D800 are first mapped so that surrogates come after U+E000–U+FFFF (Unicode, section 5.17).
    /// </summary>
    /// <param name="left">The left string.</param>
    /// <param name="right">The right string.</param>
    /// <returns>The ordering.</returns>
    internal static ValueOrdering CompareStrings(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        int common = left.CommonPrefixLength(right);
        if (common == left.Length || common == right.Length)
        {
            return Sign(left.Length.CompareTo(right.Length));
        }

        int leftUnit = left[common];
        int rightUnit = right[common];
        if (leftUnit >= 0xD800 && rightUnit >= 0xD800)
        {
            leftUnit = CodePointOrder(leftUnit);
            rightUnit = CodePointOrder(rightUnit);
        }

        return leftUnit < rightUnit ? ValueOrdering.Less : ValueOrdering.Greater;
    }

    // U+E000–U+FFFF move below the surrogates, which move above them; the order among each group is kept.
    private static int CodePointOrder(int unit) => unit >= 0xE000 ? unit - 0x800 : unit + 0x2000;

    private static ValueOrdering IntWithUInt(long left, ulong right) =>
        left < 0 ? ValueOrdering.Less : Sign(((ulong)left).CompareTo(right));

    private static ValueOrdering IntWithDouble(long left, double right)
    {
        if (double.IsNaN(right))
        {
            return ValueOrdering.Unordered;
        }

        if (right < -TwoToThe63)
        {
            return ValueOrdering.Greater;
        }

        return right > TwoToThe63 ? ValueOrdering.Less : Doubles(left, right);
    }

    private static ValueOrdering UIntWithDouble(ulong left, double right)
    {
        if (double.IsNaN(right))
        {
            return ValueOrdering.Unordered;
        }

        if (right < 0)
        {
            return ValueOrdering.Greater;
        }

        return right > TwoToThe64 ? ValueOrdering.Less : Doubles(left, right);
    }

    private static ValueOrdering Doubles(double left, double right)
    {
        if (left < right)
        {
            return ValueOrdering.Less;
        }

        if (left > right)
        {
            return ValueOrdering.Greater;
        }

        return left == right ? ValueOrdering.Equal : ValueOrdering.Unordered;
    }

    private static double Double(CelValue value)
    {
        _ = value.TryGetDouble(out double number);
        return number;
    }

    private static ValueOrdering Sign(int comparison) => comparison switch
    {
        < 0 => ValueOrdering.Less,
        > 0 => ValueOrdering.Greater,
        _ => ValueOrdering.Equal,
    };

    private static ValueOrdering Reverse(ValueOrdering ordering) => ordering switch
    {
        ValueOrdering.Less => ValueOrdering.Greater,
        ValueOrdering.Greater => ValueOrdering.Less,
        _ => ordering,
    };
}

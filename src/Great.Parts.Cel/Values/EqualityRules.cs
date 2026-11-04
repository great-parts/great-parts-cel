using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// CEL equality by the language definition, "Equality": <c>int</c>, <c>uint</c> and <c>double</c> on one number line
/// by <see cref="OrderingRules"/>, values of different non-numeric kinds unequal without an error, strings by UTF-16
/// code units, bytes by content, type values by kind and name, lists element by element and maps by the key lookup.
/// And the equality of map keys, exact across the numeric kinds.
/// </summary>
internal static class EqualityRules
{
    /// <summary>2^63 as a double, exact: the first double above the range of <c>int</c>.</summary>
    internal const double TwoToThe63 = 9223372036854775808.0;

    /// <summary>2^64 as a double, exact: the first double above the range of <c>uint</c>.</summary>
    internal const double TwoToThe64 = 18446744073709551616.0;

    /// <summary>The message of <c>InsufficientStack</c> when values are nested too deeply to compare.</summary>
    internal const string InsufficientStackMessage = "insufficient execution stack to continue comparing values";

    /// <summary>
    /// Compares two values by CEL equality, charging the cost of each pair before comparing it: 1 for scalars and for
    /// values of different kinds, 1 + ⌈(La + Lb)/10⌉ for strings and bytes, 1 for two lists or maps and then their
    /// elements, and for each key of the left map its lookup in the right one.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="error">The error value when the result is <see cref="ValueEquality.Error"/>; otherwise <c>null</c>.</param>
    /// <returns>The result.</returns>
    internal static ValueEquality AreEqual(CelValue left, CelValue right, IEvaluationMeter? meter, out CelValue error)
    {
        error = default;
        if (left.Kind == CelValueKind.Error || right.Kind == CelValueKind.Error)
        {
            error = left.Kind == CelValueKind.Error ? left : right;
            return ValueEquality.Error;
        }

        if (OrderingRules.IsNumber(left.Kind) && OrderingRules.IsNumber(right.Kind))
        {
            meter?.Charge(1);
            return Result(OrderingRules.CompareNumbers(left, right) == ValueOrdering.Equal);
        }

        if (left.Kind != right.Kind)
        {
            meter?.Charge(1);
            return ValueEquality.NotEqual;
        }

        return SameKind(left, right, meter, out error);
    }

    // Two values of one non-numeric kind.
    private static ValueEquality SameKind(CelValue left, CelValue right, IEvaluationMeter? meter, out CelValue error)
    {
        error = default;
        switch (left.Kind)
        {
            case CelValueKind.Null:
                meter?.Charge(1);
                return ValueEquality.Equal;
            case CelValueKind.Bool:
                meter?.Charge(1);
                _ = left.TryGetBoolean(out bool leftBool);
                _ = right.TryGetBoolean(out bool rightBool);
                return Result(leftBool == rightBool);
            case CelValueKind.String:
                _ = left.TryGetString(out string? leftString);
                _ = right.TryGetString(out string? rightString);
                meter?.Charge(LengthCost(leftString!.Length, rightString!.Length));
                return Result(string.Equals(leftString, rightString, StringComparison.Ordinal));
            case CelValueKind.Bytes:
                _ = left.TryGetBytes(out ReadOnlyMemory<byte> leftBytes);
                _ = right.TryGetBytes(out ReadOnlyMemory<byte> rightBytes);
                meter?.Charge(LengthCost(leftBytes.Length, rightBytes.Length));
                return Result(leftBytes.Span.SequenceEqual(rightBytes.Span));
            case CelValueKind.Type:
                meter?.Charge(1);
                _ = left.TryGetType(out CelType? leftType);
                _ = right.TryGetType(out CelType? rightType);
                return Result(leftType!.Kind == rightType!.Kind && string.Equals(leftType.Name, rightType.Name, StringComparison.Ordinal));
            case CelValueKind.List:
                _ = left.TryGetList(out CelList? leftList);
                _ = right.TryGetList(out CelList? rightList);
                return ListsEqual(leftList!, rightList!, meter, out error);
            case CelValueKind.Map:
                _ = left.TryGetMap(out CelMap? leftMap);
                _ = right.TryGetMap(out CelMap? rightMap);
                return MapsEqual(leftMap!, rightMap!, meter, out error);
            default:
                // Time, optional values, objects and decimals come with their parts.
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Tells whether two values are the same map key by the key lookup: exact across <c>int</c> and <c>uint</c>, and
    /// a <c>double</c> probe equals the integer it converts to without loss — to <c>int</c> inside the open range of
    /// <c>int</c>, otherwise to <c>uint</c> in [0, 2^64).
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

    /// <summary>
    /// Tells whether a <c>double</c> has an integral value inside the open range of <c>int</c>, so that it converts
    /// to <c>int</c> without loss.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the conversion loses nothing; <see langword="false"/> for a NaN.</returns>
    internal static bool IsLosslessInt(double number) => number > -TwoToThe63 && number < TwoToThe63 && Math.Floor(number) == number;

    /// <summary>
    /// Tells whether a <c>double</c> has an integral value in [0, 2^64), so that it converts to <c>uint</c> without loss.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the conversion loses nothing; <see langword="false"/> for a NaN.</returns>
    internal static bool IsLosslessUInt(double number) => number >= 0 && number < TwoToThe64 && Math.Floor(number) == number;

    private static ValueEquality Result(bool equal) => equal ? ValueEquality.Equal : ValueEquality.NotEqual;

    // 1 + ⌈(a + b)/10⌉ for two lengths in code units or bytes.
    private static ulong LengthCost(int a, int b) => 1 + (((ulong)a + (ulong)b + 9) / 10);

    // The same count, then the elements pairwise up to the first unequal pair.
    private static ValueEquality ListsEqual(CelList left, CelList right, IEvaluationMeter? meter, out CelValue error)
    {
        error = default;
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return ValueEquality.InsufficientStack;
        }

        meter?.Charge(1);
        int count = left.Count;
        if (count != right.Count)
        {
            return ValueEquality.NotEqual;
        }

        for (int index = 0; index < count; index++)
        {
            ValueEquality element = AreEqual(left[index], right[index], meter, out error);
            if (element != ValueEquality.Equal)
            {
                return element;
            }
        }

        return ValueEquality.Equal;
    }

    // The same count, then each key of the left map in its order: its lookup in the right map and the two values.
    private static ValueEquality MapsEqual(CelMap left, CelMap right, IEvaluationMeter? meter, out CelValue error)
    {
        error = default;
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return ValueEquality.InsufficientStack;
        }

        meter?.Charge(1);
        if (left.Count != right.Count)
        {
            return ValueEquality.NotEqual;
        }

        foreach (KeyValuePair<CelValue, CelValue> entry in left)
        {
            // The lookup of a string key of length L costs 1 + ⌈L/10⌉, of any other key 1.
            meter?.Charge(entry.Key.TryGetString(out string? key) ? 1 + (((ulong)key.Length + 9) / 10) : 1);
            if (!right.TryGetValue(entry.Key, out CelValue other))
            {
                return ValueEquality.NotEqual;
            }

            ValueEquality value = AreEqual(entry.Value, other, meter, out error);
            if (value != ValueEquality.Equal)
            {
                return value;
            }
        }

        return ValueEquality.Equal;
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

    // The conversions lose nothing, so the integers compare exactly; a NaN fails every comparison.
    private static bool DoubleEqualsInt(double number, long integer) => IsLosslessInt(number) && (long)number == integer;

    private static bool DoubleEqualsUInt(double number, ulong integer) => IsLosslessUInt(number) && (ulong)number == integer;
}

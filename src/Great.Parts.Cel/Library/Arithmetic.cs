using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// The arithmetic of int, uint and double. Overflow of the integers is detected without exceptions: by the
// signs of the operands and the result, and by the high half of the 128-bit product. double follows
// IEEE 754 without errors. Each implementation rejects a value of another kind.
internal static class Arithmetic
{
    private const string IntegerOverflow = "integer overflow";
    private const string UnsignedOverflow = "unsigned integer overflow";
    private const string DivisionByZero = "division by zero";
    private const string ModulusByZero = "modulus by zero";

    // add_int64, (int, int) -> int.
    internal static CelValue AddInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetInts(arguments, out long left, out long right))
        {
            return site.NoMatchingOverload(arguments);
        }

        long sum = unchecked(left + right);

        // The sum overflows when both operands have the same sign and the sum has the other one.
        return ((left ^ sum) & (right ^ sum)) < 0 ? site.Error(CelErrorCode.Overflow, IntegerOverflow) : CelValue.FromInt64(sum);
    }

    // subtract_int64, (int, int) -> int.
    internal static CelValue SubtractInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetInts(arguments, out long left, out long right))
        {
            return site.NoMatchingOverload(arguments);
        }

        long difference = unchecked(left - right);

        // The difference overflows when the operands have different signs and the difference has the sign of
        // the subtrahend.
        return ((left ^ right) & (left ^ difference)) < 0
            ? site.Error(CelErrorCode.Overflow, IntegerOverflow)
            : CelValue.FromInt64(difference);
    }

    // multiply_int64, (int, int) -> int.
    internal static CelValue MultiplyInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetInts(arguments, out long left, out long right))
        {
            return site.NoMatchingOverload(arguments);
        }

        // The product fits when its high half is the sign extension of its low half.
        long high = Math.BigMul(left, right, out long low);
        return high != (low >> 63) ? site.Error(CelErrorCode.Overflow, IntegerOverflow) : CelValue.FromInt64(low);
    }

    // divide_int64, (int, int) -> int: integer division truncated toward zero.
    internal static CelValue DivideInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetInts(arguments, out long dividend, out long divisor))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (divisor == 0)
        {
            return site.Error(CelErrorCode.DivisionByZero, DivisionByZero);
        }

        // The only quotient outside the range of int is 2^63; the .NET operator would throw.
        return dividend == long.MinValue && divisor == -1
            ? site.Error(CelErrorCode.Overflow, IntegerOverflow)
            : CelValue.FromInt64(dividend / divisor);
    }

    // modulo_int64, (int, int) -> int: the remainder with the sign of the dividend.
    internal static CelValue ModuloInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetInts(arguments, out long dividend, out long divisor))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (divisor == 0)
        {
            return site.Error(CelErrorCode.ModulusByZero, ModulusByZero);
        }

        // -2^63 % -1 belongs to the quotient 2^63, outside the range of int; the .NET operator would throw.
        return dividend == long.MinValue && divisor == -1
            ? site.Error(CelErrorCode.Overflow, IntegerOverflow)
            : CelValue.FromInt64(dividend % divisor);
    }

    // negate_int64, (int) -> int.
    internal static CelValue NegateInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long operand))
        {
            return site.NoMatchingOverload(arguments);
        }

        return operand == long.MinValue ? site.Error(CelErrorCode.Overflow, IntegerOverflow) : CelValue.FromInt64(-operand);
    }

    // add_uint64, (uint, uint) -> uint.
    internal static CelValue AddUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetUInts(arguments, out ulong left, out ulong right))
        {
            return site.NoMatchingOverload(arguments);
        }

        ulong sum = unchecked(left + right);
        return sum < left ? site.Error(CelErrorCode.Overflow, UnsignedOverflow) : CelValue.FromUInt64(sum);
    }

    // subtract_uint64, (uint, uint) -> uint: a negative difference is an overflow.
    internal static CelValue SubtractUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetUInts(arguments, out ulong left, out ulong right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return right > left ? site.Error(CelErrorCode.Overflow, UnsignedOverflow) : CelValue.FromUInt64(left - right);
    }

    // multiply_uint64, (uint, uint) -> uint.
    internal static CelValue MultiplyUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetUInts(arguments, out ulong left, out ulong right))
        {
            return site.NoMatchingOverload(arguments);
        }

        ulong high = Math.BigMul(left, right, out ulong low);
        return high != 0 ? site.Error(CelErrorCode.Overflow, UnsignedOverflow) : CelValue.FromUInt64(low);
    }

    // divide_uint64, (uint, uint) -> uint.
    internal static CelValue DivideUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetUInts(arguments, out ulong dividend, out ulong divisor))
        {
            return site.NoMatchingOverload(arguments);
        }

        return divisor == 0 ? site.Error(CelErrorCode.DivisionByZero, DivisionByZero) : CelValue.FromUInt64(dividend / divisor);
    }

    // modulo_uint64, (uint, uint) -> uint.
    internal static CelValue ModuloUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetUInts(arguments, out ulong dividend, out ulong divisor))
        {
            return site.NoMatchingOverload(arguments);
        }

        return divisor == 0 ? site.Error(CelErrorCode.ModulusByZero, ModulusByZero) : CelValue.FromUInt64(dividend % divisor);
    }

    // add_double, (double, double) -> double.
    internal static CelValue AddDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetDoubles(arguments, out double left, out double right) ? CelValue.FromDouble(left + right) : site.NoMatchingOverload(arguments);

    // subtract_double, (double, double) -> double.
    internal static CelValue SubtractDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetDoubles(arguments, out double left, out double right) ? CelValue.FromDouble(left - right) : site.NoMatchingOverload(arguments);

    // multiply_double, (double, double) -> double.
    internal static CelValue MultiplyDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetDoubles(arguments, out double left, out double right) ? CelValue.FromDouble(left * right) : site.NoMatchingOverload(arguments);

    // divide_double, (double, double) -> double: a division by zero is an infinity or a NaN.
    internal static CelValue DivideDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetDoubles(arguments, out double left, out double right) ? CelValue.FromDouble(left / right) : site.NoMatchingOverload(arguments);

    // negate_double, (double) -> double.
    internal static CelValue NegateDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double operand) ? CelValue.FromDouble(-operand) : site.NoMatchingOverload(arguments);

    private static bool TryGetInts(ReadOnlySpan<CelValue> arguments, out long left, out long right)
    {
        right = 0;
        return arguments[0].TryGetInt64(out left) && arguments[1].TryGetInt64(out right);
    }

    private static bool TryGetUInts(ReadOnlySpan<CelValue> arguments, out ulong left, out ulong right)
    {
        right = 0;
        return arguments[0].TryGetUInt64(out left) && arguments[1].TryGetUInt64(out right);
    }

    private static bool TryGetDoubles(ReadOnlySpan<CelValue> arguments, out double left, out double right)
    {
        right = 0;
        return arguments[0].TryGetDouble(out left) && arguments[1].TryGetDouble(out right);
    }
}

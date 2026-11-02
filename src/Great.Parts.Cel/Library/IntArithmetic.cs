using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// The overloads of the arithmetic operators for int. Overflow is detected without exceptions.
internal static class IntArithmetic
{
    private const string OverflowMessage = "integer overflow";

    // add_int64, (int, int) -> int.
    internal static CelValue Add(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long left) || !arguments[1].TryGetInt64(out long right))
        {
            return site.NoMatchingOverload(arguments);
        }

        long sum = unchecked(left + right);

        // The sum overflows when both operands have the same sign and the sum has the other one.
        return ((left ^ sum) & (right ^ sum)) < 0
            ? site.Error(CelErrorCode.Overflow, OverflowMessage)
            : CelValue.FromInt64(sum);
    }

    // divide_int64, (int, int) -> int: integer division truncated toward zero.
    internal static CelValue Divide(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long dividend) || !arguments[1].TryGetInt64(out long divisor))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (divisor == 0)
        {
            return site.Error(CelErrorCode.DivisionByZero, "division by zero");
        }

        // The only quotient outside the range of int is 2^63.
        if (dividend == long.MinValue && divisor == -1)
        {
            return site.Error(CelErrorCode.Overflow, OverflowMessage);
        }

        return CelValue.FromInt64(dividend / divisor);
    }

    // negate_int64, (int) -> int.
    internal static CelValue Negate(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long operand))
        {
            return site.NoMatchingOverload(arguments);
        }

        return operand == long.MinValue
            ? site.Error(CelErrorCode.Overflow, OverflowMessage)
            : CelValue.FromInt64(-operand);
    }
}

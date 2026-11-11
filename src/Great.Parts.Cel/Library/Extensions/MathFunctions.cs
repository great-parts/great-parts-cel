using System;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The functions of the extension set math, version 3. One implementation serves every overload of a function that differs only
// in the kinds of its numbers, as the overloads of cel-go share one: the binding that the planner chose has checked the kinds at
// the top level, and each implementation tests the kinds it needs again and answers EvalNoMatchingOverload for another, so a call
// that bypasses type checking is safe. Errors are InvalidArgument at the call unless the function says otherwise.
internal static class MathFunctions
{
    private const string IntegerOverflow = "integer overflow";
    private const string MaxNaN = "invalid argument to 'math.@max': NaN values cannot be ordered";
    private const string MinNaN = "invalid argument to 'math.@min': NaN values cannot be ordered";
    private const string MaxEmpty = "invalid argument to 'math.@max': math.@max(list) argument must not be empty";
    private const string MinEmpty = "invalid argument to 'math.@min': math.@min(list) argument must not be empty";

    // The function name is the first part and the offset is the number: math.bitShiftLeft() negative offset: -1.
    private static readonly Func<CelError, string> NegativeOffsetText = static error =>
        (string)error.First! + "() negative offset: " + error.Number.ToString(CultureInfo.InvariantCulture);

    // math.@max, math_@max_<kind>, math_@max_<kind>_<kind> and math_@max_list_<kind>: the argument itself, the larger of two
    // numbers, or the largest element of a list.
    internal static CelValue Max(ReadOnlySpan<CelValue> arguments, in CallSite site) => Extreme(arguments, in site, isMax: true);

    // math.@min: the same for the smaller.
    internal static CelValue Min(ReadOnlySpan<CelValue> arguments, in CallSite site) => Extreme(arguments, in site, isMax: false);

    // math_ceil_double.
    internal static CelValue Ceil(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value) ? CelValue.FromDouble(Math.Ceiling(value)) : site.NoMatchingOverload(arguments);

    // math_floor_double.
    internal static CelValue Floor(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value) ? CelValue.FromDouble(Math.Floor(value)) : site.NoMatchingOverload(arguments);

    // math_trunc_double.
    internal static CelValue Trunc(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value) ? CelValue.FromDouble(Math.Truncate(value)) : site.NoMatchingOverload(arguments);

    // math_round_double: half away from zero, so 2.5 is 3 and -2.5 is -3.
    internal static CelValue Round(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value)
            ? CelValue.FromDouble(Math.Round(value, MidpointRounding.AwayFromZero))
            : site.NoMatchingOverload(arguments);

    // math_isInf_double.
    internal static CelValue IsInf(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value) ? CelValue.FromBoolean(double.IsInfinity(value)) : site.NoMatchingOverload(arguments);

    // math_isNaN_double.
    internal static CelValue IsNaN(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value) ? CelValue.FromBoolean(double.IsNaN(value)) : site.NoMatchingOverload(arguments);

    // math_isFinite_double: neither infinite nor NaN.
    internal static CelValue IsFinite(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDouble(out double value) ? CelValue.FromBoolean(double.IsFinite(value)) : site.NoMatchingOverload(arguments);

    // math_abs_double, math_abs_int and math_abs_uint: the magnitude; the least int has none.
    internal static CelValue Abs(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (arguments[0].TryGetDouble(out double real))
        {
            return CelValue.FromDouble(Math.Abs(real));
        }

        if (arguments[0].TryGetInt64(out long integer))
        {
            return integer == long.MinValue ? site.Error(CelErrorCode.Overflow, IntegerOverflow) : CelValue.FromInt64(Math.Abs(integer));
        }

        return arguments[0].Kind == CelValueKind.UInt ? arguments[0] : site.NoMatchingOverload(arguments);
    }

    // math_sign_double, math_sign_int and math_sign_uint: NaN for NaN, otherwise -1, 0 or 1 of the kind of the argument; the
    // zeros of a double, also -0.0, give the positive zero.
    internal static CelValue Sign(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (arguments[0].TryGetDouble(out double real))
        {
            return CelValue.FromDouble(double.IsNaN(real) ? real : real > 0 ? 1.0 : real < 0 ? -1.0 : 0.0);
        }

        if (arguments[0].TryGetInt64(out long integer))
        {
            return CelValue.FromInt64(Math.Sign(integer));
        }

        return arguments[0].TryGetUInt64(out ulong unsigned)
            ? CelValue.FromUInt64(unsigned == 0 ? 0UL : 1UL)
            : site.NoMatchingOverload(arguments);
    }

    // math_sqrt_double, math_sqrt_int and math_sqrt_uint: the square root of the value as a double; a negative number is NaN.
    internal static CelValue Sqrt(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (arguments[0].TryGetDouble(out double real))
        {
            return CelValue.FromDouble(Math.Sqrt(real));
        }

        if (arguments[0].TryGetInt64(out long integer))
        {
            return CelValue.FromDouble(Math.Sqrt(integer));
        }

        return arguments[0].TryGetUInt64(out ulong unsigned) ? CelValue.FromDouble(Math.Sqrt(unsigned)) : site.NoMatchingOverload(arguments);
    }

    // math_bitAnd_int_int and math_bitAnd_uint_uint.
    internal static CelValue BitAnd(ReadOnlySpan<CelValue> arguments, in CallSite site) => Bitwise(arguments, in site, BitOperation.And);

    // math_bitOr_int_int and math_bitOr_uint_uint.
    internal static CelValue BitOr(ReadOnlySpan<CelValue> arguments, in CallSite site) => Bitwise(arguments, in site, BitOperation.Or);

    // math_bitXor_int_int and math_bitXor_uint_uint.
    internal static CelValue BitXor(ReadOnlySpan<CelValue> arguments, in CallSite site) => Bitwise(arguments, in site, BitOperation.Xor);

    // math_bitNot_int_int and math_bitNot_uint_uint.
    internal static CelValue BitNot(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (arguments[0].TryGetInt64(out long integer))
        {
            return CelValue.FromInt64(~integer);
        }

        return arguments[0].TryGetUInt64(out ulong unsigned) ? CelValue.FromUInt64(~unsigned) : site.NoMatchingOverload(arguments);
    }

    // math_bitShiftLeft_int_int and math_bitShiftLeft_uint_int: the first argument shifted left by the second, as its 64 bits.
    internal static CelValue BitShiftLeft(ReadOnlySpan<CelValue> arguments, in CallSite site) => Shift(arguments, in site, left: true);

    // math_bitShiftRight_int_int and math_bitShiftRight_uint_int: the shift is logical also for an int, so
    // math.bitShiftRight(-1, 1) is 9223372036854775807.
    internal static CelValue BitShiftRight(ReadOnlySpan<CelValue> arguments, in CallSite site) => Shift(arguments, in site, left: false);

    // The operation on two ints or on two uints.
    private static CelValue Bitwise(ReadOnlySpan<CelValue> arguments, in CallSite site, BitOperation operation)
    {
        if (arguments[0].TryGetInt64(out long left) && arguments[1].TryGetInt64(out long right))
        {
            return CelValue.FromInt64(operation switch
            {
                BitOperation.And => left & right,
                BitOperation.Or => left | right,
                _ => left ^ right,
            });
        }

        if (arguments[0].TryGetUInt64(out ulong unsignedLeft) && arguments[1].TryGetUInt64(out ulong unsignedRight))
        {
            return CelValue.FromUInt64(operation switch
            {
                BitOperation.And => unsignedLeft & unsignedRight,
                BitOperation.Or => unsignedLeft | unsignedRight,
                _ => unsignedLeft ^ unsignedRight,
            });
        }

        return site.NoMatchingOverload(arguments);
    }

    // A negative offset is an error, an offset of 64 and above gives 0: C# masks the count of a shift, so the test comes first.
    private static CelValue Shift(ReadOnlySpan<CelValue> arguments, in CallSite site, bool left)
    {
        if (!arguments[1].TryGetInt64(out long offset))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (arguments[0].TryGetInt64(out long integer))
        {
            if (offset < 0)
            {
                return NegativeOffset(in site, offset);
            }

            return CelValue.FromInt64(offset >= 64 ? 0 : left ? integer << (int)offset : (long)((ulong)integer >> (int)offset));
        }

        if (arguments[0].TryGetUInt64(out ulong unsigned))
        {
            if (offset < 0)
            {
                return NegativeOffset(in site, offset);
            }

            return CelValue.FromUInt64(offset >= 64 ? 0 : left ? unsigned << (int)offset : unsigned >> (int)offset);
        }

        return site.NoMatchingOverload(arguments);
    }

    private static CelValue NegativeOffset(in CallSite site, long offset) =>
        site.Error(CelErrorCode.InvalidArgument, NegativeOffsetText, site.Function, null, offset);

    // The argument itself for one number, the pair rule for two, and a fold over a list for one list.
    private static CelValue Extreme(ReadOnlySpan<CelValue> arguments, in CallSite site, bool isMax)
    {
        if (arguments.Length == 1)
        {
            if (arguments[0].TryGetList(out CelList? list))
            {
                return Fold(list, arguments, in site, isMax);
            }

            return OrderingRules.IsNumber(arguments[0].Kind) ? arguments[0] : site.NoMatchingOverload(arguments);
        }

        if (arguments.Length == 2 && OrderingRules.IsNumber(arguments[0].Kind) && OrderingRules.IsNumber(arguments[1].Kind))
        {
            return Pair(arguments[0], arguments[1], in site, isMax);
        }

        return site.NoMatchingOverload(arguments);
    }

    // The elements in order by the pair rule, each element read once. One element is returned without a comparison. The list
    // overloads do not check the elements at depth (ShallowArguments), so an element that is not a number is refused here.
    private static CelValue Fold(CelList list, ReadOnlySpan<CelValue> arguments, in CallSite site, bool isMax)
    {
        if (list.Count == 0)
        {
            return site.Error(CelErrorCode.InvalidArgument, isMax ? MaxEmpty : MinEmpty);
        }

        CelValue best = default;
        for (int i = 0; i < list.Count; i++)
        {
            CelValue element = list.Read(i, site.Meter);
            if (element.Kind == CelValueKind.Error)
            {
                return site.Placed(element);
            }

            if (!OrderingRules.IsNumber(element.Kind))
            {
                return site.NoMatchingOverload(arguments);
            }

            best = i == 0 ? element : Pair(best, element, in site, isMax);
            if (best.Kind == CelValueKind.Error)
            {
                return best;
            }
        }

        return best;
    }

    // The second number when the first is below it for the maximum, or above it for the minimum, and otherwise the first, so a
    // tie keeps the first with its own kind. A NaN cannot be ordered and is an error.
    private static CelValue Pair(CelValue first, CelValue second, in CallSite site, bool isMax)
    {
        ValueOrdering order = OrderingRules.CompareNumbers(first, second);
        if (order == ValueOrdering.Unordered)
        {
            return site.Error(CelErrorCode.InvalidArgument, isMax ? MaxNaN : MinNaN);
        }

        return (isMax ? order == ValueOrdering.Less : order == ValueOrdering.Greater) ? second : first;
    }

    private enum BitOperation
    {
        And,
        Or,
        Xor,
    }
}

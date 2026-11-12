#if NET11_0_OR_GREATER
using System;
using System.Numerics;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Decimals;

// The implementations of the set decimal. Each checks the kinds of its arguments and answers EvalNoMatchingOverload for another,
// whatever the static types say. The arithmetic and the comparison are those of Decimal128, which follows IEEE 754-2019 (the quantum
// of a result, rounding to 34 digits half to even, the clamp of the exponent) and never throws; the only results to refuse are an
// infinity, which is Overflow, and a zero divisor, which is DivisionByZero before the division. Everything else is the rules of
// Values: the text of a value, the reading of a string, the integers and double, in terms of the sign, the coefficient and the
// exponent. Nothing here uses a culture or the parsing and the formatting of Decimal128.
internal static class DecimalFunctions
{
    private const string DecimalName = "decimal";
    private const string IntName = "int";
    private const string UIntName = "uint";
    private const string DoubleName = "double";
    private const string DecimalOverflow = "decimal overflow";
    private const string DivisionByZero = "division by zero";

    // The texts of a double that is no number, as string(double) writes them.
    private const string NotANumber = "NaN";
    private const string PositiveInfinity = "+Inf";
    private const string NegativeInfinity = "-Inf";

    // The target type is the first part and the value the second: a string as it was written, a decimal as it is when the message is
    // read, so that the error holds the value and not a text made during evaluation.
    private static readonly Func<CelError, string> OutOfRangeText = static error =>
        (error.Second as string ?? TextOf((Decimal128)error.Second!)) + " is out of the range of " + (string)error.First!;

    // The string is the second part, as written.
    private static readonly Func<CelError, string> CannotParseText = static error =>
        "invalid argument to '" + DecimalName + "': cannot parse " + (string)error.Second! + " as " + DecimalName;

    // add_decimal, _+_(decimal, decimal) -> decimal.
    internal static CelValue Add(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? Finite(left + right, in site) : site.NoMatchingOverload(arguments);

    // subtract_decimal, _-_(decimal, decimal) -> decimal.
    internal static CelValue Subtract(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? Finite(left - right, in site) : site.NoMatchingOverload(arguments);

    // multiply_decimal, _*_(decimal, decimal) -> decimal.
    internal static CelValue Multiply(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? Finite(left * right, in site) : site.NoMatchingOverload(arguments);

    // divide_decimal, _/_(decimal, decimal) -> decimal: a divisor that is a zero of any sign or cohort is DivisionByZero before the
    // division, so 0.0 / 0.0, which Decimal128 gives as a NaN, is the same error.
    internal static CelValue Divide(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetPair(arguments, out Decimal128 left, out Decimal128 right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalParts.From(right).Coefficient == UInt128.Zero
            ? site.Error(CelErrorCode.DivisionByZero, DivisionByZero)
            : Finite(left / right, in site);
    }

    // negate_decimal, -_(decimal) -> decimal: the negation of a zero is a negative zero with the same exponent.
    internal static CelValue Negate(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDecimal(out Decimal128 value) ? CelValue.FromDecimal(-value) : site.NoMatchingOverload(arguments);

    // less_decimal, _<_(decimal, decimal) -> bool.
    internal static CelValue Less(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? CelValue.FromBoolean(left < right) : site.NoMatchingOverload(arguments);

    // less_equals_decimal, _<=_(decimal, decimal) -> bool.
    internal static CelValue LessEquals(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? CelValue.FromBoolean(left <= right) : site.NoMatchingOverload(arguments);

    // greater_decimal, _>_(decimal, decimal) -> bool.
    internal static CelValue Greater(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? CelValue.FromBoolean(left > right) : site.NoMatchingOverload(arguments);

    // greater_equals_decimal, _>=_(decimal, decimal) -> bool.
    internal static CelValue GreaterEquals(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out Decimal128 left, out Decimal128 right) ? CelValue.FromBoolean(left >= right) : site.NoMatchingOverload(arguments);

    // int64_to_decimal, decimal(int) -> decimal: exact, exponent 0.
    internal static CelValue Int64ToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetInt64(out long value)
            ? CelValue.FromDecimal(DecimalNumberRules.FromInt64(value).ToDecimal128())
            : site.NoMatchingOverload(arguments);

    // uint64_to_decimal, decimal(uint) -> decimal: exact, exponent 0.
    internal static CelValue UInt64ToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetUInt64(out ulong value)
            ? CelValue.FromDecimal(DecimalNumberRules.FromUInt64(value).ToDecimal128())
            : site.NoMatchingOverload(arguments);

    // double_to_decimal, decimal(double) -> decimal: the digits and the layout of string(double); a NaN and an infinity are
    // ConversionRange.
    internal static CelValue DoubleToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDouble(out double value))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (!double.IsFinite(value))
        {
            string text = double.IsNaN(value) ? NotANumber : value > 0 ? PositiveInfinity : NegativeInfinity;
            return site.Error(CelErrorCode.ConversionRange, OutOfRangeText, DecimalName, text);
        }

        DecimalTextRules.FromDouble(value, out DecimalParts parts);
        return CelValue.FromDecimal(parts.ToDecimal128());
    }

    // string_to_decimal, decimal(string) -> decimal: the grammar of the page, rounded to 34 digits half to even. A text outside the
    // grammar is InvalidArgument; an exponent outside −6176 to 6111 after the rounding, in a zero too, is ConversionRange.
    internal static CelValue StringToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalTextRules.TryParse(text, false, out DecimalParts parts) switch
        {
            DecimalParseResult.Parsed => CelValue.FromDecimal(parts.ToDecimal128()),
            DecimalParseResult.ExponentOutOfRange => site.Error(CelErrorCode.ConversionRange, OutOfRangeText, DecimalName, text),
            _ => site.Error(CelErrorCode.InvalidArgument, CannotParseText, DecimalName, text),
        };
    }

    // decimal_to_decimal, decimal(decimal) -> decimal: the argument itself, with its exponent and the sign of a zero.
    internal static CelValue DecimalIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].Kind == CelValueKind.Decimal ? arguments[0] : site.NoMatchingOverload(arguments);

    // decimal_to_int64, int(decimal) -> int: truncated toward zero, then ConversionRange outside the range of int.
    internal static CelValue DecimalToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDecimal(out Decimal128 value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalNumberRules.ToInt64(DecimalParts.From(value), out long result)
            ? CelValue.FromInt64(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, IntName, value);
    }

    // decimal_to_uint64, uint(decimal) -> uint: truncated toward zero, then ConversionRange outside the range of uint; the sign is
    // tested after the truncation, so uint(-0.5m) is 0u.
    internal static CelValue DecimalToUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDecimal(out Decimal128 value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalNumberRules.ToUInt64(DecimalParts.From(value), out ulong result)
            ? CelValue.FromUInt64(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, UIntName, value);
    }

    // decimal_to_double, double(decimal) -> double: the nearest double, half to even; ConversionRange when it would be infinite.
    internal static CelValue DecimalToDouble(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDecimal(out Decimal128 value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalTextRules.ToDouble(DecimalParts.From(value), out double result)
            ? CelValue.FromDouble(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, DoubleName, value);
    }

    // decimal_to_string, string(decimal) -> string: the to-scientific-string, formatted on the stack. Its length is charged,
    // ⌈Lᵣ/10⌉, before the string is created.
    internal static CelValue DecimalToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDecimal(out Decimal128 value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        int length = DecimalTextRules.Format(DecimalParts.From(value), text);
        if (site.HasLimit)
        {
            site.Charge(DecimalCosts.Tenths((ulong)length));
        }

        return CelValue.FromString(new string(text[..length]));
    }

    // The two values of a binary overload; false when either is not a decimal.
    private static bool TryGetPair(ReadOnlySpan<CelValue> arguments, out Decimal128 left, out Decimal128 right)
    {
        right = default;
        return arguments[0].TryGetDecimal(out left) && arguments[1].TryGetDecimal(out right);
    }

    // A result of the arithmetic: an infinity, the only result that is not finite once a zero divisor is refused, is Overflow.
    private static CelValue Finite(Decimal128 result, in CallSite site) =>
        Decimal128.IsFinite(result) ? CelValue.FromDecimal(result) : site.Error(CelErrorCode.Overflow, DecimalOverflow);

    // The text of a value of a message.
    private static string TextOf(Decimal128 value)
    {
        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        return new string(text[..DecimalTextRules.Format(DecimalParts.From(value), text)]);
    }
}
#endif

using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Decimals;

// The implementations of the set decimal. Each checks the kinds of its arguments and answers EvalNoMatchingOverload for another,
// whatever the static types say. The arithmetic and the comparison are those of the platform type, a Decimal128 on net11.0 (IEEE
// 754-2019: the quantum of a result, rounding to 34 digits half to even, the clamp of the exponent) and a System.Decimal on net10.0;
// the only results to refuse are one that the platform cannot hold, which is Overflow, and a zero divisor, which is DivisionByZero
// before the division. Everything else is the rules of Values: the text of a value, the reading of a string, the integers and
// double, in terms of the sign, the coefficient and the exponent. Nothing here uses a culture or the parsing and the formatting of
// the platform type.
internal static class DecimalFunctions
{
    private const string DecimalName = "decimal";
    private const string IntName = "int";
    private const string UIntName = "uint";
    private const string DoubleName = "double";
    private const string DecimalOverflow = "decimal overflow";
    private const string DivisionByZero = "division by zero";
    private const string RoundName = "round";
    private const string QuantizeName = "quantize";

    // What the preparer keeps for a mode string that names no mode, as the program holds it for the call.
    private static readonly object NotAMode = new();

    // The texts of a double that is no number, as string(double) writes them.
    private const string NotANumber = "NaN";
    private const string PositiveInfinity = "+Inf";
    private const string NegativeInfinity = "-Inf";

    // The target type is the first part and the value the second: a string as it was written, a decimal as it is when the message is
    // read, so that the error holds the value and not a text made during evaluation.
    private static readonly ErrorTemplate OutOfRangeText = new(
        static error => (error.Second as string ?? TextOf((PlatformDecimal)error.Second!)) + " is out of the range of " + (string)error.First!,
        expectedType: static error => ValueTypeRules.TypeOfName((string)error.First!),
        actualType: static error => error.Second is string ? CelType.String : CelType.Decimal);

    // The function is the first part and the string that is no mode the second, as written.
    private static readonly ErrorTemplate UnknownModeText = new(
        static error => "invalid argument to '" + (string)error.First! + "': unknown rounding mode \"" + (string)error.Second! + "\"",
        subject: static error => (string)error.First!);

    // The string is the second part, as written.
    private static readonly ErrorTemplate CannotParseText = new(
        static error => "invalid argument to '" + DecimalName + "': cannot parse " + (string)error.Second! + " as " + DecimalName,
        subject: static error => (string)error.First!);

    // add_decimal, _+_(decimal, decimal) -> decimal.
    internal static CelValue Add(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right)
            ? Finite(DecimalPlatform.TryAdd(left, right, out PlatformDecimal result), result, in site)
            : site.NoMatchingOverload(arguments);

    // subtract_decimal, _-_(decimal, decimal) -> decimal.
    internal static CelValue Subtract(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right)
            ? Finite(DecimalPlatform.TrySubtract(left, right, out PlatformDecimal result), result, in site)
            : site.NoMatchingOverload(arguments);

    // multiply_decimal, _*_(decimal, decimal) -> decimal.
    internal static CelValue Multiply(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right)
            ? Finite(DecimalPlatform.TryMultiply(left, right, out PlatformDecimal result), result, in site)
            : site.NoMatchingOverload(arguments);

    // divide_decimal, _/_(decimal, decimal) -> decimal: a divisor that is a zero of any sign or cohort is DivisionByZero before the
    // division, so 0.0 / 0.0, which Decimal128 gives as a NaN and System.Decimal refuses, is the same error.
    internal static CelValue Divide(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalPlatform.ToParts(right).Coefficient == UInt128.Zero
            ? site.Error(CelErrorCode.DivisionByZero, DivisionByZero)
            : Finite(DecimalPlatform.TryDivide(left, right, out PlatformDecimal result), result, in site);
    }

    // negate_decimal, -_(decimal) -> decimal: the negation of a zero is a negative zero with the same exponent.
    internal static CelValue Negate(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetPlatformDecimal(out PlatformDecimal value)
            ? CelValue.FromPlatformDecimal(DecimalPlatform.Negate(value))
            : site.NoMatchingOverload(arguments);

    // less_decimal, _<_(decimal, decimal) -> bool.
    internal static CelValue Less(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right) ? CelValue.FromBoolean(left < right) : site.NoMatchingOverload(arguments);

    // less_equals_decimal, _<=_(decimal, decimal) -> bool.
    internal static CelValue LessEquals(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right) ? CelValue.FromBoolean(left <= right) : site.NoMatchingOverload(arguments);

    // greater_decimal, _>_(decimal, decimal) -> bool.
    internal static CelValue Greater(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right) ? CelValue.FromBoolean(left > right) : site.NoMatchingOverload(arguments);

    // greater_equals_decimal, _>=_(decimal, decimal) -> bool.
    internal static CelValue GreaterEquals(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal left, out PlatformDecimal right) ? CelValue.FromBoolean(left >= right) : site.NoMatchingOverload(arguments);

    // int64_to_decimal, decimal(int) -> decimal: exact, exponent 0.
    internal static CelValue Int64ToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetInt64(out long value)
            ? CelValue.FromPlatformDecimal(DecimalPlatform.FromParts(DecimalNumberRules.FromInt64(value)))
            : site.NoMatchingOverload(arguments);

    // uint64_to_decimal, decimal(uint) -> decimal: exact, exponent 0.
    internal static CelValue UInt64ToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetUInt64(out ulong value)
            ? CelValue.FromPlatformDecimal(DecimalPlatform.FromParts(DecimalNumberRules.FromUInt64(value)))
            : site.NoMatchingOverload(arguments);

    // double_to_decimal, decimal(double) -> decimal: the digits and the layout of string(double); a NaN and an infinity are
    // ConversionRange, and so is a value that the platform does not hold (decimal(1e300) on net10.0).
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
        return DecimalPlatform.TryRound(parts, out PlatformDecimal result)
            ? CelValue.FromPlatformDecimal(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, DecimalName, TextOf(parts));
    }

    // string_to_decimal, decimal(string) -> decimal: the grammar of the page, rounded once to the precision of the platform half to even.
    // A text outside the grammar is InvalidArgument; on net11.0 an exponent outside −6176 to 6111 after the rounding, in a zero too, and
    // on net10.0 a value whose integer part does not fit 96 bits is ConversionRange.
    internal static CelValue StringToDecimal(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalTextRules.TryParse(text, false, out DecimalParts parts) switch
        {
            DecimalParseResult.Parsed => CelValue.FromPlatformDecimal(DecimalPlatform.FromParts(parts)),
            DecimalParseResult.ExponentOutOfRange or DecimalParseResult.ValueOutOfRange => site.Error(CelErrorCode.ConversionRange, OutOfRangeText, DecimalName, text),
            _ => site.Error(CelErrorCode.InvalidArgument, CannotParseText, DecimalName, text),
        };
    }

    // decimal_to_decimal, decimal(decimal) -> decimal: the argument itself, with its exponent and the sign of a zero.
    internal static CelValue DecimalIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].Kind == CelValueKind.Decimal ? arguments[0] : site.NoMatchingOverload(arguments);

    // decimal_to_int64, int(decimal) -> int: truncated toward zero, then ConversionRange outside the range of int.
    internal static CelValue DecimalToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetPlatformDecimal(out PlatformDecimal value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalNumberRules.ToInt64(DecimalPlatform.ToParts(value), out long result)
            ? CelValue.FromInt64(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, IntName, value);
    }

    // decimal_to_uint64, uint(decimal) -> uint: truncated toward zero, then ConversionRange outside the range of uint; the sign is
    // tested after the truncation, so uint(-0.5m) is 0u.
    internal static CelValue DecimalToUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetPlatformDecimal(out PlatformDecimal value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalNumberRules.ToUInt64(DecimalPlatform.ToParts(value), out ulong result)
            ? CelValue.FromUInt64(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, UIntName, value);
    }

    // decimal_to_double, double(decimal) -> double: the nearest double, half to even; ConversionRange when it would be infinite.
    internal static CelValue DecimalToDouble(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetPlatformDecimal(out PlatformDecimal value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return DecimalTextRules.ToDouble(DecimalPlatform.ToParts(value), out double result)
            ? CelValue.FromDouble(result)
            : site.Error(CelErrorCode.ConversionRange, OutOfRangeText, DoubleName, value);
    }

    // decimal_to_string, string(decimal) -> string: the to-scientific-string, formatted on the stack. Its length is charged,
    // ⌈Lᵣ/10⌉, before the string is created.
    internal static CelValue DecimalToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetPlatformDecimal(out PlatformDecimal value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        int length = DecimalTextRules.Format(DecimalPlatform.ToParts(value), text);
        if (site.HasLimit)
        {
            site.Charge(DecimalCosts.Tenths((ulong)length));
        }

        return CelValue.FromString(new string(text[..length]));
    }

    // decimal_round_int64, x.round(n) -> decimal: n digits after the point by half to even; no zeros are added.
    internal static CelValue Round(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetPlatformDecimal(out PlatformDecimal value) && arguments[1].TryGetInt64(out long digits)
            ? RoundTo(value, digits, DecimalRoundingMode.HalfEven, in site)
            : site.NoMatchingOverload(arguments);

    // decimal_round_int64_string, x.round(n, mode) -> decimal: the same by a mode, one of the seven names; another string is
    // InvalidArgument, a constant one too, at evaluation.
    internal static CelValue RoundWithMode(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetPlatformDecimal(out PlatformDecimal value)
            || !arguments[1].TryGetInt64(out long digits)
            || !arguments[2].TryGetString(out string? name))
        {
            return site.NoMatchingOverload(arguments);
        }

        return TryGetMode(name, in site, RoundName, out DecimalRoundingMode mode, out CelValue refused)
            ? RoundTo(value, digits, mode, in site)
            : refused;
    }

    // decimal_quantize_decimal, x.quantize(q) -> decimal: x with the exponent of q by half to even; the value of q does not matter.
    internal static CelValue Quantize(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryGetPair(arguments, out PlatformDecimal value, out PlatformDecimal quantum)
            ? QuantizeTo(value, quantum, DecimalRoundingMode.HalfEven, in site)
            : site.NoMatchingOverload(arguments);

    // decimal_quantize_decimal_string, x.quantize(q, mode) -> decimal.
    internal static CelValue QuantizeWithMode(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetPlatformDecimal(out PlatformDecimal value)
            || !arguments[1].TryGetPlatformDecimal(out PlatformDecimal quantum)
            || !arguments[2].TryGetString(out string? name))
        {
            return site.NoMatchingOverload(arguments);
        }

        return TryGetMode(name, in site, QuantizeName, out DecimalRoundingMode mode, out CelValue refused)
            ? QuantizeTo(value, quantum, mode, in site)
            : refused;
    }

    // The preparer of the mode when the call has a string constant there: the mode is found once, with the program, and a string that is
    // no mode is kept as it is, so that the call refuses it at evaluation. It charges nothing and does not throw.
    internal static readonly ConstantPreparer ModePreparer = static (name, _) =>
        ConstantPreparation.Success(DecimalRoundingRules.TryParseMode(name, out DecimalRoundingMode mode) ? mode : NotAMode);

    // The constant argument of the two overloads that take a mode: the receiver is 0, the number or the quantum is 1, the mode is 2.
    internal static ConstantArgument ModeArgument { get; } = new(2, ModePreparer);

    // The mode of the call: the one that the program prepared, or the one that the string names. A string that is none is
    // InvalidArgument naming the function and the string.
    private static bool TryGetMode(string name, in CallSite site, string function, out DecimalRoundingMode mode, out CelValue refused)
    {
        refused = default;
        if (site.Prepared is DecimalRoundingMode prepared)
        {
            mode = prepared;
            return true;
        }

        if (DecimalRoundingRules.TryParseMode(name, out mode))
        {
            return true;
        }

        refused = site.Error(CelErrorCode.InvalidArgument, UnknownModeText, function, name);
        return false;
    }

    private static CelValue RoundTo(PlatformDecimal value, long digits, DecimalRoundingMode mode, in CallSite site) =>
        DecimalPlatform.TryFromRounded(DecimalRoundingRules.Round(DecimalPlatform.ToParts(value), digits, mode), out PlatformDecimal result)
            ? CelValue.FromPlatformDecimal(result)
            : site.Error(CelErrorCode.Overflow, DecimalOverflow);

    private static CelValue QuantizeTo(PlatformDecimal value, PlatformDecimal quantum, DecimalRoundingMode mode, in CallSite site) =>
        DecimalRoundingRules.TryQuantize(DecimalPlatform.ToParts(value), DecimalPlatform.ToParts(quantum).Exponent, mode, out DecimalParts rounded)
        && DecimalPlatform.TryFromRounded(rounded, out PlatformDecimal result)
            ? CelValue.FromPlatformDecimal(result)
            : site.Error(CelErrorCode.Overflow, DecimalOverflow);

    // The two values of a binary overload; false when either is not a decimal.
    private static bool TryGetPair(ReadOnlySpan<CelValue> arguments, out PlatformDecimal left, out PlatformDecimal right)
    {
        right = default;
        return arguments[0].TryGetPlatformDecimal(out left) && arguments[1].TryGetPlatformDecimal(out right);
    }

    // A result of the arithmetic: one that the platform cannot hold, the only failure once a zero divisor is refused, is Overflow.
    private static CelValue Finite(bool held, PlatformDecimal result, in CallSite site) =>
        held ? CelValue.FromPlatformDecimal(result) : site.Error(CelErrorCode.Overflow, DecimalOverflow);

    // The text of a value of a message.
    private static string TextOf(PlatformDecimal value) => TextOf(DecimalPlatform.ToParts(value));

    private static string TextOf(DecimalParts parts)
    {
        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        return new string(text[..DecimalTextRules.Format(parts, text)]);
    }
}

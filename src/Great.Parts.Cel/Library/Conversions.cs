using System;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// type, dyn and the conversions between bool, bytes, double, int, string and uint. An identity returns
// its argument without a copy, but like every implementation it rejects a value of another kind. A text
// result whose length is known only from its content charges its part before it is created.
internal static class Conversions
{
    private const string IntName = "int";
    private const string UIntName = "uint";
    private const string DoubleName = "double";

    // The target types of a text that does not parse, by the byte of the error.
    private const byte IntTarget = 0;
    private const byte UIntTarget = 1;
    private const byte DoubleTarget = 2;
    private const byte BoolTarget = 3;

    private static readonly string[] ParseTargets = [IntName, UIntName, DoubleName, "bool"];

    // The target type is the first part; the value is kept by ErrorValueParts.
    private static readonly Func<CelError, string> OutOfRangeText = static error => ValueText(ErrorValueParts.ValueOf(error)) + " is out of the range of " + (string)error.First!;

    // The function name is the first part, the text the second, and the byte the index of the target type.
    private static readonly Func<CelError, string> CannotParseText = static error =>
        "invalid argument to '" + (string)error.First! + "': cannot parse " + (string)error.Second! + " as " + ParseTargets[error.Small];

    // The function name is the first part, the offset of the byte the number.
    private static readonly Func<CelError, string> InvalidUtf8Text = static error =>
        "invalid argument to '" + (string)error.First! + "': invalid UTF-8 at byte " + error.Number.ToString(CultureInfo.InvariantCulture);

    // type, (A) -> type(A): the runtime type with its parameters erased; of an object value its object type, and an
    // instance without a description is TypeNotAccessible at the call.
    internal static CelValue Type(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (arguments[0].Kind != CelValueKind.Object)
        {
            return CelValue.FromType(ValueTypeRules.TypeOf(arguments[0]));
        }

        return ObjectRules.TryDescribe(arguments[0], site.Types, out CelObjectType? description, out CelValue error)
            ? CelValue.FromType(description!.Type)
            : site.Placed(error);
    }

    // to_dyn, (A) -> dyn.
    internal static CelValue ToDyn(ReadOnlySpan<CelValue> arguments, in CallSite _) => arguments[0];

    // bool_to_bool, bytes_to_bytes, double_to_double, int64_to_int64, string_to_string, uint64_to_uint64.
    internal static CelValue BoolIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) => Identity(arguments, in site, CelValueKind.Bool);

    internal static CelValue BytesIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) => Identity(arguments, in site, CelValueKind.Bytes);

    internal static CelValue DoubleIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) => Identity(arguments, in site, CelValueKind.Double);

    internal static CelValue IntIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) => Identity(arguments, in site, CelValueKind.Int);

    internal static CelValue StringIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) => Identity(arguments, in site, CelValueKind.String);

    internal static CelValue UIntIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) => Identity(arguments, in site, CelValueKind.UInt);

    // int64_to_double, (int) -> double: the nearest double, ties to even.
    internal static CelValue Int64ToDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetInt64(out long value) ? CelValue.FromDouble(value) : site.NoMatchingOverload(arguments);

    // uint64_to_double, (uint) -> double: the nearest double, ties to even.
    internal static CelValue UInt64ToDouble(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetUInt64(out ulong value) ? CelValue.FromDouble(NumberText.ToDouble(value)) : site.NoMatchingOverload(arguments);

    // double_to_int64, (double) -> int: truncation toward zero inside the open range (-2^63, 2^63).
    internal static CelValue DoubleToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDouble(out double value))
        {
            return site.NoMatchingOverload(arguments);
        }

        // A NaN fails both comparisons.
        return value > -EqualityRules.TwoToThe63 && value < EqualityRules.TwoToThe63
            ? CelValue.FromInt64((long)value)
            : OutOfRange(in site, arguments[0], IntName);
    }

    // uint64_to_int64, (uint) -> int.
    internal static CelValue UInt64ToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetUInt64(out ulong value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return value <= long.MaxValue
            ? CelValue.FromInt64((long)value)
            : OutOfRange(in site, arguments[0], IntName);
    }

    // double_to_uint64, (double) -> uint: the sign is tested before truncation, so -0.0 is 0u and -0.5 an
    // error; the range is [0, 2^64).
    internal static CelValue DoubleToUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDouble(out double value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return value >= 0 && value < EqualityRules.TwoToThe64
            ? CelValue.FromUInt64((ulong)value)
            : OutOfRange(in site, arguments[0], UIntName);
    }

    // int64_to_uint64, (int) -> uint.
    internal static CelValue Int64ToUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long value))
        {
            return site.NoMatchingOverload(arguments);
        }

        return value >= 0
            ? CelValue.FromUInt64((ulong)value)
            : OutOfRange(in site, arguments[0], UIntName);
    }

    // string_to_int64, (string) -> int.
    internal static CelValue StringToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        NumberText.ParseResult result = NumberText.ParseInt64(text, out long value);
        return result == NumberText.ParseResult.Parsed ? CelValue.FromInt64(value) : NotParsed(in site, arguments[0], text, result, IntTarget);
    }

    // string_to_uint64, (string) -> uint.
    internal static CelValue StringToUInt64(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        NumberText.ParseResult result = NumberText.ParseUInt64(text, out ulong value);
        return result == NumberText.ParseResult.Parsed ? CelValue.FromUInt64(value) : NotParsed(in site, arguments[0], text, result, UIntTarget);
    }

    // string_to_double, (string) -> double.
    internal static CelValue StringToDouble(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        NumberText.ParseResult result = NumberText.ParseDouble(text, out double value);
        return result == NumberText.ParseResult.Parsed ? CelValue.FromDouble(value) : NotParsed(in site, arguments[0], text, result, DoubleTarget);
    }

    // The error of a text that is not a number of the target type, or whose number is out of its range.
    private static CelValue NotParsed(in CallSite site, CelValue argument, string text, NumberText.ParseResult result, byte target) =>
        result == NumberText.ParseResult.OutOfRange ? OutOfRange(in site, argument, ParseTargets[target]) : CannotParse(in site, text, target);

    // string_to_bool, (string) -> bool: the twelve texts of the standard library page.
    internal static CelValue StringToBool(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return NumberText.TryParseBool(text, out bool value) ? CelValue.FromBoolean(value) : CannotParse(in site, text, BoolTarget);
    }

    // bool_to_string, (bool) -> string.
    internal static CelValue BoolToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetBoolean(out bool value))
        {
            return site.NoMatchingOverload(arguments);
        }

        // Both texts are shorter than 10 characters: one unit.
        site.Charge(1);
        return CelValue.FromString(value ? "true" : "false");
    }

    // int64_to_string, (int) -> string: decimal digits with a minus sign.
    internal static CelValue Int64ToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[NumberText.MaxIntegerLength];
        _ = value.TryFormat(text, out int length, default, CultureInfo.InvariantCulture);
        return NewString(in site, text[..length]);
    }

    // uint64_to_string, (uint) -> string: decimal digits without a suffix.
    internal static CelValue UInt64ToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetUInt64(out ulong value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[NumberText.MaxIntegerLength];
        _ = value.TryFormat(text, out int length, default, CultureInfo.InvariantCulture);
        return NewString(in site, text[..length]);
    }

    // double_to_string, (double) -> string: Go %g of cel-go.
    internal static CelValue DoubleToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDouble(out double value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[NumberText.MaxDoubleLength];
        return NewString(in site, text[..NumberText.FormatDouble(value, text)]);
    }

    // string_to_bytes, (string) -> bytes: UTF-8, a lone surrogate as U+FFFD.
    internal static CelValue StringToBytes(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (site.HasLimit)
        {
            site.Charge(Costs.Tenths((ulong)Encoding.UTF8.GetByteCount(text)));
        }

        return CelValue.FromBytes(Encoding.UTF8.GetBytes(text));
    }

    // bytes_to_string, (bytes) -> string: strict UTF-8.
    internal static CelValue BytesToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetBytes(out ReadOnlyMemory<byte> memory))
        {
            return site.NoMatchingOverload(arguments);
        }

        ReadOnlySpan<byte> bytes = memory.Span;
        if (!Utf8.IsValid(bytes))
        {
            return site.Error(CelErrorCode.InvalidArgument, InvalidUtf8Text, site.Function, number: InvalidOffset(bytes));
        }

        if (site.HasLimit)
        {
            site.Charge(Costs.Tenths((ulong)Encoding.UTF8.GetCharCount(bytes)));
        }

        return CelValue.FromString(Encoding.UTF8.GetString(bytes));
    }

    private static CelValue Identity(ReadOnlySpan<CelValue> arguments, in CallSite site, CelValueKind kind) =>
        arguments[0].Kind == kind ? arguments[0] : site.NoMatchingOverload(arguments);

    // Charges ⌈Lᵣ/10⌉ for a text formatted on the stack, then creates the string.
    private static CelValue NewString(in CallSite site, ReadOnlySpan<char> text)
    {
        if (site.HasLimit)
        {
            site.Charge(Costs.Tenths((ulong)text.Length));
        }

        return CelValue.FromString(new string(text));
    }

    // The offset of the first byte that does not begin a well-formed UTF-8 sequence.
    private static int InvalidOffset(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        while (System.Text.Rune.DecodeFromUtf8(bytes[offset..], out _, out int consumed) == System.Buffers.OperationStatus.Done)
        {
            offset += consumed;
        }

        return offset;
    }

    // The value is written as string() writes it, a text as it is.
    private static CelValue OutOfRange(in CallSite site, CelValue value, string targetType) =>
        site.ErrorWithValue(CelErrorCode.ConversionRange, OutOfRangeText, targetType, value);

    // The target is the index of the target type in ParseTargets.
    private static CelValue CannotParse(in CallSite site, string text, byte target) =>
        site.Error(CelErrorCode.InvalidArgument, CannotParseText, site.Function, text, small: target);

    // A double in its shortest form, an int or a uint in decimal, a string as it is.
    private static string ValueText(CelValue value)
    {
        if (value.TryGetDouble(out double number))
        {
            return NumberText.FormatDouble(number);
        }

        if (value.TryGetInt64(out long signed))
        {
            return signed.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetUInt64(out ulong unsigned))
        {
            return unsigned.ToString(CultureInfo.InvariantCulture);
        }

        _ = value.TryGetString(out string? text);
        return text!;
    }
}

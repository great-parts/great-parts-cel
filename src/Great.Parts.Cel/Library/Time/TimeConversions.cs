using System;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The nine conversions of time: timestamp(timestamp), timestamp(int), timestamp(string), int(timestamp),
// string(timestamp) and the same for duration. Texts go through TimeTextRules; the text of a result is formed in a buffer on the
// stack, and its cost was charged from the argument before the call. Like every implementation, each rejects a value of
// another kind.
internal static class TimeConversions
{
    private const string TimestampName = "timestamp";
    private const string DurationName = "duration";

    // The target type is the first part; the value is kept by ErrorValueParts, a number or a string as it is.
    private static readonly Func<CelError, string> OutOfRangeText = static error =>
        ValueText(ErrorValueParts.ValueOf(error)) + " is out of the range of " + (string)error.First!;

    // The function name is the first part, the text the second.
    private static readonly Func<CelError, string> CannotParseText = static error =>
        "invalid argument to '" + (string)error.First! + "': cannot parse " + (string)error.Second! + " as " + (error.Small == 0 ? TimestampName : DurationName);

    // timestamp_to_timestamp, (timestamp) -> timestamp: the argument itself.
    internal static CelValue TimestampIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].Kind == CelValueKind.Timestamp ? arguments[0] : site.NoMatchingOverload(arguments);

    // duration_to_duration, (duration) -> duration: the argument itself.
    internal static CelValue DurationIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].Kind == CelValueKind.Duration ? arguments[0] : site.NoMatchingOverload(arguments);

    // int64_to_timestamp, (int) -> timestamp: Unix seconds, nanoseconds 0.
    internal static CelValue Int64ToTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long seconds))
        {
            return site.NoMatchingOverload(arguments);
        }

        return seconds is >= CelTimestamp.MinSeconds and <= CelTimestamp.MaxSeconds
            ? CelValue.FromTimestamp(CelTimestamp.FromChecked(seconds, 0))
            : OutOfRange(in site, arguments[0], TimestampName);
    }

    // timestamp_to_int64, (timestamp) -> int: the seconds of the timestamp, the floor of its Unix time.
    internal static CelValue TimestampToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp value) ? CelValue.FromInt64(value.Seconds) : site.NoMatchingOverload(arguments);

    // duration_to_int64, (duration) -> int: the nanoseconds.
    internal static CelValue DurationToInt64(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDuration(out CelDuration value) ? CelValue.FromInt64(value.TotalNanoseconds) : site.NoMatchingOverload(arguments);

    // string_to_timestamp, (string) -> timestamp: RFC 3339.
    internal static CelValue StringToTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return TimeTextRules.TryParseTimestamp(text, out CelTimestamp value) switch
        {
            TimeParseResult.Parsed => CelValue.FromTimestamp(value),
            TimeParseResult.OutOfRange => OutOfRange(in site, arguments[0], TimestampName),
            _ => CannotParse(in site, text, 0),
        };
    }

    // string_to_duration, (string) -> duration: the grammar of time.ParseDuration.
    internal static CelValue StringToDuration(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return TimeTextRules.TryParseDuration(text, out CelDuration value) switch
        {
            TimeParseResult.Parsed => CelValue.FromDuration(value),
            TimeParseResult.OutOfRange => OutOfRange(in site, arguments[0], DurationName),
            _ => CannotParse(in site, text, 1),
        };
    }

    // timestamp_to_string, (timestamp) -> string: RFC 3339 in UTC with Z; at most 30 characters, formed on the stack.
    internal static CelValue TimestampToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimestamp(out CelTimestamp value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[TimeTextRules.MaxTimestampLength];
        return CelValue.FromString(new string(text[..TimeTextRules.WriteTimestamp(value, text)]));
    }

    // duration_to_string, (duration) -> string: the seconds and the fraction of up to nine digits, exact, with s; at most 22
    // characters, formed on the stack.
    internal static CelValue DurationToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDuration(out CelDuration value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[TimeTextRules.MaxDurationLength];
        return CelValue.FromString(new string(text[..TimeTextRules.WriteDuration(value.TotalNanoseconds, text)]));
    }

    // The value, an int or a string, is written as it is.
    private static CelValue OutOfRange(in CallSite site, CelValue value, string targetType) =>
        site.ErrorWithValue(CelErrorCode.ConversionRange, OutOfRangeText, targetType, value);

    // The target is 0 for timestamp and 1 for duration.
    private static CelValue CannotParse(in CallSite site, string text, byte target) =>
        site.Error(CelErrorCode.InvalidArgument, CannotParseText, site.Function, text, small: target);

    private static string ValueText(CelValue value)
    {
        if (value.TryGetInt64(out long number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        _ = value.TryGetString(out string? text);
        return text!;
    }
}

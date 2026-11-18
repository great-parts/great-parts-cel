using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The six operations + and - of time. A duration is whole seconds and nanoseconds with the sign of the seconds, as
// google.protobuf.Duration keeps it, so every operation is an addition of two small numbers in a long that cannot overflow:
// the nanoseconds carry or borrow one second, and the result is checked against the range of its type. Nothing throws.
internal static class TimeArithmetic
{
    private const long NanosecondsPerSecond = 1_000_000_000;
    private const string TimestampOverflow = "timestamp overflow";
    private const string DurationOverflow = "duration overflow";

    // add_timestamp_duration, (timestamp, duration) -> timestamp.
    internal static CelValue AddTimestampDuration(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp moment) && arguments[1].TryGetDuration(out CelDuration span)
            ? Shift(in site, moment, span.Seconds, span.Nanoseconds)
            : site.NoMatchingOverload(arguments);

    // add_duration_timestamp, (duration, timestamp) -> timestamp.
    internal static CelValue AddDurationTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDuration(out CelDuration span) && arguments[1].TryGetTimestamp(out CelTimestamp moment)
            ? Shift(in site, moment, span.Seconds, span.Nanoseconds)
            : site.NoMatchingOverload(arguments);

    // subtract_timestamp_duration, (timestamp, duration) -> timestamp.
    internal static CelValue SubtractTimestampDuration(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp moment) && arguments[1].TryGetDuration(out CelDuration span)
            ? Shift(in site, moment, -span.Seconds, -span.Nanoseconds)
            : site.NoMatchingOverload(arguments);

    // subtract_timestamp_timestamp, (timestamp, timestamp) -> duration. The timestamps are at most 3.2 * 10^11 seconds apart, within the
    // range of a duration, so the difference never overflows.
    internal static CelValue SubtractTimestampTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimestamp(out CelTimestamp left) || !arguments[1].TryGetTimestamp(out CelTimestamp right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return Normalize(in site, left.Seconds - right.Seconds, left.Nanoseconds - right.Nanoseconds);
    }

    // add_duration_duration, (duration, duration) -> duration.
    internal static CelValue AddDurationDuration(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDuration(out CelDuration left) || !arguments[1].TryGetDuration(out CelDuration right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return Normalize(in site, left.Seconds + right.Seconds, (long)left.Nanoseconds + right.Nanoseconds);
    }

    // subtract_duration_duration, (duration, duration) -> duration.
    internal static CelValue SubtractDurationDuration(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDuration(out CelDuration left) || !arguments[1].TryGetDuration(out CelDuration right))
        {
            return site.NoMatchingOverload(arguments);
        }

        return Normalize(in site, left.Seconds - right.Seconds, (long)left.Nanoseconds - right.Nanoseconds);
    }

    // The duration of seconds and nanoseconds, each the sum or the difference of two parts: the nanoseconds are less than 2 * 10^9 in
    // absolute value. They carry into the seconds, then take the sign of the seconds, and the seconds are checked against the range.
    private static CelValue Normalize(in CallSite site, long seconds, long nanoseconds)
    {
        if (nanoseconds >= NanosecondsPerSecond)
        {
            seconds++;
            nanoseconds -= NanosecondsPerSecond;
        }
        else if (nanoseconds <= -NanosecondsPerSecond)
        {
            seconds--;
            nanoseconds += NanosecondsPerSecond;
        }

        if (seconds > 0 && nanoseconds < 0)
        {
            seconds--;
            nanoseconds += NanosecondsPerSecond;
        }
        else if (seconds < 0 && nanoseconds > 0)
        {
            seconds++;
            nanoseconds -= NanosecondsPerSecond;
        }

        return seconds is >= -CelDuration.MaxSeconds and <= CelDuration.MaxSeconds
            ? CelValue.FromDuration(CelDuration.FromChecked(seconds, (int)nanoseconds))
            : site.Error(CelErrorCode.Overflow, DurationOverflow);
    }

    // The moment shifted by the duration of the given seconds and nanoseconds, both with the same sign: the nanoseconds carry or
    // borrow one second, and the seconds are checked against the range of timestamp.
    private static CelValue Shift(in CallSite site, CelTimestamp moment, long seconds, int nanoseconds)
    {
        long fraction = moment.Nanoseconds + nanoseconds;
        long total = moment.Seconds + seconds;
        if (fraction >= NanosecondsPerSecond)
        {
            fraction -= NanosecondsPerSecond;
            total++;
        }
        else if (fraction < 0)
        {
            fraction += NanosecondsPerSecond;
            total--;
        }

        return total is >= CelTimestamp.MinSeconds and <= CelTimestamp.MaxSeconds
            ? CelValue.FromTimestamp(CelTimestamp.FromChecked(total, (int)fraction))
            : site.Error(CelErrorCode.Overflow, TimestampOverflow);
    }
}

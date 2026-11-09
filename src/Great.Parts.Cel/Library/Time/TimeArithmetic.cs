using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The six operations + and - of time. A duration splits into whole seconds and nanoseconds within the second by floor
// division, so a shift of a timestamp is an addition of two small numbers that cannot overflow `long`; the result is checked
// against the range of timestamp. The difference of two timestamps is computed in 128 bits. Nothing throws.
internal static class TimeArithmetic
{
    private const long NanosecondsPerSecond = 1_000_000_000;
    private const string TimestampOverflow = "timestamp overflow";
    private const string DurationOverflow = "duration overflow";

    // add_timestamp_duration, (timestamp, duration) -> timestamp.
    internal static CelValue AddTimestampDuration(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp moment) && arguments[1].TryGetDuration(out CelDuration span)
            ? Shift(in site, moment, span.TotalNanoseconds, false)
            : site.NoMatchingOverload(arguments);

    // add_duration_timestamp, (duration, timestamp) -> timestamp.
    internal static CelValue AddDurationTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDuration(out CelDuration span) && arguments[1].TryGetTimestamp(out CelTimestamp moment)
            ? Shift(in site, moment, span.TotalNanoseconds, false)
            : site.NoMatchingOverload(arguments);

    // subtract_timestamp_duration, (timestamp, duration) -> timestamp.
    internal static CelValue SubtractTimestampDuration(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp moment) && arguments[1].TryGetDuration(out CelDuration span)
            ? Shift(in site, moment, span.TotalNanoseconds, true)
            : site.NoMatchingOverload(arguments);

    // subtract_timestamp_timestamp, (timestamp, timestamp) -> duration.
    internal static CelValue SubtractTimestampTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimestamp(out CelTimestamp left) || !arguments[1].TryGetTimestamp(out CelTimestamp right))
        {
            return site.NoMatchingOverload(arguments);
        }

        // The seconds differ by less than 3.2 * 10^11, so the product with 10^9 fits 128 bits with room to spare.
        Int128 difference = ((Int128)(left.Seconds - right.Seconds) * NanosecondsPerSecond) + (left.Nanoseconds - right.Nanoseconds);
        return difference < long.MinValue || difference > long.MaxValue
            ? site.Error(CelErrorCode.Overflow, DurationOverflow)
            : CelValue.FromDuration(new CelDuration((long)difference));
    }

    // add_duration_duration, (duration, duration) -> duration.
    internal static CelValue AddDurationDuration(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDuration(out CelDuration left) || !arguments[1].TryGetDuration(out CelDuration right))
        {
            return site.NoMatchingOverload(arguments);
        }

        long a = left.TotalNanoseconds;
        long b = right.TotalNanoseconds;
        long sum = unchecked(a + b);

        // The sum overflows when both operands have the same sign and the sum has the other one.
        return ((a ^ sum) & (b ^ sum)) < 0 ? site.Error(CelErrorCode.Overflow, DurationOverflow) : CelValue.FromDuration(new CelDuration(sum));
    }

    // subtract_duration_duration, (duration, duration) -> duration.
    internal static CelValue SubtractDurationDuration(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDuration(out CelDuration left) || !arguments[1].TryGetDuration(out CelDuration right))
        {
            return site.NoMatchingOverload(arguments);
        }

        long a = left.TotalNanoseconds;
        long b = right.TotalNanoseconds;
        long difference = unchecked(a - b);

        // The difference overflows when the operands have different signs and the difference has the sign of the subtrahend.
        return ((a ^ b) & (a ^ difference)) < 0
            ? site.Error(CelErrorCode.Overflow, DurationOverflow)
            : CelValue.FromDuration(new CelDuration(difference));
    }

    // The moment shifted by the duration forward or back: the duration is d = ds * 10^9 + dn with 0 <= dn < 10^9, the
    // nanoseconds carry or borrow one second, and the seconds are checked against the range of timestamp.
    private static CelValue Shift(in CallSite site, CelTimestamp moment, long nanoseconds, bool subtract)
    {
        long seconds = nanoseconds / NanosecondsPerSecond;
        long remainder = nanoseconds % NanosecondsPerSecond;
        if (remainder < 0)
        {
            remainder += NanosecondsPerSecond;
            seconds--;
        }

        long total;
        long fraction;
        if (subtract)
        {
            fraction = moment.Nanoseconds - remainder;
            long borrow = fraction < 0 ? 1 : 0;
            fraction += borrow * NanosecondsPerSecond;
            total = moment.Seconds - seconds - borrow;
        }
        else
        {
            fraction = moment.Nanoseconds + remainder;
            long carry = fraction >= NanosecondsPerSecond ? 1 : 0;
            fraction -= carry * NanosecondsPerSecond;
            total = moment.Seconds + seconds + carry;
        }

        return total is >= CelTimestamp.MinSeconds and <= CelTimestamp.MaxSeconds
            ? CelValue.FromTimestamp(CelTimestamp.FromChecked(total, (int)fraction))
            : site.Error(CelErrorCode.Overflow, TimestampOverflow);
    }
}

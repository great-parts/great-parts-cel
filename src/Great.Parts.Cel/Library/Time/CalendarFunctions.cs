using System;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The implementations of the set calendar, version 1: the types date and time_of_day, which are the days since 0001-01-01 and the
// ticks of 100 nanoseconds since midnight, as DateOnly and TimeOnly count them. Each overload checks the kinds of its arguments and
// answers EvalNoMatchingOverload for another, whatever the static types say. The text is read and written by CalendarTextRules, the
// calendar is that of CalendarRules, and the time zone of a function is found as for the receivers of a timestamp; nothing here uses a
// culture, a clock, the time zone of the process or the parsing and the formatting of DateOnly and TimeOnly.
internal static class CalendarFunctions
{
    private const string DateName = "date";
    private const string TimeOfDayName = "time_of_day";
    private const string DateOverflow = "date overflow";
    private const string TimestampOverflow = "timestamp overflow";

    // The DayNumber of 1970-01-01, and of 9999-12-31, the last day of the range.
    private const int UnixEpochDayNumber = 719_162;
    private const int MaxDayNumber = 3_652_058;

    private const long TicksPerSecond = CalendarTextRules.TicksPerSecond;
    private const long TicksPerMillisecond = 10_000;
    private const long NanosecondsPerTick = 100;

    // Eighteen hours, more than the largest offset of a time zone, before and after a local time.
    private const long EighteenHours = 18 * 3600;

    // The months of the range of dates, 0001-01 to 9999-12, are 119,988: a larger count of months or years is out of it for any date.
    private const long LargestCount = 120_000;

    // The function is the first part and the text the second; the small number is 0 for a date and 1 for a time of day.
    private static readonly ErrorTemplate CannotParseText = new(
        static error => "invalid argument to '" + (string)error.First! + "': cannot parse " + (string)error.Second! + " as "
            + (error.Small == 0 ? DateName + ", expected YYYY-MM-DD" : TimeOfDayName + ", expected HH:MM, HH:MM:SS or HH:MM:SS.f with up to 7 digits"),
        subject: static error => (string)error.First!);

    // The function is the first part; the second is the year, the month and the day as an array of three numbers.
    private static readonly ErrorTemplate NotADateText = new(
        static error =>
        {
            var parts = (long[])error.Second!;
            return "invalid argument to '" + (string)error.First! + "': the year " + parts[0].ToString(CultureInfo.InvariantCulture)
                + ", the month " + parts[1].ToString(CultureInfo.InvariantCulture) + " and the day " + parts[2].ToString(CultureInfo.InvariantCulture)
                + " are not a date from 0001-01-01 to 9999-12-31";
        },
        subject: static error => (string)error.First!);

    /// <summary>The part of a date that a receiver reads.</summary>
    internal enum DateField
    {
        Year,
        Month,
        DayOfMonthFromOne,
        DayOfMonth,
        DayOfWeek,
        DayOfYear,
    }

    /// <summary>The part of a time of day that a receiver reads.</summary>
    internal enum TimePart
    {
        Hours,
        Minutes,
        Seconds,
        Milliseconds,
    }

    /// <summary>One of the four orderings.</summary>
    internal enum Relation
    {
        Less,
        LessEquals,
        Greater,
        GreaterEquals,
    }

    // A receiver of date: getFullYear, getMonth, getDate, getDayOfMonth, getDayOfWeek, getDayOfYear.
    internal static OverloadImplementation Receiver(DateField field) => new DateReceiver(field).Call;

    // A receiver of time_of_day: getHours, getMinutes, getSeconds, getMilliseconds.
    internal static OverloadImplementation Receiver(TimePart part) => new TimeReceiver(part).Call;

    // addDays, addMonths and addYears: the number of months that one unit of the count is, or 0 for days.
    internal static OverloadImplementation Shift(int monthsPerUnit) => new DateShift(monthsPerUnit).Call;

    // The ordering of two dates, and of two times of day.
    internal static OverloadImplementation DateOrdering(Relation relation) => new DateRelation(relation).Call;

    internal static OverloadImplementation TimeOfDayOrdering(Relation relation) => new TimeOfDayRelation(relation).Call;

    // string_to_date, date(string) -> date.
    internal static CelValue StringToDate(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CalendarTextRules.TryParseDate(text, out DateOnly value)
            ? CelValue.FromDate(value)
            : site.Error(CelErrorCode.InvalidArgument, CannotParseText, site.Function, text);
    }

    // int64_int64_int64_to_date, date(int, int, int) -> date: the year, the month and the day.
    internal static CelValue PartsToDate(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long year) || !arguments[1].TryGetInt64(out long month) || !arguments[2].TryGetInt64(out long day))
        {
            return site.NoMatchingOverload(arguments);
        }

        return year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= CalendarRules.DaysInMonth(year, (int)month)
            ? CelValue.FromDate(new DateOnly((int)year, (int)month, (int)day))
            : site.Error(CelErrorCode.InvalidArgument, NotADateText, site.Function, new[] { year, month, day });
    }

    // timestamp_to_date, date(timestamp) -> date: the date of the instant in UTC.
    internal static CelValue TimestampToDate(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp moment) ? DateAt(in site, moment.Seconds, 0) : site.NoMatchingOverload(arguments);

    // timestamp_to_date_with_tz, date(timestamp, string) -> date: the date of the instant in the time zone.
    internal static CelValue TimestampToDateWithTz(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimestamp(out CelTimestamp moment) || !arguments[1].TryGetString(out string? zone))
        {
            return site.NoMatchingOverload(arguments);
        }

        return TimeReceivers.TryResolveZone(zone, in site, out TimeZoneResolution resolution, out CelValue error)
            ? DateAt(in site, moment.Seconds, resolution.OffsetAt(moment.Seconds))
            : error;
    }

    // date_to_date, date(date) -> date: the argument itself.
    internal static CelValue DateIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].Kind == CelValueKind.Date ? arguments[0] : site.NoMatchingOverload(arguments);

    // string_to_time_of_day, time_of_day(string) -> time_of_day.
    internal static CelValue StringToTimeOfDay(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        return CalendarTextRules.TryParseTimeOfDay(text, out TimeOnly value)
            ? CelValue.FromTimeOfDay(value)
            : site.Error(CelErrorCode.InvalidArgument, CannotParseText, site.Function, text, small: 1);
    }

    // timestamp_to_time_of_day, time_of_day(timestamp) -> time_of_day: the time of day of the instant in UTC.
    internal static CelValue TimestampToTimeOfDay(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetTimestamp(out CelTimestamp moment)
            ? TimeAt(moment.Seconds, moment.Nanoseconds, 0)
            : site.NoMatchingOverload(arguments);

    // timestamp_to_time_of_day_with_tz, time_of_day(timestamp, string) -> time_of_day: the time of day of the instant in the time zone.
    internal static CelValue TimestampToTimeOfDayWithTz(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimestamp(out CelTimestamp moment) || !arguments[1].TryGetString(out string? zone))
        {
            return site.NoMatchingOverload(arguments);
        }

        return TimeReceivers.TryResolveZone(zone, in site, out TimeZoneResolution resolution, out CelValue error)
            ? TimeAt(moment.Seconds, moment.Nanoseconds, resolution.OffsetAt(moment.Seconds))
            : error;
    }

    // time_of_day_to_time_of_day, time_of_day(time_of_day) -> time_of_day: the argument itself.
    internal static CelValue TimeOfDayIdentity(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].Kind == CelValueKind.TimeOfDay ? arguments[0] : site.NoMatchingOverload(arguments);

    // date_to_string, string(date) -> string: YYYY-MM-DD, formed on the stack.
    internal static CelValue DateToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDate(out DateOnly value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[CalendarTextRules.DateLength];
        return CelValue.FromString(new string(text[..CalendarTextRules.WriteDate(value, text)]));
    }

    // time_of_day_to_string, string(time_of_day) -> string: HH:MM:SS and the fraction without the zeros at its end, formed on the stack.
    internal static CelValue TimeOfDayToString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimeOfDay(out TimeOnly value))
        {
            return site.NoMatchingOverload(arguments);
        }

        Span<char> text = stackalloc char[CalendarTextRules.MaxTimeOfDayLength];
        return CelValue.FromString(new string(text[..CalendarTextRules.WriteTimeOfDay(value, text)]));
    }

    // date_time_of_day_to_timestamp, timestamp(date, time_of_day) -> timestamp: the local date and time at the offset 0.
    internal static CelValue DateTimeOfDayToTimestamp(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDate(out DateOnly date) || !arguments[1].TryGetTimeOfDay(out TimeOnly time))
        {
            return site.NoMatchingOverload(arguments);
        }

        return Instant(in site, LocalSeconds(date, time), time);
    }

    // date_time_of_day_to_timestamp_with_tz, timestamp(date, time_of_day, string) -> timestamp: the local date and time in the time zone. The
    // offsets of the zone 18 hours before and after the local time decide it: when the local time exists once, it is that instant; when the zone
    // repeats it, the earlier instant (the offset before the change); when the zone skips it, the local time moved forward by the length of the
    // gap, which is the instant of the offset before the change.
    internal static CelValue DateTimeOfDayToTimestampWithTz(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetDate(out DateOnly date) || !arguments[1].TryGetTimeOfDay(out TimeOnly time) || !arguments[2].TryGetString(out string? zone))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (!TimeReceivers.TryResolveZone(zone, in site, out TimeZoneResolution resolution, out CelValue error))
        {
            return error;
        }

        long local = LocalSeconds(date, time);
        int before = resolution.OffsetAt(local - EighteenHours);
        long first = local - before;
        long instant = first;
        if (resolution.OffsetAt(first) != before)
        {
            int after = resolution.OffsetAt(local + EighteenHours);
            long second = local - after;
            instant = resolution.OffsetAt(second) == after ? second : first;
        }

        return Instant(in site, instant, time);
    }

    // subtract_date_date, _-_(date, date) -> int: the days from the second date to the first.
    internal static CelValue SubtractDates(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetDate(out DateOnly left) && arguments[1].TryGetDate(out DateOnly right)
            ? CelValue.FromInt64(left.DayNumber - right.DayNumber)
            : site.NoMatchingOverload(arguments);

    // subtract_time_of_day_time_of_day, _-_(time_of_day, time_of_day) -> duration: with the sign, never across midnight.
    internal static CelValue SubtractTimesOfDay(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetTimeOfDay(out TimeOnly left) || !arguments[1].TryGetTimeOfDay(out TimeOnly right))
        {
            return site.NoMatchingOverload(arguments);
        }

        // At most 24 hours of ticks: the seconds are far inside the range of a duration.
        _ = CelDuration.TryFromTicks(left.Ticks - right.Ticks, out CelDuration difference);
        return CelValue.FromDuration(difference);
    }

    // The date at an offset of the instant given in Unix seconds; a date before 0001-01-01 or after 9999-12-31, which only an offset can
    // make of an instant at the edge of the range of timestamp, is Overflow.
    private static CelValue DateAt(in CallSite site, long seconds, int offset)
    {
        long dayNumber = CalendarRules.FloorDivide(seconds + offset, CalendarRules.SecondsPerDay) + UnixEpochDayNumber;
        return dayNumber is >= 0 and <= MaxDayNumber
            ? CelValue.FromDate(DateOnly.FromDayNumber((int)dayNumber))
            : site.Error(CelErrorCode.Overflow, DateOverflow);
    }

    // The time of day at an offset of the instant given in Unix seconds and nanoseconds; the nanoseconds below 100 are dropped.
    private static CelValue TimeAt(long seconds, int nanoseconds, int offset)
    {
        long local = seconds + offset;
        long secondOfDay = local - (CalendarRules.FloorDivide(local, CalendarRules.SecondsPerDay) * CalendarRules.SecondsPerDay);
        return CelValue.FromTimeOfDay(new TimeOnly((secondOfDay * TicksPerSecond) + (nanoseconds / NanosecondsPerTick)));
    }

    // The Unix seconds of a local date and time as if the offset were 0; the fraction of the second is not in them.
    private static long LocalSeconds(DateOnly date, TimeOnly time) =>
        ((date.DayNumber - UnixEpochDayNumber) * CalendarRules.SecondsPerDay) + (time.Ticks / TicksPerSecond);

    // The timestamp of the seconds of an instant and the fraction of a time of day; Overflow outside the range of timestamp.
    private static CelValue Instant(in CallSite site, long seconds, TimeOnly time) =>
        seconds is >= CelTimestamp.MinSeconds and <= CelTimestamp.MaxSeconds
            ? CelValue.FromTimestamp(CelTimestamp.FromChecked(seconds, (int)(time.Ticks % TicksPerSecond * NanosecondsPerTick)))
            : site.Error(CelErrorCode.Overflow, TimestampOverflow);

    private sealed class DateReceiver(DateField field)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site)
        {
            if (!arguments[0].TryGetDate(out DateOnly value))
            {
                return site.NoMatchingOverload(arguments);
            }

            long days = value.DayNumber - UnixEpochDayNumber;
            CalendarRules.CivilFromDays(days, out long year, out int month, out int day);
            return CelValue.FromInt64(field switch
            {
                DateField.Year => year,
                DateField.Month => month - 1,
                DateField.DayOfMonthFromOne => day,
                DateField.DayOfMonth => day - 1,
                DateField.DayOfWeek => CalendarRules.DayOfWeek(days),
                _ => CalendarRules.DayOfYear(year, month, day),
            });
        }
    }

    private sealed class TimeReceiver(TimePart part)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site)
        {
            if (!arguments[0].TryGetTimeOfDay(out TimeOnly value))
            {
                return site.NoMatchingOverload(arguments);
            }

            long ticks = value.Ticks;
            return CelValue.FromInt64(part switch
            {
                TimePart.Hours => ticks / (3600 * TicksPerSecond),
                TimePart.Minutes => ticks / (60 * TicksPerSecond) % 60,
                TimePart.Seconds => ticks / TicksPerSecond % 60,
                _ => ticks % TicksPerSecond / TicksPerMillisecond,
            });
        }
    }

    // addDays (0 months for a unit), addMonths (1) and addYears (12): the target in the range of dates, the day of the month kept when the
    // target month has it and otherwise the last day of the month.
    private sealed class DateShift(int monthsPerUnit)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site)
        {
            if (!arguments[0].TryGetDate(out DateOnly value) || !arguments[1].TryGetInt64(out long count))
            {
                return site.NoMatchingOverload(arguments);
            }

            return monthsPerUnit == 0 ? AddDays(in site, value, count) : AddMonths(in site, value, count, monthsPerUnit);
        }

        private static CelValue AddDays(in CallSite site, DateOnly value, long count) =>
            count >= -value.DayNumber && count <= MaxDayNumber - value.DayNumber
                ? CelValue.FromDate(DateOnly.FromDayNumber(value.DayNumber + (int)count))
                : site.Error(CelErrorCode.Overflow, DateOverflow);

        // The month index is the year times 12 plus the zero-based month. The count is checked first, so its product with 12 and the sum
        // cannot overflow a long.
        private static CelValue AddMonths(in CallSite site, DateOnly value, long count, int monthsPerUnit)
        {
            if (count is < -LargestCount or > LargestCount)
            {
                return site.Error(CelErrorCode.Overflow, DateOverflow);
            }

            long index = ((long)value.Year * 12) + (value.Month - 1) + (count * monthsPerUnit);
            long year = CalendarRules.FloorDivide(index, 12);
            if (year is < 1 or > 9999)
            {
                return site.Error(CelErrorCode.Overflow, DateOverflow);
            }

            int month = (int)(index - (year * 12)) + 1;
            return CelValue.FromDate(new DateOnly((int)year, month, Math.Min(value.Day, CalendarRules.DaysInMonth(year, month))));
        }
    }

    private static bool Holds(Relation relation, int comparison) => relation switch
    {
        Relation.Less => comparison < 0,
        Relation.LessEquals => comparison <= 0,
        Relation.Greater => comparison > 0,
        _ => comparison >= 0,
    };

    private sealed class DateRelation(Relation relation)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            arguments[0].TryGetDate(out DateOnly left) && arguments[1].TryGetDate(out DateOnly right)
                ? CelValue.FromBoolean(Holds(relation, left.CompareTo(right)))
                : site.NoMatchingOverload(arguments);
    }

    private sealed class TimeOfDayRelation(Relation relation)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            arguments[0].TryGetTimeOfDay(out TimeOnly left) && arguments[1].TryGetTimeOfDay(out TimeOnly right)
                ? CelValue.FromBoolean(Holds(relation, left.CompareTo(right)))
                : site.NoMatchingOverload(arguments);
    }
}

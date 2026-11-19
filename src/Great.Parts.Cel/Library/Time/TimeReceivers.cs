using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The 24 receivers of timestamp and duration: ten fields of a timestamp without a zone, the same ten with a zone, and the four
// of a duration. Without a zone the offset is 0. With a zone, the second argument is a string that TimeZoneRules resolves
// against the database of the call site for each call, constant or not; the errors of that step come after the kinds of the
// arguments are checked. The fields are those of the local time, not clamped to the range of timestamp.
internal static class TimeReceivers
{
    private const long SecondsPerMinute = 60;
    private const long SecondsPerHour = 3_600;
    private const long NanosecondsPerMillisecond = 1_000_000;

    // The zone string is the first part and the database, or null without one, the second.
    private static readonly ErrorTemplate NotFoundText = new(
        static error =>
        {
            string text = "time zone '" + (string)error.First! + "' is not found";
            if (error.Second is not CelTimeZoneDatabase database)
            {
                return text + ": the environment has no time zone database";
            }

            text += " in the time zone database of the environment";
            return database.Version is { } version ? text + " (version " + version + ")" : text;
        },
        subject: static error => (string)error.First!);

    // The zone string is the first part.
    private static readonly ErrorTemplate InvalidText = new(
        static error => "invalid time zone '" + (string)error.First! + "': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM",
        subject: static error => (string)error.First!);

    // The field of a timestamp that a receiver reads.
    internal enum Field
    {
        Year,
        Month,
        DayOfYear,
        DayOfMonth,
        Date,
        DayOfWeek,
        Hours,
        Minutes,
        Seconds,
        Milliseconds,
    }

    // The field of a duration that a receiver reads.
    internal enum Part
    {
        Hours,
        Minutes,
        Seconds,
        Milliseconds,
    }

    // A receiver of timestamp without a zone: the field of the local time at offset 0.
    internal static OverloadImplementation Timestamp(Field field) => new TimestampReceiver(field).Call;

    // A receiver of timestamp with a zone string.
    internal static OverloadImplementation TimestampInZone(Field field) => new ZonedReceiver(field).Call;

    // A receiver of duration.
    internal static OverloadImplementation Duration(Part part) => new DurationReceiver(part).Call;

    // The value of a field of the instant at an offset. The time fields and the milliseconds are computed from the second of
    // the day alone; the date fields come from the calendar.
    private static long Select(Field field, long seconds, int nanoseconds, int offset)
    {
        if (field == Field.Milliseconds)
        {
            return nanoseconds / NanosecondsPerMillisecond;
        }

        if (field is Field.Hours or Field.Minutes or Field.Seconds)
        {
            long local = seconds + offset;
            long secondOfDay = local - (CalendarRules.FloorDivide(local, CalendarRules.SecondsPerDay) * CalendarRules.SecondsPerDay);
            return field switch
            {
                Field.Hours => secondOfDay / 3_600,
                Field.Minutes => secondOfDay % 3_600 / 60,
                _ => secondOfDay % 60,
            };
        }

        TimeZoneLocalFields fields = TimeZoneLocalFields.Of(seconds, nanoseconds, offset);
        return field switch
        {
            Field.Year => fields.Year,
            Field.Month => fields.Month - 1,
            Field.DayOfYear => fields.DayOfYear,
            Field.DayOfMonth => fields.Day - 1,
            Field.Date => fields.Day,
            _ => fields.DayOfWeek,
        };
    }

    private sealed class TimestampReceiver(Field field)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            arguments[0].TryGetTimestamp(out CelTimestamp moment)
                ? CelValue.FromInt64(Select(field, moment.Seconds, moment.Nanoseconds, 0))
                : site.NoMatchingOverload(arguments);
    }

    private sealed class ZonedReceiver(Field field)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site)
        {
            if (!arguments[0].TryGetTimestamp(out CelTimestamp moment) || !arguments[1].TryGetString(out string? zone))
            {
                return site.NoMatchingOverload(arguments);
            }

            TimeZoneResolution resolution = TimeZoneRules.Resolve(site.TimeZones, zone, site.Meter);
            return resolution.Kind switch
            {
                TimeZoneResolutionKind.NotFound => site.Error(CelErrorCode.TimeZoneNotFound, NotFoundText, zone, site.TimeZones),
                TimeZoneResolutionKind.Invalid => site.Error(CelErrorCode.InvalidTimeZone, InvalidText, zone),
                _ => CelValue.FromInt64(Select(field, moment.Seconds, moment.Nanoseconds, resolution.OffsetAt(moment.Seconds))),
            };
        }
    }

    // The whole hours, minutes and seconds of a duration, truncated toward zero, and its milliseconds within the second, with
    // the sign of the duration.
    private sealed class DurationReceiver(Part part)
    {
        internal CelValue Call(ReadOnlySpan<CelValue> arguments, in CallSite site)
        {
            if (!arguments[0].TryGetDuration(out CelDuration value))
            {
                return site.NoMatchingOverload(arguments);
            }

            return CelValue.FromInt64(part switch
            {
                Part.Hours => value.Seconds / SecondsPerHour,
                Part.Minutes => value.Seconds / SecondsPerMinute,
                Part.Seconds => value.Seconds,
                _ => value.Nanoseconds / NanosecondsPerMillisecond,
            });
        }
    }
}

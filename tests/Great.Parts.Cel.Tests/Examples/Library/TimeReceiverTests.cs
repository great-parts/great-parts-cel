using System.Collections.Generic;
using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The receivers of timestamp and duration, the table of the receivers on the standard library page: the 24 rows, every one by its
// identifier, the forms of a timestamp with and without a time zone (the identifier of the seconds with a
// zone is timestamp_to_seconds_tz, without "with", as in cel-go). Without a zone a timestamp is read in
// UTC. The month, the day of the year and getDayOfMonth count from zero, getDate from one; the day of the
// week 0 is Sunday. Duration has getHours, getMinutes, getSeconds and getMilliseconds: the whole hours,
// minutes and seconds of the duration, dropping the fraction toward zero; getMilliseconds is the part of a
// second in milliseconds, as the language definition says and not the total as cel-go gives it (the
// register entry duration_to_milliseconds, see TimeKnownDifferenceTests). The rows of IANA names are in
// TimeZoneOffsetTests; the zone here is UTC or a fixed offset. The base time 2009-02-13T23:31:30.123456789Z
// is a Friday; in +03:00 it is Saturday 14 February 02:31:30.123456789.
// The local time at the edges of the range of timestamp is not cut: the fields are those of the
// proleptic Gregorian calendar of the moment plus the offset, so the minimum in -05:00 is 0000-12-31
// 19:00:00 (year 0, a leap year, the day of the year 365, a Sunday) and the maximum in +05:00 is
// 10000-01-01 04:59:59.999999999 (a Saturday).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-1")]
[Trait("Requirement", "R-5")]
public sealed class TimeReceiverTests
{
    private const string Rows = """
        timestamp_to_year | timestamp('2009-02-13T23:31:30.123456789Z').getFullYear() | int 2009
        timestamp_to_year_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getFullYear('+03:00') | int 2009
        timestamp_to_year_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getFullYear('UTC') | int 2009
        timestamp_to_month | timestamp('2009-02-13T23:31:30.123456789Z').getMonth() | int 1
        timestamp_to_month_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getMonth('+03:00') | int 1
        timestamp_to_month_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getMonth('UTC') | int 1
        timestamp_to_day_of_year | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfYear() | int 43
        timestamp_to_day_of_year_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfYear('+03:00') | int 44
        timestamp_to_day_of_year_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfYear('UTC') | int 43
        timestamp_to_day_of_month | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfMonth() | int 12
        timestamp_to_day_of_month_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfMonth('+03:00') | int 13
        timestamp_to_day_of_month_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfMonth('UTC') | int 12
        timestamp_to_day_of_month_1_based | timestamp('2009-02-13T23:31:30.123456789Z').getDate() | int 13
        timestamp_to_day_of_month_1_based_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDate('+03:00') | int 14
        timestamp_to_day_of_month_1_based_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDate('UTC') | int 13
        timestamp_to_day_of_week | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfWeek() | int 5
        timestamp_to_day_of_week_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfWeek('+03:00') | int 6
        timestamp_to_day_of_week_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getDayOfWeek('UTC') | int 5
        timestamp_to_hours | timestamp('2009-02-13T23:31:30.123456789Z').getHours() | int 23
        timestamp_to_hours_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getHours('+03:00') | int 2
        timestamp_to_hours_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getHours('UTC') | int 23
        timestamp_to_minutes | timestamp('2009-02-13T23:31:30.123456789Z').getMinutes() | int 31
        timestamp_to_minutes_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getMinutes('+03:00') | int 31
        timestamp_to_minutes_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getMinutes('UTC') | int 31
        timestamp_to_seconds | timestamp('2009-02-13T23:31:30.123456789Z').getSeconds() | int 30
        timestamp_to_seconds_tz | timestamp('2009-02-13T23:31:30.123456789Z').getSeconds('+03:00') | int 30
        timestamp_to_seconds_tz | timestamp('2009-02-13T23:31:30.123456789Z').getSeconds('UTC') | int 30
        timestamp_to_milliseconds | timestamp('2009-02-13T23:31:30.123456789Z').getMilliseconds() | int 123
        timestamp_to_milliseconds_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getMilliseconds('+03:00') | int 123
        timestamp_to_milliseconds_with_tz | timestamp('2009-02-13T23:31:30.123456789Z').getMilliseconds('UTC') | int 123
        timestamp_to_day_of_year | timestamp('2024-12-31T00:00:00Z').getDayOfYear() | int 365
        timestamp_to_day_of_year | timestamp('2023-12-31T00:00:00Z').getDayOfYear() | int 364
        timestamp_to_day_of_year | timestamp('1900-03-01T00:00:00Z').getDayOfYear() | int 59
        timestamp_to_day_of_year | timestamp('2000-03-01T00:00:00Z').getDayOfYear() | int 60
        timestamp_to_day_of_month | timestamp('2000-02-29T12:00:00Z').getDayOfMonth() | int 28
        timestamp_to_day_of_month_1_based | timestamp('2000-02-29T12:00:00Z').getDate() | int 29
        timestamp_to_day_of_month_1_based_with_tz | timestamp('2009-02-28T23:59:59Z').getDate('+00:01') | int 1
        timestamp_to_year | timestamp('1969-12-31T23:59:59.999999999Z').getFullYear() | int 1969
        timestamp_to_month | timestamp('1969-12-31T23:59:59.999999999Z').getMonth() | int 11
        timestamp_to_day_of_year | timestamp('1969-12-31T23:59:59.999999999Z').getDayOfYear() | int 364
        timestamp_to_day_of_week | timestamp('1969-12-31T23:59:59.999999999Z').getDayOfWeek() | int 3
        timestamp_to_seconds | timestamp('1969-12-31T23:59:59.999999999Z').getSeconds() | int 59
        timestamp_to_milliseconds | timestamp('1969-12-31T23:59:59.999999999Z').getMilliseconds() | int 999
        timestamp_to_milliseconds | timestamp('2009-02-13T23:31:30.000999999Z').getMilliseconds() | int 0
        timestamp_to_year | timestamp('0001-01-01T00:00:00Z').getFullYear() | int 1
        timestamp_to_day_of_week | timestamp('0001-01-01T00:00:00Z').getDayOfWeek() | int 1
        timestamp_to_year | timestamp('9999-12-31T23:59:59.999999999Z').getFullYear() | int 9999
        timestamp_to_month | timestamp('9999-12-31T23:59:59.999999999Z').getMonth() | int 11
        timestamp_to_day_of_year | timestamp('9999-12-31T23:59:59.999999999Z').getDayOfYear() | int 364
        timestamp_to_day_of_month | timestamp('9999-12-31T23:59:59.999999999Z').getDayOfMonth() | int 30
        timestamp_to_day_of_month_1_based | timestamp('9999-12-31T23:59:59.999999999Z').getDate() | int 31
        timestamp_to_day_of_week | timestamp('9999-12-31T23:59:59.999999999Z').getDayOfWeek() | int 5
        timestamp_to_milliseconds | timestamp('9999-12-31T23:59:59.999999999Z').getMilliseconds() | int 999
        timestamp_to_year_with_tz | timestamp('0001-01-01T00:00:00Z').getFullYear('-05:00') | int 0
        timestamp_to_month_with_tz | timestamp('0001-01-01T00:00:00Z').getMonth('-05:00') | int 11
        timestamp_to_day_of_year_with_tz | timestamp('0001-01-01T00:00:00Z').getDayOfYear('-05:00') | int 365
        timestamp_to_day_of_month_with_tz | timestamp('0001-01-01T00:00:00Z').getDayOfMonth('-05:00') | int 30
        timestamp_to_day_of_month_1_based_with_tz | timestamp('0001-01-01T00:00:00Z').getDate('-05:00') | int 31
        timestamp_to_day_of_week_with_tz | timestamp('0001-01-01T00:00:00Z').getDayOfWeek('-05:00') | int 0
        timestamp_to_hours_with_tz | timestamp('0001-01-01T00:00:00Z').getHours('-05:00') | int 19
        timestamp_to_minutes_with_tz | timestamp('0001-01-01T00:00:00Z').getMinutes('-05:00') | int 0
        timestamp_to_seconds_tz | timestamp('0001-01-01T00:00:00Z').getSeconds('-05:00') | int 0
        timestamp_to_milliseconds_with_tz | timestamp('0001-01-01T00:00:00Z').getMilliseconds('-05:00') | int 0
        timestamp_to_year_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getFullYear('+05:00') | int 10000
        timestamp_to_month_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getMonth('+05:00') | int 0
        timestamp_to_day_of_year_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getDayOfYear('+05:00') | int 0
        timestamp_to_day_of_month_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getDayOfMonth('+05:00') | int 0
        timestamp_to_day_of_month_1_based_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getDate('+05:00') | int 1
        timestamp_to_day_of_week_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getDayOfWeek('+05:00') | int 6
        timestamp_to_hours_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getHours('+05:00') | int 4
        timestamp_to_minutes_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getMinutes('+05:00') | int 59
        timestamp_to_seconds_tz | timestamp('9999-12-31T23:59:59.999999999Z').getSeconds('+05:00') | int 59
        timestamp_to_milliseconds_with_tz | timestamp('9999-12-31T23:59:59.999999999Z').getMilliseconds('+05:00') | int 999
        timestamp_to_hours_with_tz | timestamp('2009-02-13T23:31:30Z').getHours('-05:00') | int 18
        timestamp_to_hours_with_tz | timestamp('2009-02-13T23:31:30Z').getHours('02:00') | int 1
        timestamp_to_day_of_month_1_based_with_tz | timestamp('2009-02-13T23:31:30Z').getDate('-23:59') | int 12
        timestamp_to_minutes_with_tz | timestamp('2009-02-13T23:31:30Z').getMinutes('-23:59') | int 32
        timestamp_to_minutes_with_tz | timestamp('2009-02-13T23:31:30Z').getMinutes('+23:59') | int 30
        duration_to_hours | duration('3723.456s').getHours() | int 1
        duration_to_minutes | duration('3723.456s').getMinutes() | int 62
        duration_to_seconds | duration('3723.456s').getSeconds() | int 3723
        duration_to_milliseconds | duration('3723.456s').getMilliseconds() | int 456
        duration_to_hours | duration('-3723.456s').getHours() | int -1
        duration_to_minutes | duration('-3723.456s').getMinutes() | int -62
        duration_to_seconds | duration('-3723.456s').getSeconds() | int -3723
        duration_to_hours | duration('0s').getHours() | int 0
        duration_to_hours | duration('59m59.999s').getHours() | int 0
        duration_to_hours | duration('-59m').getHours() | int 0
        duration_to_hours | duration('-61m').getHours() | int -1
        duration_to_hours | duration('36h').getHours() | int 36
        duration_to_minutes | duration('59.999s').getMinutes() | int 0
        duration_to_seconds | duration('-0.5s').getSeconds() | int 0
        duration_to_seconds | duration('1.999999999s').getSeconds() | int 1
        duration_to_milliseconds | duration('1.234s').getMilliseconds() | int 234
        duration_to_milliseconds | duration('2s').getMilliseconds() | int 0
        duration_to_milliseconds | duration('1.0005s').getMilliseconds() | int 0
        duration_to_milliseconds | duration('0.999999999s').getMilliseconds() | int 999
        duration_to_hours | duration('9223372036854775807ns').getHours() | int 2562047
        duration_to_minutes | duration('9223372036854775807ns').getMinutes() | int 153722867
        duration_to_seconds | duration('-9223372036854775808ns').getSeconds() | int -9223372036
        duration_to_hours | duration('-9223372036854775808ns').getHours() | int -2562047
        """;

    private static readonly string[] Identifiers =
    [
        "duration_to_hours",
        "duration_to_milliseconds",
        "duration_to_minutes",
        "duration_to_seconds",
        "timestamp_to_day_of_month",
        "timestamp_to_day_of_month_1_based",
        "timestamp_to_day_of_month_1_based_with_tz",
        "timestamp_to_day_of_month_with_tz",
        "timestamp_to_day_of_week",
        "timestamp_to_day_of_week_with_tz",
        "timestamp_to_day_of_year",
        "timestamp_to_day_of_year_with_tz",
        "timestamp_to_hours",
        "timestamp_to_hours_with_tz",
        "timestamp_to_milliseconds",
        "timestamp_to_milliseconds_with_tz",
        "timestamp_to_minutes",
        "timestamp_to_minutes_with_tz",
        "timestamp_to_month",
        "timestamp_to_month_with_tz",
        "timestamp_to_seconds",
        "timestamp_to_seconds_tz",
        "timestamp_to_year",
        "timestamp_to_year_with_tz",
    ];

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows, labelColumns: 1));
    }

    [Fact]
    public void EveryReceiverOfTheTable_HasAnExample()
    {
        var expected = new SortedSet<string>(Identifiers, System.StringComparer.Ordinal);

        Assert.Equal(24, expected.Count);
        Assert.Equal(expected, TimeRows.Labels(Rows));
    }
}

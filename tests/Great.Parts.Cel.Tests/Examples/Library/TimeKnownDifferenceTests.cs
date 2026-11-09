using System.Collections.Generic;
using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The 13 entries of the register of known differences with cel-go that belong to time, each with the
// minimal case of the entry and the outcome of Great.Parts.Cel that the entry states (the outcome of
// cel-go is in the entry, and the differential run takes it from the oracle). A row is
// "signature | expression | outcome" in the form of TimeRows; the signature is that of the register.
//   duration_to_milliseconds   duration('1.5s').getMilliseconds() is 500, the milliseconds of the second,
//                              as the language definition and the cel-spec case get_milliseconds require;
//                              cel-go gives 1500.
//   duration_to_string         string(duration('9223372036.854775807s')) keeps all the digits; cel-go
//                              formats the seconds through a float and gives 9223372036.854776s.
//   timestamp_to_string        string(timestamp('2023-01-01T00:00:00-07:00')) is the moment in UTC with Z,
//                              2023-01-01T07:00:00Z; cel-go keeps the offset of the string.
//   the ten receivers with a zone: the zone "" and the zone Local are TimeZoneNotFound (the library never
//     reads the zone of the process, and "" is not a name), the offset +1:5 is InvalidTimeZone (an offset
//     is [+-]HH:MM); cel-go gives a value for each (UTC, the zone of the process, +01:05). The cases are
//     one per entry in the register; here each receiver has the three cases.
// The database is that of TimeZoneData, version 2026a.
[Trait("Requirement", "R-6")]
[Trait("Requirement", "R-5")]
[Trait("Requirement", "F-1")]
public sealed class TimeKnownDifferenceTests
{
    private const string Rows = """
        duration_to_milliseconds(duration) value-mismatch | duration('1.5s').getMilliseconds() | int 500
        duration_to_string(duration) value-mismatch | string(duration('9223372036.854775807s')) | str 9223372036.854775807s
        timestamp_to_string(timestamp) value-mismatch | string(timestamp('2023-01-01T00:00:00-07:00')) | str 2023-01-01T07:00:00Z
        timestamp_to_year_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getFullYear('') | error TimeZoneNotFound 45: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_year_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getFullYear('Local') | error TimeZoneNotFound 45: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_year_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getFullYear('+1:5') | error InvalidTimeZone 45: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_month_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMonth('') | error TimeZoneNotFound 42: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_month_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMonth('Local') | error TimeZoneNotFound 42: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_month_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMonth('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_day_of_year_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfYear('') | error TimeZoneNotFound 46: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_year_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfYear('Local') | error TimeZoneNotFound 46: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_year_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfYear('+1:5') | error InvalidTimeZone 46: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_day_of_month_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfMonth('') | error TimeZoneNotFound 47: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_month_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfMonth('Local') | error TimeZoneNotFound 47: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_month_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfMonth('+1:5') | error InvalidTimeZone 47: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_day_of_month_1_based_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDate('') | error TimeZoneNotFound 41: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_month_1_based_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDate('Local') | error TimeZoneNotFound 41: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_month_1_based_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDate('+1:5') | error InvalidTimeZone 41: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_day_of_week_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfWeek('') | error TimeZoneNotFound 46: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_week_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfWeek('Local') | error TimeZoneNotFound 46: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_day_of_week_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getDayOfWeek('+1:5') | error InvalidTimeZone 46: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_hours_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getHours('') | error TimeZoneNotFound 42: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_hours_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getHours('Local') | error TimeZoneNotFound 42: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_hours_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getHours('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_minutes_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMinutes('') | error TimeZoneNotFound 44: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_minutes_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMinutes('Local') | error TimeZoneNotFound 44: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_minutes_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMinutes('+1:5') | error InvalidTimeZone 44: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_seconds_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getSeconds('') | error TimeZoneNotFound 44: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_seconds_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getSeconds('Local') | error TimeZoneNotFound 44: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_seconds_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getSeconds('+1:5') | error InvalidTimeZone 44: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp_to_milliseconds_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMilliseconds('') | error TimeZoneNotFound 49: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_milliseconds_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMilliseconds('Local') | error TimeZoneNotFound 49: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp_to_milliseconds_with_tz(timestamp, string) we-error | timestamp('2023-01-01T00:00:00Z').getMilliseconds('+1:5') | error InvalidTimeZone 49: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        """;

    private static readonly string[] Signatures =
    [
        "duration_to_milliseconds(duration) value-mismatch",
        "duration_to_string(duration) value-mismatch",
        "timestamp_to_day_of_month_1_based_with_tz(timestamp, string) we-error",
        "timestamp_to_day_of_month_with_tz(timestamp, string) we-error",
        "timestamp_to_day_of_week_with_tz(timestamp, string) we-error",
        "timestamp_to_day_of_year_with_tz(timestamp, string) we-error",
        "timestamp_to_hours_with_tz(timestamp, string) we-error",
        "timestamp_to_milliseconds_with_tz(timestamp, string) we-error",
        "timestamp_to_minutes_with_tz(timestamp, string) we-error",
        "timestamp_to_month_with_tz(timestamp, string) we-error",
        "timestamp_to_seconds_tz(timestamp, string) we-error",
        "timestamp_to_string(timestamp) value-mismatch",
        "timestamp_to_year_with_tz(timestamp, string) we-error",
    ];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(TimeZoneData.Database())
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsTheOutcomeOfTheEntry()
    {
        Assert.Empty(TimeRows.Check(environment, Rows, labelColumns: 1));
    }

    [Fact]
    public void EveryEntryOfTheRegister_HasAnExample()
    {
        var expected = new SortedSet<string>(Signatures, System.StringComparer.Ordinal);

        Assert.Equal(13, expected.Count);
        Assert.Equal(expected, TimeRows.Labels(Rows));
    }
}

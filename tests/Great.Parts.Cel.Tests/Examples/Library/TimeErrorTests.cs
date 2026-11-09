using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The evaluation errors of time, each with its text and place; a row is "expression | outcome" in the form
// of TimeRows (the error: code, offset of the place, message; the expressions are on one line, so the line
// is 1 and the column the offset plus 1). The place of a call is the operator, or the "(" after the name; of
// a receiver call too.
//   Overflow (3006): "{kind} overflow", the kind "timestamp" or "duration"; the sum and the difference of
//     moments and durations outside the range, the difference of two moments outside int64 nanoseconds.
//   ConversionRange (3007): "{value} is out of the range of {targetType}": timestamp(int) outside the
//     seconds -62135596800 to 253402300799; a string of time that parses but is outside the range of
//     the type. {value} of a string is its text, of an int the number; the target type is written
//     "timestamp" and "duration", as the kind of Overflow (the name of the type of time in messages).
//   InvalidArgument (3008): "invalid argument to '{function}': {detail}", the function the name of the
//     call; the detail is the library's own text, so the rows state the start of the message.
//   TimeZoneNotFound (3022): "time zone '{zone}' is not found in the time zone database of the
//     environment (version {version})"; the database here is that of TimeZoneData, version 2026a.
//   InvalidTimeZone (3023): "invalid time zone '{zone}': expected 'UTC', an IANA time zone identifier
//     or a fixed offset [+-]HH:MM".
// Each of the ten receivers with a zone has a row of 3022 and one of 3023.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-4")]
[Trait("Requirement", "F-8")]
[Trait("Requirement", "R-5")]
public sealed class TimeErrorTests
{
    private const string Rows = """
        timestamp('9999-12-31T23:59:59.999999999Z') + duration('1ns') | error Overflow 44: timestamp overflow
        duration('1ns') + timestamp('9999-12-31T23:59:59.999999999Z') | error Overflow 16: timestamp overflow
        timestamp('9999-12-31T23:59:59Z') + duration('9223372036854775807ns') | error Overflow 34: timestamp overflow
        timestamp('0001-01-01T00:00:00Z') - duration('1ns') | error Overflow 34: timestamp overflow
        timestamp('0001-01-01T00:00:00Z') - duration('9223372036854775807ns') | error Overflow 34: timestamp overflow
        timestamp('0001-01-01T00:00:00Z') + duration('-1ns') | error Overflow 34: timestamp overflow
        timestamp('9999-12-31T23:59:59Z') - duration('-9223372036854775808ns') | error Overflow 34: timestamp overflow
        duration('9223372036854775807ns') + duration('1ns') | error Overflow 34: duration overflow
        duration('-9223372036854775808ns') + duration('-1ns') | error Overflow 35: duration overflow
        duration('-9223372036854775808ns') - duration('1ns') | error Overflow 35: duration overflow
        duration('9223372036854775807ns') - duration('-1ns') | error Overflow 34: duration overflow
        duration('0s') - duration('-9223372036854775808ns') | error Overflow 15: duration overflow
        timestamp('9999-12-31T23:59:59Z') - timestamp('0001-01-01T00:00:00Z') | error Overflow 34: duration overflow
        timestamp('0001-01-01T00:00:00Z') - timestamp('9999-12-31T23:59:59Z') | error Overflow 34: duration overflow
        timestamp('2262-04-11T23:47:16.854775808Z') - timestamp('1970-01-01T00:00:00Z') | error Overflow 44: duration overflow
        (timestamp('9999-12-31T23:59:59.999999999Z') + duration('1ns')).getFullYear() | error Overflow 45: timestamp overflow
        timestamp(253402300800) | error ConversionRange 9: 253402300800 is out of the range of timestamp
        timestamp(-62135596801) | error ConversionRange 9: -62135596801 is out of the range of timestamp
        timestamp(9223372036854775807) | error ConversionRange 9: 9223372036854775807 is out of the range of timestamp
        timestamp(-9223372036854775808) | error ConversionRange 9: -9223372036854775808 is out of the range of timestamp
        1 + timestamp(253402300800).getFullYear() | error ConversionRange 13: 253402300800 is out of the range of timestamp
        timestamp('0000-01-01T00:00:00Z') | error ConversionRange 9: 0000-01-01T00:00:00Z is out of the range of timestamp
        timestamp('0001-01-01T00:00:00+01:00') | error ConversionRange 9: 0001-01-01T00:00:00+01:00 is out of the range of timestamp
        timestamp('9999-12-31T23:59:59-01:00') | error ConversionRange 9: 9999-12-31T23:59:59-01:00 is out of the range of timestamp
        duration('9223372036854775808ns') | error ConversionRange 8: 9223372036854775808ns is out of the range of duration
        duration('2562048h') | error ConversionRange 8: 2562048h is out of the range of duration
        duration('9223372036.854775808s') | error ConversionRange 8: 9223372036.854775808s is out of the range of duration
        duration('-9223372036854775809ns') | error ConversionRange 8: -9223372036854775809ns is out of the range of duration
        timestamp('2023-02-30T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        timestamp('') | error InvalidArgument 9: invalid argument to 'timestamp': *
        duration('1H') | error InvalidArgument 8: invalid argument to 'duration': *
        duration('') | error InvalidArgument 8: invalid argument to 'duration': *
        1 + timestamp('x').getFullYear() | error InvalidArgument 13: invalid argument to 'timestamp': *
        timestamp('2009-02-13T23:31:30Z').getFullYear('Mars/Base') | error TimeZoneNotFound 45: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getMonth('Mars/Base') | error TimeZoneNotFound 42: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getDayOfYear('Mars/Base') | error TimeZoneNotFound 46: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getDayOfMonth('Mars/Base') | error TimeZoneNotFound 47: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getDate('Mars/Base') | error TimeZoneNotFound 41: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getDayOfWeek('Mars/Base') | error TimeZoneNotFound 46: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Mars/Base') | error TimeZoneNotFound 42: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getMinutes('Mars/Base') | error TimeZoneNotFound 44: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getSeconds('Mars/Base') | error TimeZoneNotFound 44: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getMilliseconds('Mars/Base') | error TimeZoneNotFound 49: time zone 'Mars/Base' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getFullYear('+1:5') | error InvalidTimeZone 45: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getMonth('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getDayOfYear('+1:5') | error InvalidTimeZone 46: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getDayOfMonth('+1:5') | error InvalidTimeZone 47: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getDate('+1:5') | error InvalidTimeZone 41: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getDayOfWeek('+1:5') | error InvalidTimeZone 46: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getMinutes('+1:5') | error InvalidTimeZone 44: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getSeconds('+1:5') | error InvalidTimeZone 44: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getMilliseconds('+1:5') | error InvalidTimeZone 49: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(TimeZoneData.Database())
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsErrorAtItsPlace()
    {
        Assert.Empty(TimeRows.Check(environment, Rows));
    }
}

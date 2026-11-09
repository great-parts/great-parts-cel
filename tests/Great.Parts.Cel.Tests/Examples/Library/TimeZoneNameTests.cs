using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The name of a time zone of the receivers of timestamp, the table of the zone names on the standard library page. UTC is built in
// and needs no database. [+-]?HH:MM is a fixed offset, hours 00 to 23 and minutes 00 to 59, without a sign
// a plus, and needs no database either. Any other string is an IANA identifier: an exact ordinal match
// with an identifier of the database of the environment, the case counts. A string with ":" that is
// not an offset is InvalidTimeZone; a name that is not in the database, the empty string and "Local"
// among them, or any name when the environment has no database, is TimeZoneNotFound. Both errors come at
// evaluation, at the call (the "(" after the name), also for a constant. The messages are those of the
// table of error codes; the version of the database in the message is the one the application gave: 2026a.
// The base time is 2009-02-13T23:31:30Z. The database is that of TimeZoneData (version 2026a).
// The cases the library does not take the way cel-go does are registry entries ("", Local, +1:5,
// +001:30), whose examples are in TimeKnownDifferenceTests; the case of the name is not an entry (the
// behavior of cel-go there depends on the file system of its database).
[Trait("Requirement", "R-5")]
[Trait("Requirement", "+R-4")]
public sealed class TimeZoneNameTests
{
    private const string Rows = """
        timestamp('2009-02-13T23:31:30Z').getHours('UTC') | int 23
        timestamp('2009-02-13T23:31:30Z').getHours('+03:00') | int 2
        timestamp('2009-02-13T23:31:30Z').getMinutes('+03:00') | int 31
        timestamp('2009-02-13T23:31:30Z').getDate('+03:00') | int 14
        timestamp('2009-02-13T23:31:30Z').getHours('-03:00') | int 20
        timestamp('2009-02-13T23:31:30Z').getMinutes('-03:00') | int 31
        timestamp('2009-02-13T23:31:30Z').getDate('-03:00') | int 13
        timestamp('2009-02-13T23:31:30Z').getHours('03:00') | int 2
        timestamp('2009-02-13T23:31:30Z').getMinutes('03:00') | int 31
        timestamp('2009-02-13T23:31:30Z').getDate('03:00') | int 14
        timestamp('2009-02-13T23:31:30Z').getHours('+00:00') | int 23
        timestamp('2009-02-13T23:31:30Z').getMinutes('+00:00') | int 31
        timestamp('2009-02-13T23:31:30Z').getDate('+00:00') | int 13
        timestamp('2009-02-13T23:31:30Z').getHours('-00:00') | int 23
        timestamp('2009-02-13T23:31:30Z').getMinutes('-00:00') | int 31
        timestamp('2009-02-13T23:31:30Z').getDate('-00:00') | int 13
        timestamp('2009-02-13T23:31:30Z').getHours('+05:30') | int 5
        timestamp('2009-02-13T23:31:30Z').getMinutes('+05:30') | int 1
        timestamp('2009-02-13T23:31:30Z').getDate('+05:30') | int 14
        timestamp('2009-02-13T23:31:30Z').getHours('+23:59') | int 23
        timestamp('2009-02-13T23:31:30Z').getMinutes('+23:59') | int 30
        timestamp('2009-02-13T23:31:30Z').getDate('+23:59') | int 14
        timestamp('2009-02-13T23:31:30Z').getHours('-23:59') | int 23
        timestamp('2009-02-13T23:31:30Z').getMinutes('-23:59') | int 32
        timestamp('2009-02-13T23:31:30Z').getDate('-23:59') | int 12
        timestamp('2009-02-13T23:31:30Z').getHours('00:00') | int 23
        timestamp('2009-02-13T23:31:30Z').getMinutes('00:00') | int 31
        timestamp('2009-02-13T23:31:30Z').getDate('00:00') | int 13
        timestamp('2009-02-13T23:31:30Z').getHours('+1:5') | error InvalidTimeZone 42: invalid time zone '+1:5': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+001:30') | error InvalidTimeZone 42: invalid time zone '+001:30': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+24:00') | error InvalidTimeZone 42: invalid time zone '+24:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('24:00') | error InvalidTimeZone 42: invalid time zone '24:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+03:60') | error InvalidTimeZone 42: invalid time zone '+03:60': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+3:00') | error InvalidTimeZone 42: invalid time zone '+3:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('03:0') | error InvalidTimeZone 42: invalid time zone '03:0': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+03:00:00') | error InvalidTimeZone 42: invalid time zone '+03:00:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours(':') | error InvalidTimeZone 42: invalid time zone ':': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+03:00 ') | error InvalidTimeZone 42: invalid time zone '+03:00 ': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours(' +03:00') | error InvalidTimeZone 42: invalid time zone ' +03:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('Europe:Moscow') | error InvalidTimeZone 42: invalid time zone 'Europe:Moscow': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('UTC:00') | error InvalidTimeZone 42: invalid time zone 'UTC:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+03:') | error InvalidTimeZone 42: invalid time zone '+03:': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours(':30') | error InvalidTimeZone 42: invalid time zone ':30': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('+-3:00') | error InvalidTimeZone 42: invalid time zone '+-3:00': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM
        timestamp('2009-02-13T23:31:30Z').getHours('') | error TimeZoneNotFound 42: time zone '' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Local') | error TimeZoneNotFound 42: time zone 'Local' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('utc') | error TimeZoneNotFound 42: time zone 'utc' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Utc') | error TimeZoneNotFound 42: time zone 'Utc' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('UTC ') | error TimeZoneNotFound 42: time zone 'UTC ' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Z') | error TimeZoneNotFound 42: time zone 'Z' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('GMT') | error TimeZoneNotFound 42: time zone 'GMT' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Europe/Moscow') | error TimeZoneNotFound 42: time zone 'Europe/Moscow' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('test/rule') | error TimeZoneNotFound 42: time zone 'test/rule' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('TEST/RULE') | error TimeZoneNotFound 42: time zone 'TEST/RULE' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Test/rule') | error TimeZoneNotFound 42: time zone 'Test/rule' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Rule ') | error TimeZoneNotFound 42: time zone 'Test/Rule ' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('+0300') | error TimeZoneNotFound 42: time zone '+0300' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('+3') | error TimeZoneNotFound 42: time zone '+3' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('03') | error TimeZoneNotFound 42: time zone '03' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('UTC+3') | error TimeZoneNotFound 42: time zone 'UTC+3' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Missing') | error TimeZoneNotFound 42: time zone 'Test/Missing' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Test') | error TimeZoneNotFound 42: time zone 'Test' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('+03;00') | error TimeZoneNotFound 42: time zone '+03;00' is not found in the time zone database of the environment (version 2026a)
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Rule') | int 18
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Fixed') | int 5
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Link') | int 5
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Moved') | int 2
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(TimeZoneData.Database())
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows));
    }
}

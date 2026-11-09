using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The offset of a time zone of the database at a moment: the block of TZif of the version 2 and later,
// the type 0 before the first transition, the transitions by their moments, the rule of the footer
// (POSIX TZ, RFC 9636) after the last one, a precision of a second, the library's own proleptic Gregorian
// calendar. The zones are those of TimeZoneData, and the comment of that file gives the data of each
// (the rules of the forms Mm.w.d, Jn and n, a negative rule time, a rule time over 24 hours). A row is
// the receiver at the moment given as a UTC string in the zone of the name: the instants of the rows with
// a transition are the second before it and the moment itself.
// The cel-go oracle reads the same bytes from zoneinfo.zip; the run of all zones of the file against Go is
// a nightly run (the profile of the differential run), not of these rows.
[Trait("Requirement", "R-5")]
[Trait("Requirement", "+R-4")]
public sealed class TimeZoneOffsetTests
{
    private const string Rows = """
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Fixed') | int 5
        timestamp('2009-02-13T23:31:30Z').getMinutes('Test/Fixed') | int 16
        timestamp('2009-02-13T23:31:30Z').getDate('Test/Fixed') | int 14
        timestamp('2009-02-13T23:31:30Z').getDayOfWeek('Test/Fixed') | int 6
        timestamp('2009-02-13T23:31:30Z').getHours('Test/Link') | int 5
        timestamp('2009-02-13T23:31:30Z').getMinutes('Test/Link') | int 16
        timestamp('1969-12-31T23:59:59Z').getHours('Test/Rule') | int 19
        timestamp('1969-12-31T23:59:59Z').getMinutes('Test/Rule') | int 3
        timestamp('1969-12-31T23:59:59Z').getSeconds('Test/Rule') | int 57
        timestamp('1969-12-31T23:59:59Z').getDate('Test/Rule') | int 31
        timestamp('1969-12-31T23:59:59Z').getMonth('Test/Rule') | int 11
        timestamp('1969-12-31T23:59:59Z').getFullYear('Test/Rule') | int 1969
        timestamp('1970-01-01T00:00:00Z').getHours('Test/Rule') | int 19
        timestamp('1970-01-01T00:00:00Z').getMinutes('Test/Rule') | int 0
        timestamp('1970-01-01T00:00:00Z').getDate('Test/Rule') | int 31
        timestamp('1970-01-01T00:00:00Z').getFullYear('Test/Rule') | int 1969
        timestamp('0001-01-02T00:00:00Z').getHours('Test/Rule') | int 19
        timestamp('0001-01-02T00:00:00Z').getMinutes('Test/Rule') | int 3
        timestamp('0001-01-02T00:00:00Z').getSeconds('Test/Rule') | int 58
        timestamp('0001-01-02T00:00:00Z').getDate('Test/Rule') | int 1
        timestamp('0001-01-02T00:00:00Z').getFullYear('Test/Rule') | int 1
        timestamp('2023-01-15T12:00:00Z').getHours('Test/Rule') | int 7
        timestamp('2023-07-15T12:00:00Z').getHours('Test/Rule') | int 8
        timestamp('2023-07-15T12:00:00Z').getDayOfWeek('Test/Rule') | int 6
        timestamp('2023-07-15T12:00:00Z').getDayOfYear('Test/Rule') | int 195
        timestamp('2023-03-12T06:59:59Z').getHours('Test/Rule') | int 1
        timestamp('2023-03-12T06:59:59Z').getMinutes('Test/Rule') | int 59
        timestamp('2023-03-12T06:59:59Z').getSeconds('Test/Rule') | int 59
        timestamp('2023-03-12T06:59:59Z').getDate('Test/Rule') | int 12
        timestamp('2023-03-12T06:59:59Z').getDayOfWeek('Test/Rule') | int 0
        timestamp('2023-03-12T07:00:00Z').getHours('Test/Rule') | int 3
        timestamp('2023-03-12T07:00:00Z').getMinutes('Test/Rule') | int 0
        timestamp('2023-03-12T07:00:00Z').getDayOfYear('Test/Rule') | int 70
        timestamp('2023-11-05T05:59:59Z').getHours('Test/Rule') | int 1
        timestamp('2023-11-05T05:59:59Z').getMinutes('Test/Rule') | int 59
        timestamp('2023-11-05T05:59:59Z').getDate('Test/Rule') | int 5
        timestamp('2023-11-05T06:00:00Z').getHours('Test/Rule') | int 1
        timestamp('2023-11-05T06:00:00Z').getMinutes('Test/Rule') | int 0
        timestamp('2023-11-05T06:00:00Z').getDate('Test/Rule') | int 5
        timestamp('2400-07-01T12:00:00Z').getHours('Test/Rule') | int 8
        timestamp('9999-12-31T23:59:59Z').getHours('Test/Rule') | int 18
        timestamp('9999-12-31T23:59:59Z').getMinutes('Test/Rule') | int 59
        timestamp('9999-12-31T23:59:59Z').getSeconds('Test/Rule') | int 59
        timestamp('9999-12-31T23:59:59Z').getDate('Test/Rule') | int 31
        timestamp('9999-12-31T23:59:59Z').getFullYear('Test/Rule') | int 9999
        timestamp('1999-12-31T23:59:59Z').getHours('Test/Moved') | int 2
        timestamp('1999-12-31T23:59:59Z').getMinutes('Test/Moved') | int 29
        timestamp('1999-12-31T23:59:59Z').getFullYear('Test/Moved') | int 2000
        timestamp('1999-12-31T23:59:59Z').getDate('Test/Moved') | int 1
        timestamp('2000-01-01T00:00:00Z').getHours('Test/Moved') | int 3
        timestamp('2005-06-15T12:00:00Z').getHours('Test/Moved') | int 15
        timestamp('2009-12-31T23:59:59Z').getHours('Test/Moved') | int 2
        timestamp('2009-12-31T23:59:59Z').getFullYear('Test/Moved') | int 2010
        timestamp('2010-01-01T00:00:00Z').getHours('Test/Moved') | int 4
        timestamp('2050-01-01T00:00:00Z').getHours('Test/Moved') | int 4
        timestamp('2023-03-01T00:59:59Z').getHours('Test/Julian') | int 1
        timestamp('2023-03-01T01:00:00Z').getHours('Test/Julian') | int 3
        timestamp('2024-02-29T12:00:00Z').getHours('Test/Julian') | int 13
        timestamp('2024-02-29T12:00:00Z').getDate('Test/Julian') | int 29
        timestamp('2024-03-01T00:59:59Z').getHours('Test/Julian') | int 1
        timestamp('2024-03-01T01:00:00Z').getHours('Test/Julian') | int 3
        timestamp('2023-07-01T12:00:00Z').getHours('Test/Julian') | int 14
        timestamp('2023-10-26T23:59:59Z').getMinutes('Test/Julian') | int 59
        timestamp('2023-10-26T23:59:59Z').getDate('Test/Julian') | int 27
        timestamp('2023-10-27T00:00:00Z').getMinutes('Test/Julian') | int 0
        timestamp('2023-10-27T00:00:00Z').getHours('Test/Julian') | int 1
        timestamp('2023-10-27T00:00:00Z').getDate('Test/Julian') | int 27
        timestamp('2024-02-29T00:59:59Z').getHours('Test/ZeroBased') | int 1
        timestamp('2024-02-29T01:00:00Z').getHours('Test/ZeroBased') | int 3
        timestamp('2023-02-28T12:00:00Z').getHours('Test/ZeroBased') | int 13
        timestamp('2023-03-01T00:59:59Z').getHours('Test/ZeroBased') | int 1
        timestamp('2023-03-01T01:00:00Z').getHours('Test/ZeroBased') | int 3
        timestamp('2023-10-27T23:59:59Z').getMinutes('Test/ZeroBased') | int 59
        timestamp('2023-10-27T23:59:59Z').getDate('Test/ZeroBased') | int 28
        timestamp('2023-10-28T00:00:00Z').getMinutes('Test/ZeroBased') | int 0
        timestamp('2023-10-28T00:00:00Z').getHours('Test/ZeroBased') | int 1
        timestamp('2024-10-26T23:59:59Z').getMinutes('Test/ZeroBased') | int 59
        timestamp('2024-10-26T23:59:59Z').getDate('Test/ZeroBased') | int 27
        timestamp('2024-10-27T00:00:00Z').getMinutes('Test/ZeroBased') | int 0
        timestamp('2024-10-27T00:00:00Z').getHours('Test/ZeroBased') | int 1
        timestamp('2024-03-31T00:59:59Z').getHours('Test/Nuuk') | int 22
        timestamp('2024-03-31T00:59:59Z').getMinutes('Test/Nuuk') | int 59
        timestamp('2024-03-31T00:59:59Z').getDate('Test/Nuuk') | int 30
        timestamp('2024-03-31T01:00:00Z').getHours('Test/Nuuk') | int 0
        timestamp('2024-03-31T01:00:00Z').getDate('Test/Nuuk') | int 31
        timestamp('2024-10-27T00:59:59Z').getHours('Test/Nuuk') | int 23
        timestamp('2024-10-27T00:59:59Z').getMinutes('Test/Nuuk') | int 59
        timestamp('2024-10-27T00:59:59Z').getDate('Test/Nuuk') | int 26
        timestamp('2024-10-27T01:00:00Z').getHours('Test/Nuuk') | int 23
        timestamp('2024-10-27T01:00:00Z').getMinutes('Test/Nuuk') | int 0
        timestamp('2024-10-27T01:00:00Z').getDate('Test/Nuuk') | int 26
        timestamp('2024-07-01T12:00:00Z').getHours('Test/Nuuk') | int 11
        timestamp('2024-01-01T12:00:00Z').getHours('Test/Nuuk') | int 10
        timestamp('2024-03-31T23:59:59Z').getHours('Test/Over24') | int 0
        timestamp('2024-03-31T23:59:59Z').getMinutes('Test/Over24') | int 59
        timestamp('2024-03-31T23:59:59Z').getDate('Test/Over24') | int 1
        timestamp('2024-04-01T00:00:00Z').getHours('Test/Over24') | int 2
        timestamp('2024-04-01T00:00:00Z').getMinutes('Test/Over24') | int 0
        timestamp('2024-04-01T00:00:00Z').getDate('Test/Over24') | int 1
        timestamp('2024-10-27T22:59:59Z').getHours('Test/Over24') | int 0
        timestamp('2024-10-27T22:59:59Z').getMinutes('Test/Over24') | int 59
        timestamp('2024-10-27T22:59:59Z').getDate('Test/Over24') | int 28
        timestamp('2024-10-27T23:00:00Z').getHours('Test/Over24') | int 0
        timestamp('2024-10-27T23:00:00Z').getMinutes('Test/Over24') | int 0
        timestamp('2024-10-27T23:00:00Z').getDate('Test/Over24') | int 28
        timestamp('2024-07-01T12:00:00Z').getHours('Test/Over24') | int 14
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

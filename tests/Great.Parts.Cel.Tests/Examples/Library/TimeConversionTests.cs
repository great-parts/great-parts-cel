using System.Collections.Generic;
using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The conversions of time of the standard library page, the table of the time conversions: the nine rows
// timestamp(timestamp), timestamp(int), timestamp(string), int(timestamp), string(timestamp),
// duration(duration), duration(string), int(duration), string(duration), every row by its identifier.
// The form of the rows is that of TimeRows: "identifier | expression | outcome". timestamp(string) is the
// strict form of RFC 3339 that cel-go has — the pattern isStrictRFC3339, narrowed by time.Parse of Go 1.27.3:
// the day within the month by the Gregorian leap year rule, the second 00 to 59, the letters T and Z
// capital; the digits of the fraction after the ninth are dropped without rounding. duration(string) is
// the grammar of time.ParseDuration: a sign, then number-unit pairs that may repeat, a number is
// "digits", "digits.", ".digits" or "digits.digits", the units h, m, s, ms, us, the two signs of micro
// (U+00B5 and U+03BC) with s, and ns, in this case; a bare 0 with a sign or without. A string that does not
// parse is InvalidArgument, one that parses but is outside the range of the type is ConversionRange, both
// at the call, the "(" after the name; the detail of InvalidArgument is the text of the library and the
// rows state the start of the message. The text of ConversionRange has the string as {value}.
// The cases of the cel-spec files timestamps and conversions are in the runner of the suite; the rows
// here are the boundaries of the page. Strings of time are errors at evaluation, also as constants.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "R-1")]
public sealed class TimeConversionTests
{
    private const string Rows = """
        timestamp_to_timestamp | timestamp(timestamp(1234567890)) | ts 1234567890 0
        timestamp_to_timestamp | timestamp(timestamp('1969-12-31T23:59:59.5Z')) | ts -1 500000000
        int64_to_timestamp | timestamp(1234567890) | ts 1234567890 0
        int64_to_timestamp | timestamp(0) | ts 0 0
        int64_to_timestamp | timestamp(-1) | ts -1 0
        int64_to_timestamp | timestamp(-62135596800) | ts -62135596800 0
        int64_to_timestamp | timestamp(253402300799) | ts 253402300799 0
        string_to_timestamp | timestamp('2009-02-13T23:31:30Z') | ts 1234567890 0
        string_to_timestamp | timestamp('2009-02-13T23:31:30+01:00') | ts 1234564290 0
        string_to_timestamp | timestamp('2009-02-13T23:31:30-23:59') | ts 1234654230 0
        string_to_timestamp | timestamp('2009-02-13T23:31:30.5Z') | ts 1234567890 500000000
        string_to_timestamp | timestamp('2009-02-13T23:31:30.123456789Z') | ts 1234567890 123456789
        string_to_timestamp | timestamp('2009-02-13T23:31:30.1234567891Z') | ts 1234567890 123456789
        string_to_timestamp | timestamp('2009-02-13T23:31:30.9999999999Z') | ts 1234567890 999999999
        string_to_timestamp | timestamp('1970-01-01T00:00:00Z') | ts 0 0
        string_to_timestamp | timestamp('1969-12-31T23:59:59Z') | ts -1 0
        string_to_timestamp | timestamp('2024-02-29T00:00:00Z') | ts 1709164800 0
        string_to_timestamp | timestamp('2000-02-29T00:00:00Z') | ts 951782400 0
        string_to_timestamp | timestamp('0001-01-01T00:00:00Z') | ts -62135596800 0
        string_to_timestamp | timestamp('9999-12-31T23:59:59.999999999Z') | ts 253402300799 999999999
        string_to_timestamp | timestamp('') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2023-02-30T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2023-04-31T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2023-02-29T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('1900-02-29T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:60Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13t23:31:30Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13 23:31:30Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-2-13T23:31:30Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30.Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T24:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-13-01T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-00-10T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-00T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30+24:00') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30+0100') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30+01') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30+01:60') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp(' 2009-02-13T23:31:30Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('2009-02-13T23:31:30Z ') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('20090213T233130Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('10000-01-01T00:00:00Z') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('now') | error InvalidArgument 9: invalid argument to 'timestamp': *
        string_to_timestamp | timestamp('0000-01-01T00:00:00Z') | error ConversionRange 9: 0000-01-01T00:00:00Z is out of the range of timestamp
        string_to_timestamp | timestamp('0001-01-01T00:00:00+01:00') | error ConversionRange 9: 0001-01-01T00:00:00+01:00 is out of the range of timestamp
        string_to_timestamp | timestamp('9999-12-31T23:59:59-01:00') | error ConversionRange 9: 9999-12-31T23:59:59-01:00 is out of the range of timestamp
        string_to_timestamp | timestamp('9999-12-31T23:59:59.999999999-00:01') | error ConversionRange 9: 9999-12-31T23:59:59.999999999-00:01 is out of the range of timestamp
        timestamp_to_int64 | int(timestamp(1234567890)) | int 1234567890
        timestamp_to_int64 | int(timestamp('2009-02-13T23:31:30.999999999Z')) | int 1234567890
        timestamp_to_int64 | int(timestamp('1969-12-31T23:59:59.5Z')) | int -1
        timestamp_to_int64 | int(timestamp(-62135596800)) | int -62135596800
        timestamp_to_int64 | int(timestamp('9999-12-31T23:59:59.999999999Z')) | int 253402300799
        timestamp_to_string | string(timestamp(0)) | str 1970-01-01T00:00:00Z
        timestamp_to_string | string(timestamp(1234567890)) | str 2009-02-13T23:31:30Z
        timestamp_to_string | string(timestamp('2009-02-13T23:31:30.5Z')) | str 2009-02-13T23:31:30.5Z
        timestamp_to_string | string(timestamp('2009-02-13T23:31:30.120Z')) | str 2009-02-13T23:31:30.12Z
        timestamp_to_string | string(timestamp('2009-02-13T23:31:30.000000001Z')) | str 2009-02-13T23:31:30.000000001Z
        timestamp_to_string | string(timestamp('2009-02-13T23:31:30.123456789Z')) | str 2009-02-13T23:31:30.123456789Z
        timestamp_to_string | string(timestamp('0001-01-01T00:00:00Z')) | str 0001-01-01T00:00:00Z
        timestamp_to_string | string(timestamp('9999-12-31T23:59:59.999999999Z')) | str 9999-12-31T23:59:59.999999999Z
        timestamp_to_string | string(timestamp('1969-12-31T23:59:59.5Z')) | str 1969-12-31T23:59:59.5Z
        timestamp_to_string | string(timestamp('2009-02-13T23:31:30+01:00')) | str 2009-02-13T22:31:30Z
        timestamp_to_string | string(timestamp('2009-02-13T23:31:30-23:59')) | str 2009-02-14T23:30:30Z
        duration_to_duration | duration(duration('1h')) | du 3600000000000
        duration_to_duration | duration(duration('-1.5s')) | du -1500000000
        string_to_duration | duration('1h') | du 3600000000000
        string_to_duration | duration('1h30m') | du 5400000000000
        string_to_duration | duration('90m') | du 5400000000000
        string_to_duration | duration('1.5s') | du 1500000000
        string_to_duration | duration('-1.5s') | du -1500000000
        string_to_duration | duration('+5s') | du 5000000000
        string_to_duration | duration('.5s') | du 500000000
        string_to_duration | duration('5.s') | du 5000000000
        string_to_duration | duration('1h1h') | du 7200000000000
        string_to_duration | duration('1m1s') | du 61000000000
        string_to_duration | duration('1us') | du 1000
        string_to_duration | duration('1µs') | du 1000
        string_to_duration | duration('1μs') | du 1000
        string_to_duration | duration('1ns') | du 1
        string_to_duration | duration('100ms') | du 100000000
        string_to_duration | duration('0.5ms') | du 500000
        string_to_duration | duration('1.5h') | du 5400000000000
        string_to_duration | duration('0') | du 0
        string_to_duration | duration('+0') | du 0
        string_to_duration | duration('-0') | du 0
        string_to_duration | duration('0s') | du 0
        string_to_duration | duration('-0s') | du 0
        string_to_duration | duration('2562047h') | du 9223369200000000000
        string_to_duration | duration('9223372036854775807ns') | du 9223372036854775807
        string_to_duration | duration('-9223372036854775808ns') | du -9223372036854775808
        string_to_duration | duration('9223372036.854775807s') | du 9223372036854775807
        string_to_duration | duration('-9223372036.854775808s') | du -9223372036854775808
        string_to_duration | duration('1.000000001s') | du 1000000001
        string_to_duration | duration('0.000000001s') | du 1
        string_to_duration | duration('1h0m') | du 3600000000000
        string_to_duration | duration('') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1H') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('h') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1 h') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1d') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('--1s') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1s ') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration(' 1s') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('.s') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1.2.3s') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('+') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('-') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1h-1m') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1S') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1ms1') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1sec') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1MS') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('one hour') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('1_0s') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('0x10s') | error InvalidArgument 8: invalid argument to 'duration': *
        string_to_duration | duration('9223372036854775808ns') | error ConversionRange 8: 9223372036854775808ns is out of the range of duration
        string_to_duration | duration('2562048h') | error ConversionRange 8: 2562048h is out of the range of duration
        string_to_duration | duration('9223372036.854775808s') | error ConversionRange 8: 9223372036.854775808s is out of the range of duration
        string_to_duration | duration('-9223372036854775809ns') | error ConversionRange 8: -9223372036854775809ns is out of the range of duration
        string_to_duration | duration('-9223372036.854775809s') | error ConversionRange 8: -9223372036.854775809s is out of the range of duration
        string_to_duration | duration('9223372036854775807s') | error ConversionRange 8: 9223372036854775807s is out of the range of duration
        duration_to_int64 | int(duration('1h')) | int 3600000000000
        duration_to_int64 | int(duration('-1.5s')) | int -1500000000
        duration_to_int64 | int(duration('1ns')) | int 1
        duration_to_int64 | int(duration('9223372036854775807ns')) | int 9223372036854775807
        duration_to_int64 | int(duration('-9223372036854775808ns')) | int -9223372036854775808
        duration_to_string | string(duration('0s')) | str 0s
        duration_to_string | string(duration('60.001s')) | str 60.001s
        duration_to_string | string(duration('-1.5s')) | str -1.5s
        duration_to_string | string(duration('1h')) | str 3600s
        duration_to_string | string(duration('1ns')) | str 0.000000001s
        duration_to_string | string(duration('-1ns')) | str -0.000000001s
        duration_to_string | string(duration('100ms')) | str 0.1s
        duration_to_string | string(duration('1.000000001s')) | str 1.000000001s
        duration_to_string | string(duration('9223372036854775807ns')) | str 9223372036.854775807s
        duration_to_string | string(duration('-9223372036854775808ns')) | str -9223372036.854775808s
        duration_to_string | string(duration('9223372036.854775807s')) | str 9223372036.854775807s
        duration_to_string | string(duration('1234567890123456789ns')) | str 1234567890.123456789s
        duration_to_string | string(duration('-0.5s')) | str -0.5s
        duration_to_string | string(duration('1m1.5s')) | str 61.5s
        """;

    private static readonly string[] Identifiers =
    [
        "timestamp_to_timestamp",
        "int64_to_timestamp",
        "string_to_timestamp",
        "timestamp_to_int64",
        "timestamp_to_string",
        "duration_to_duration",
        "string_to_duration",
        "duration_to_int64",
        "duration_to_string",
    ];

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(TimeRows.Check(environment, Rows, labelColumns: 1));
    }

    [Fact]
    public void EveryConversionOfTheTable_HasAnExample()
    {
        var expected = new SortedSet<string>(Identifiers, System.StringComparer.Ordinal);

        Assert.Equal(expected, TimeRows.Labels(Rows));
    }
}

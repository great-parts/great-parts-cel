using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cel.Expr;
using CsCheck;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Tests.Support;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Any = Google.Protobuf.WellKnownTypes.Any;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The profile of time of eng/difftest (docs/design/eng-difftest.md, "Profile", "Time", "Findings and output"): the strings and
// numbers of time, the zone names of the data file, the values of time in outcomes and in the conversion that both sides share,
// and the library side of the zone offsets. The generator of expressions with time is tested in GeneratorTests.
public sealed class TimeProfileTests
{
    private const int Draws = 8000;

    private static readonly Regex ValidTimestamp = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,12})?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    private static readonly Regex ValidDuration = new(@"^[+-]?(\d*\.?\d*(ns|us|µs|μs|ms|s|m|h))+$|^[+-]?0$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    // ---- Strings and numbers of time ----

    [Fact]
    public void StringsOfTimestamp_AreAtMost40CodePointsAndSomeAreValidAndSomeMutated()
    {
        List<string> texts = [.. Pcgs().Select(TimeStrings.Timestamp)];

        Assert.All(texts, text => Assert.InRange(text.EnumerateRunes().Count(), 1, TimeStrings.MaxCodePoints));
        Assert.Contains(texts, text => ValidTimestamp.IsMatch(text));
        Assert.Contains(texts, text => !ValidTimestamp.IsMatch(text));
        Assert.Contains(texts, text => ValidTimestamp.IsMatch(text) && text.Contains('.', StringComparison.Ordinal) && text.Split('.')[1].TakeWhile(char.IsAsciiDigit).Count() == 12);
        foreach (string offset in new[] { "Z", "+00:00", "-00:00", "+23:59", "-23:59", "+24:00", "+03:60" })
        {
            Assert.Contains(texts, text => ValidTimestamp.IsMatch(text) && text.EndsWith(offset, StringComparison.Ordinal));
        }

        foreach (string year in new[] { "0000", "0001", "1900", "2000", "2023", "2024", "9999" })
        {
            Assert.Contains(texts, text => text.StartsWith(year, StringComparison.Ordinal));
        }

        Assert.Contains(texts, text => text.Contains('t', StringComparison.Ordinal));
        Assert.Contains(texts, text => text.EndsWith('z'));
        Assert.Contains(texts, text => text.Length > 10 && text[10] == ' ');
        Assert.Contains(texts, text => text.Contains(".Z", StringComparison.Ordinal) || text.Contains(".+", StringComparison.Ordinal) || text.Contains(".-", StringComparison.Ordinal));
        Assert.Contains(texts, text => char.IsAsciiDigit(text[^1]) && !text.Contains('+', StringComparison.Ordinal) && text.LastIndexOf('-') == 7);
    }

    [Fact]
    public void StringsOfDuration_AreAtMost40CodePointsAndSomeAreValidAndSomeHaveAWrongUnit()
    {
        List<string> texts = [.. Pcgs().Select(TimeStrings.Duration)];

        Assert.All(texts, text => Assert.InRange(text.EnumerateRunes().Count(), 1, TimeStrings.MaxCodePoints));
        Assert.Contains(texts, text => ValidDuration.IsMatch(text));
        Assert.Contains(texts, text => !ValidDuration.IsMatch(text));
        foreach (string bare in new[] { "0", "+0", "-0" })
        {
            Assert.Contains(texts, text => text == bare);
        }

        foreach (string unit in new[] { "ns", "us", "µs", "μs", "ms", "s", "m", "h" })
        {
            Assert.Contains(texts, text => ValidDuration.IsMatch(text) && text.EndsWith(unit, StringComparison.Ordinal));
        }

        Assert.Contains(texts, text => text.Contains("9223372036854775807", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Contains("9223372036854775808", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.StartsWith('-'));
        Assert.Contains(texts, text => text.StartsWith('+'));
    }

    [Fact]
    public void IntegersOfTimestamp_AreTheLimitsOfTheRangeAndTheNumbersNextToThem()
    {
        List<long> numbers = [.. Pcgs().Select(TimeStrings.TimestampInteger)];

        foreach (long boundary in new long[] { 0, 1, -1, -62_135_596_800, 253_402_300_799, -62_135_596_801, 253_402_300_800, long.MinValue, long.MaxValue })
        {
            Assert.Contains(boundary, numbers);
        }

        Assert.Contains(numbers, number => number is > 1_000_000 and < (1L << 40));
    }

    [Fact]
    public void ZoneArguments_AreOfEveryKindOfTheProfile()
    {
        HashSet<string> zones = [.. ZoneData.Zones];
        List<string> arguments = [.. Pcgs().Select(TimeStrings.Zone)];

        Assert.All(arguments, argument => Assert.InRange(argument.EnumerateRunes().Count(), 0, TimeStrings.MaxCodePoints));
        Assert.Contains(arguments, zones.Contains);
        Assert.Contains("UTC", arguments);
        foreach (string text in new[] { "+05:30", "-03:00", "00:00", "+1:5", "+001:30", "+24:00", "+03:60", ":", string.Empty, "Local", "Mars/Base" })
        {
            Assert.Contains(text, arguments);
        }

        Assert.Contains(arguments, argument => !zones.Contains(argument) && zones.Any(zone => string.Equals(zone, argument, StringComparison.OrdinalIgnoreCase)));
    }

    // ---- The data file ----

    [Fact]
    public void ZonesOfTheDataFile_AreTheEntriesOfTheArchiveThatAreNotDirectoriesInItsOrder()
    {
        string file = Path.Combine(RepositoryRoot.Find(), ProfileTables.ZoneInfoPath);
        using ZipArchive archive = ZipFile.OpenRead(file);
        List<string> expected = [.. archive.Entries.Select(entry => entry.FullName).Where(name => !name.EndsWith('/'))];

        Assert.Equal(expected, ZoneData.Names(file));
        Assert.Equal(expected, ZoneData.Zones);
        Assert.Equal(expected, ZoneData.Entries(file).Select(entry => entry.Key));
        Assert.True(expected.Count > 500);
        Assert.Contains("Europe/Berlin", expected);
        Assert.Contains("UTC", expected);
        Assert.All(ZoneData.Entries(file), entry => Assert.Equal("TZif", Encoding.ASCII.GetString(entry.Value, 0, 4)));
    }

    // ---- Values of time ----

    [Fact]
    public void ValuesOfTime_HaveTheTypeNamesOfTheStandardLibraryPageAndAreWrittenBySecondsAndNanoseconds()
    {
        Value timestamp = Time(new Timestamp { Seconds = 1672531200, Nanos = 5 });
        Value duration = Time(new Duration { Seconds = -1, Nanos = -500_000_000 });

        Assert.Equal("timestamp", ValueText.TypeName(timestamp));
        Assert.Equal("duration", ValueText.TypeName(duration));
        Assert.Equal("timestamp(1672531200s 5ns)", ValueText.Format(timestamp));
        Assert.Equal("duration(-1s -500000000ns)", ValueText.Format(duration));
    }

    [Fact]
    public void ValuesOfTime_AreEqualByTheirSecondsAndNanosecondsAndByTheirType()
    {
        Value timestamp = Time(new Timestamp { Seconds = 7, Nanos = 9 });

        Assert.True(ValueEquality.Equal(timestamp, Time(new Timestamp { Seconds = 7, Nanos = 9 })));
        Assert.False(ValueEquality.Equal(timestamp, Time(new Timestamp { Seconds = 7, Nanos = 10 })));
        Assert.False(ValueEquality.Equal(timestamp, Time(new Timestamp { Seconds = 8, Nanos = 9 })));
        Assert.False(ValueEquality.Equal(timestamp, Time(new Duration { Seconds = 7, Nanos = 9 })));
        Assert.True(ValueEquality.Equal(Time(new Duration { Seconds = -7, Nanos = -9 }), Time(new Duration { Seconds = -7, Nanos = -9 })));
    }

    [Fact]
    public void ConversionOfValues_ReadsAndWritesTheTwoKindsOfTimeAndTheirTypeNames()
    {
        Assert.True(ValueConversion.ToCelValue(Time(new Timestamp { Seconds = -62_135_596_800, Nanos = 999_999_999 })).TryGetTimestamp(out CelTimestamp timestamp));
        Assert.Equal((-62_135_596_800L, 999_999_999), (timestamp.Seconds, timestamp.Nanoseconds));
        Assert.True(ValueConversion.ToCelValue(Time(new Duration { Seconds = 123, Nanos = 321_456_789 })).TryGetDuration(out CelDuration duration));
        Assert.Equal(123_321_456_789L, duration.TotalNanoseconds);

        Assert.Equal(Time(new Timestamp { Seconds = 253_402_300_799, Nanos = 1 }), ValueConversion.ToExprValue(CelValue.FromTimestamp(new CelTimestamp(253_402_300_799, 1))));
        Assert.Equal(Time(new Duration { Seconds = 1, Nanos = 500_000_000 }), ValueConversion.ToExprValue(CelValue.FromDuration(new CelDuration(1_500_000_000))));
        Assert.Equal(Time(new Duration { Seconds = -1, Nanos = -500_000_000 }), ValueConversion.ToExprValue(CelValue.FromDuration(new CelDuration(-1_500_000_000))));
        Assert.Equal(Time(new Duration { Seconds = -9_223_372_036, Nanos = -854_775_808 }), ValueConversion.ToExprValue(CelValue.FromDuration(new CelDuration(long.MinValue))));

        Assert.True(ValueConversion.ToCelValue(new Value { TypeValue = "google.protobuf.Timestamp" }).TryGetType(out CelType? timestampType));
        Assert.Equal(CelType.Timestamp, timestampType);
        Assert.True(ValueConversion.ToCelValue(new Value { TypeValue = "google.protobuf.Duration" }).TryGetType(out CelType? durationType));
        Assert.Equal(CelType.Duration, durationType);
        Assert.Equal("google.protobuf.Timestamp", ValueConversion.ToExprValue(CelValue.FromType(CelType.Timestamp)).TypeValue);
        Assert.Equal("google.protobuf.Duration", ValueConversion.ToExprValue(CelValue.FromType(CelType.Duration)).TypeValue);
        Assert.Throws<NotImplementedException>(() => ValueConversion.ToCelValue(new Value { ObjectValue = Any.Pack(new Google.Protobuf.WellKnownTypes.Empty()) }));
    }

    // ---- Helpers ----

    // The random sources of the draws: one per number, from a fixed seed.
    private static IEnumerable<PCG> Pcgs() => Enumerable.Range(0, Draws).Select(n => CaseGenerator.CaseState(20261104, (ulong)n));

    private static Value Time(Timestamp timestamp) => new() { ObjectValue = Any.Pack(timestamp) };

    private static Value Time(Duration duration) => new() { ObjectValue = Any.Pack(duration) };
}

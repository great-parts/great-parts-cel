using System;
using System.Collections.Generic;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The offsets of zones of the test archive at instants that are constants of the test, worked out outside the library, and the
// consistency of the files of the archive with themselves: the transitions they list and the rule of their footer.
public sealed class ZoneOffsetTests
{
    // The offset in seconds east of UTC of a zone one second before an instant and at it.
    [Theory]
    // Kathmandu: +05:41:16 until 1920-01-01 local, +05:30 until 1986-01-01 local, +05:45 after.
    [InlineData("Asia/Kathmandu", -1_577_943_676L, 20_476, 19_800)]
    [InlineData("Asia/Kathmandu", 504_901_799L, 19_800, 19_800)]
    [InlineData("Asia/Kathmandu", 504_901_800L, 19_800, 20_700)]
    [InlineData("Asia/Kathmandu", 4_102_444_800L, 20_700, 20_700)]
    // Moscow: +04:00 from 2011-03-27 until 2014-10-26, then +03:00.
    [InlineData("Europe/Moscow", 1_414_274_399L, 14_400, 14_400)]
    [InlineData("Europe/Moscow", 1_414_274_400L, 14_400, 10_800)]
    [InlineData("Europe/Moscow", 962_409_600L, 14_400, 14_400)]
    [InlineData("Europe/Moscow", 4_102_444_800L, 10_800, 10_800)]
    // New York: LMT -04:56:02 until 1883-11-18 17:00:00 UTC, then -05:00; daylight time by the rule; the rule after 2037.
    [InlineData("America/New_York", -2_717_650_801L, -17_762, -17_762)]
    [InlineData("America/New_York", -2_717_650_800L, -17_762, -18_000)]
    [InlineData("America/New_York", 1_678_604_399L, -18_000, -18_000)]
    [InlineData("America/New_York", 1_678_604_400L, -18_000, -14_400)]
    [InlineData("America/New_York", 1_699_163_999L, -14_400, -14_400)]
    [InlineData("America/New_York", 1_699_164_000L, -14_400, -18_000)]
    [InlineData("America/New_York", 4_103_654_400L, -18_000, -18_000)]
    [InlineData("America/New_York", 4_119_292_800L, -14_400, -14_400)]
    // Lord Howe: +10:30 with daylight time of half an hour, +11:00; the instants are in 2023.
    [InlineData("Australia/Lord_Howe", 1_680_361_199L, 39_600, 39_600)]
    [InlineData("Australia/Lord_Howe", 1_680_361_200L, 39_600, 37_800)]
    [InlineData("Australia/Lord_Howe", 1_696_087_799L, 37_800, 37_800)]
    [InlineData("Australia/Lord_Howe", 1_696_087_800L, 37_800, 39_600)]
    [InlineData("Australia/Lord_Howe", 4_103_654_400L, 39_600, 39_600)]
    [InlineData("Australia/Lord_Howe", 4_119_292_800L, 37_800, 37_800)]
    // London: UTC, and +01:00 from the last Sunday of March.
    [InlineData("Europe/London", 1_679_792_399L, 0, 0)]
    [InlineData("Europe/London", 1_679_792_400L, 0, 3_600)]
    public static void OffsetOfAZone_AtKnownInstants(string zoneId, long instant, int before, int at)
    {
        ZoneData zone = ZoneInfoFile.Database().Find(zoneId)!;

        Assert.NotNull(zone);
        Assert.Equal(before, TimeZoneRules.OffsetAt(zone, instant - 1));
        Assert.Equal(at, TimeZoneRules.OffsetAt(zone, instant));
    }

    // At every explicit transition the offset is the offset of its type, and one second before it, the offset of the type before:
    // the type 0 for the first transition.
    [Fact]
    public static void EveryTransitionOfEveryZone_GivesItsTypeAndTheTypeBefore()
    {
        foreach (KeyValuePair<string, byte[]> entry in ZoneInfoFile.Entries())
        {
            ZoneData zone = TzifFile.Read(entry.Value);
            for (int i = 0; i < zone.Transitions.Length; i++)
            {
                int type = zone.TransitionTypes[i];
                int previous = i == 0 ? 0 : zone.TransitionTypes[i - 1];

                Assert.True(
                    zone.TypeOffsets[type] == TimeZoneRules.OffsetAt(zone, zone.Transitions[i]),
                    $"{entry.Key}: transition {i} at {zone.Transitions[i]}");
                Assert.True(
                    zone.TypeOffsets[previous] == TimeZoneRules.OffsetAt(zone, zone.Transitions[i] - 1),
                    $"{entry.Key}: one second before transition {i} at {zone.Transitions[i]}");
            }
        }
    }

    // After the last transition of a slim file the rule of the footer governs: the offsets over the years 2009 to 2100 equal those of
    // a search written in the test, which walks the days of the month one by one and takes the n-th weekday by counting. The rules of
    // these zones are the rules the footers hold (a week 5 is the last such weekday), and the zones differ in hemisphere, the weekday
    // time, whether the time is of standard or daylight time, and the size of the daylight shift.
    [Theory]
    // zone, start (month, week, local second of standard time), end (month, week, local second of daylight time), standard, daylight
    [InlineData("America/New_York", 3, 2, 7_200, 11, 1, 7_200, -18_000, -14_400)]
    [InlineData("Europe/Berlin", 3, 5, 7_200, 10, 5, 10_800, 3_600, 7_200)]
    [InlineData("Europe/London", 3, 5, 3_600, 10, 5, 7_200, 0, 3_600)]
    [InlineData("Australia/Sydney", 10, 1, 7_200, 4, 1, 10_800, 36_000, 39_600)]
    [InlineData("Australia/Lord_Howe", 10, 1, 7_200, 4, 1, 7_200, 37_800, 39_600)]
    [InlineData("Pacific/Auckland", 9, 5, 7_200, 4, 1, 10_800, 43_200, 46_800)]
    public static void RuleOfTheFooter_AgreesWithADayByDaySearch(
        string zoneId, int startMonth, int startWeek, int startSecond, int endMonth, int endWeek, int endSecond, int standard, int daylight)
    {
        ZoneData zone = ZoneInfoFile.Database().Find(zoneId)!;
        Assert.NotNull(zone);
        Assert.NotNull(zone.Rule);
        int checkedYears = 0;
        for (int year = 2009; year <= 2100; year++)
        {
            long starts = LocalSecondsOfSunday(year, startMonth, startWeek, startSecond) - standard;
            long ends = LocalSecondsOfSunday(year, endMonth, endWeek, endSecond) - daylight;
            Assert.True(zone.Transitions[^1] < Math.Min(starts, ends), zoneId + " " + year);

            // Daylight time lies between the start and the end in the north, and outside them in the south.
            if (starts < ends)
            {
                Assert.Equal(standard, TimeZoneRules.OffsetAt(zone, starts - 1));
                Assert.Equal(daylight, TimeZoneRules.OffsetAt(zone, starts));
                Assert.Equal(daylight, TimeZoneRules.OffsetAt(zone, ends - 1));
                Assert.Equal(standard, TimeZoneRules.OffsetAt(zone, ends));
            }
            else
            {
                Assert.Equal(daylight, TimeZoneRules.OffsetAt(zone, ends - 1));
                Assert.Equal(standard, TimeZoneRules.OffsetAt(zone, ends));
                Assert.Equal(standard, TimeZoneRules.OffsetAt(zone, starts - 1));
                Assert.Equal(daylight, TimeZoneRules.OffsetAt(zone, starts));
            }

            checkedYears++;
        }

        Assert.Equal(92, checkedYears);
    }

    // The local time of day of the n-th Sunday of a month, as seconds from 1970-01-01 counted in local time: the days of the month
    // are walked one by one with DateTime, which a test may use; week 5 is the last Sunday.
    private static long LocalSecondsOfSunday(int year, int month, int week, int secondOfDay)
    {
        int found = 0;
        int sunday = 0;
        for (int day = 1; day <= DateTime.DaysInMonth(year, month); day++)
        {
            if (new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc).DayOfWeek == DayOfWeek.Sunday)
            {
                found++;
                sunday = day;
                if (found == week)
                {
                    break;
                }
            }
        }

        DateTime midnight = new(year, month, sunday, 0, 0, 0, DateTimeKind.Utc);
        return new DateTimeOffset(midnight).ToUnixTimeSeconds() + secondOfDay;
    }
}

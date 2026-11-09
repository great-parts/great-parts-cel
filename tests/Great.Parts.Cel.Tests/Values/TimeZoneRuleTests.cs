using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The POSIX TZ rule of a footer: its forms, the offset it gives around the instants where it changes, and the texts that are not
// rules. The expected instants are Unix seconds that were worked out outside the library, by the dates of the calendar.
public sealed class TimeZoneRuleTests
{
    // The instant is an offset change when the offset one second before it differs from the offset at it.
    [Theory]
    // EST5EDT,M3.2.0,M11.1.0: the second Sunday of March at 02:00 to the first Sunday of November at 02:00, in 2023, 2024 and 2100.
    [InlineData("EST5EDT,M3.2.0,M11.1.0", 1_678_604_400L, -18_000, -14_400)]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", 1_699_164_000L, -14_400, -18_000)]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", 1_710_054_000L, -18_000, -14_400)]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", 1_730_613_600L, -14_400, -18_000)]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", 4_108_690_800L, -18_000, -14_400)]
    [InlineData("EST5EDT,M3.2.0,M11.1.0", 4_129_250_400L, -14_400, -18_000)]
    // A daylight name with no offset and no dates: one hour more, and the dates M3.2.0 and M11.1.0.
    [InlineData("EST5EDT", 1_678_604_400L, -18_000, -14_400)]
    [InlineData("EST5EDT", 1_699_164_000L, -14_400, -18_000)]
    // The Julian day Jn does not count 29 February: J60 is 1 March in 2023 and in the leap year 2024.
    [InlineData("<+01>-1<+02>,J60,J300", 1_677_632_400L, 3_600, 7_200)]
    [InlineData("<+01>-1<+02>,J60,J300", 1_698_364_800L, 7_200, 3_600)]
    [InlineData("<+01>-1<+02>,J60,J300", 1_709_254_800L, 3_600, 7_200)]
    [InlineData("<+01>-1<+02>,J60,J300", 1_729_987_200L, 7_200, 3_600)]
    // The day n counts from zero and counts 29 February: 59 is 1 March in 2023 and 29 February in 2024.
    [InlineData("<+01>-1<+02>,59,300", 1_677_632_400L, 3_600, 7_200)]
    [InlineData("<+01>-1<+02>,59,300", 1_698_451_200L, 7_200, 3_600)]
    [InlineData("<+01>-1<+02>,59,300", 1_709_168_400L, 3_600, 7_200)]
    [InlineData("<+01>-1<+02>,59,300", 1_729_987_200L, 7_200, 3_600)]
    // A negative rule time: one hour before midnight of the last Sunday of March; the end is at 00:00 of daylight time.
    [InlineData("<-02>2<-01>,M3.5.0/-1,M10.5.0/0", 1_679_792_400L, -7_200, -3_600)]
    [InlineData("<-02>2<-01>,M3.5.0/-1,M10.5.0/0", 1_698_541_200L, -3_600, -7_200)]
    [InlineData("<-02>2<-01>,M3.5.0/-1,M10.5.0/0", 1_711_846_800L, -7_200, -3_600)]
    [InlineData("<-02>2<-01>,M3.5.0/-1,M10.5.0/0", 1_729_990_800L, -3_600, -7_200)]
    // A rule time of 25 hours is 01:00 of the next day.
    [InlineData("<+01>-1<+02>,M3.5.0/25,M10.5.0/25", 1_679_875_200L, 3_600, 7_200)]
    [InlineData("<+01>-1<+02>,M3.5.0/25,M10.5.0/25", 1_698_620_400L, 7_200, 3_600)]
    // Week 5 is the last such weekday even when the month has four: February 2023 has Sundays on 5, 12, 19 and 26.
    [InlineData("<+01>-1<+02>,M2.5.0/0,M12.1.0/0", 1_677_366_000L, 3_600, 7_200)]
    [InlineData("<+01>-1<+02>,M2.5.0/0,M12.1.0/0", 1_701_554_400L, 7_200, 3_600)]
    // The southern hemisphere: daylight time from October to April, so the start comes after the end within a year.
    [InlineData("AEST-10AEDT,M10.1.0,M4.1.0/3", 1_696_089_600L, 36_000, 39_600)]
    [InlineData("AEST-10AEDT,M10.1.0,M4.1.0/3", 1_680_364_800L, 39_600, 36_000)]
    public static void TheOffsetChangesExactlyAtTheInstantOfTheRule(string footer, long instant, int before, int after)
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule(footer, 0));

        Assert.Equal(before, TimeZoneRules.OffsetAt(zone, instant - 1));
        Assert.Equal(after, TimeZoneRules.OffsetAt(zone, instant));
    }

    // The offset of the rule with no change: the standard offset at every instant.
    [Theory]
    [InlineData("<+0545>-5:45", 20_700)]
    [InlineData("UTC0", 0)]
    [InlineData("<-03>3", -10_800)]
    [InlineData("<+1030>-10:30:15", 37_815)]
    [InlineData("<-12>12", -43_200)]
    [InlineData("<+14>-14", 50_400)]
    public static void RuleWithoutDaylightName_GivesTheStandardOffsetAlways(string footer, int offset)
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule(footer, 0));

        Assert.Equal(offset, TimeZoneRules.OffsetAt(zone, 0));
        Assert.Equal(offset, TimeZoneRules.OffsetAt(zone, 1_700_000_000));
        Assert.Equal(offset, TimeZoneRules.OffsetAt(zone, 4_102_444_800));
    }

    // Dates after the standard offset of a rule with no daylight name are not used; the reference implementation accepts them.
    [Fact]
    public static void RuleWithDatesButNoDaylightName_IsTheStandardOffset()
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule("EST5,M3.2.0,M11.1.0", 0));

        Assert.Equal(-18_000, TimeZoneRules.OffsetAt(zone, 1_690_000_000));
    }

    // The semicolon that the reference implementation accepts before the dates, after the daylight offset: the offsets are
    // those of the rule with a comma.
    [Theory]
    [InlineData(1_690_000_000L, -14_400)]
    [InlineData(1_700_000_000L, -18_000)]
    public static void RuleWithSemicolonBeforeTheDates_GivesTheOffsetsOfTheRuleWithAComma(long instant, int offset)
    {
        ZoneData withSemicolon = TzifFile.Read(TzifFile.WithRule("EST5EDT4;M3.2.0,M11.1.0", 0));
        ZoneData withComma = TzifFile.Read(TzifFile.WithRule("EST5EDT4,M3.2.0,M11.1.0", 0));

        Assert.Equal(offset, TimeZoneRules.OffsetAt(withSemicolon, instant));
        Assert.Equal(offset, TimeZoneRules.OffsetAt(withComma, instant));
    }

    // The daylight offset of the rule is its own, not the standard offset plus one hour.
    [Theory]
    [InlineData("<+1030>-10:30<+11>-11,M10.1.0,M4.1.0", 1_690_000_000L, 37_800)]
    [InlineData("<+1030>-10:30<+11>-11,M10.1.0,M4.1.0", 1_700_000_000L, 39_600)]
    [InlineData("<+1030>-10:30<+11>-11,M10.1.0,M4.1.0", 1_680_000_000L, 39_600)]
    public static void DaylightOffsetOfTheRule_IsUsedAsWritten(string footer, long instant, int offset)
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule(footer, 0));

        Assert.Equal(offset, TimeZoneRules.OffsetAt(zone, instant));
    }

    // Names of three characters or more up to the first digit, sign or comma, or any characters in angle brackets.
    [Theory]
    [InlineData("EST5EDT,M3.2.0,M11.1.0")]
    [InlineData("<EST>5<EDT>,M3.2.0,M11.1.0")]
    [InlineData("<>5<>,M3.2.0,M11.1.0")]
    [InlineData("<a b>5<c d>,M3.2.0,M11.1.0")]
    [InlineData("ABCDEF5GHIJKL,M3.2.0,M11.1.0")]
    [InlineData("EST+5EDT,M3.2.0,M11.1.0")]
    public static void NamesInAnyForm_AreSkipped(string footer)
    {
        Assert.True(TimeZoneRules.TryParseRule(System.Text.Encoding.ASCII.GetBytes(footer), out TimeZonePosixRule? rule));

        Assert.NotNull(rule);
        Assert.Equal(-18_000, rule.StandardOffset);
        Assert.True(rule.HasDaylight);
        Assert.Equal(-14_400, rule.DaylightOffset);
    }

    [Fact]
    public static void EmptyText_IsNoRule()
    {
        Assert.True(TimeZoneRules.TryParseRule([], out TimeZonePosixRule? rule));

        Assert.Null(rule);
    }

    // The forms of a rule that are not rules.
    [Theory]
    [InlineData("garbage")]
    [InlineData("ES5")]
    [InlineData("EST")]
    [InlineData("EST5EDT,M13.1.0,M10.5.0")]
    [InlineData("EST5EDT,M0.1.0,M10.5.0")]
    [InlineData("EST5EDT,M3.0.0,M10.5.0")]
    [InlineData("EST5EDT,M3.6.0,M10.5.0")]
    [InlineData("EST5EDT,M3.1.7,M10.5.0")]
    [InlineData("EST5EDT,M3.1,M10.5.0")]
    [InlineData("EST5EDT,J0,J300")]
    [InlineData("EST5EDT,J366,J300")]
    [InlineData("EST5EDT,366,300")]
    [InlineData("EST5EDT,M3.2.0")]
    [InlineData("EST5EDT,M3.2.0,M11.1.0,M1.1.1")]
    [InlineData("EST5EDT,M3.2.0,M11.1.0x")]
    [InlineData("EST5EDT,M3.2.0/,M11.1.0")]
    [InlineData("EST5EDT,M3.2.0/x,M11.1.0")]
    [InlineData("EST5EDTM3.2.0,M11.1.0")]
    [InlineData("EST169EDT,M3.2.0,M11.1.0")]
    [InlineData("<-03>169,M3.2.0,M11.1.0")]
    [InlineData("EST5EDT,M3.2.0;M11.1.0")]
    [InlineData("EST5:60EDT,M3.2.0,M11.1.0")]
    [InlineData("EST5:30:60EDT,M3.2.0,M11.1.0")]
    [InlineData("EST5EDT,M3.2.0/169,M11.1.0")]
    [InlineData("EST5EDT,M3.2.0,M11.1.0/-169")]
    [InlineData("<EST5EDT")]
    [InlineData("EST5EDT,")]
    [InlineData("EST+")]
    [InlineData("EST99999999999999999999")]
    public static void TextThatIsNotARule_IsRefused(string text)
    {
        Assert.False(TimeZoneRules.TryParseRule(System.Text.Encoding.ASCII.GetBytes(text), out TimeZonePosixRule? rule));

        Assert.Null(rule);
    }

    // The hours of a rule time go up to 168, the bound of Go tzset; RFC 9636 names 167.
    [Fact]
    public static void RuleTimeUpTo168Hours_IsRead()
    {
        Assert.True(TimeZoneRules.TryParseRule("EST5EDT,M3.2.0/168,M11.1.0/-168"u8, out TimeZonePosixRule? rule));

        Assert.Equal(168 * 3_600, rule!.Start.TimeSeconds);
        Assert.Equal(-168 * 3_600, rule.End.TimeSeconds);
    }

    // The standard offset and the daylight offset go up to 168 hours too.
    [Fact]
    public static void OffsetUpTo168Hours_IsRead()
    {
        Assert.True(TimeZoneRules.TryParseRule("<-168>168<+168>-168,M3.2.0,M11.1.0"u8, out TimeZonePosixRule? rule));

        Assert.Equal(-168 * 3_600, rule!.StandardOffset);
        Assert.Equal(168 * 3_600, rule.DaylightOffset);
    }

    [Fact]
    public static void TimeOfTheRule_DefaultsToTwoOClock()
    {
        Assert.True(TimeZoneRules.TryParseRule("EST5EDT,J60,10"u8, out TimeZonePosixRule? rule));

        Assert.Equal(2 * 3_600, rule!.Start.TimeSeconds);
        Assert.Equal(2 * 3_600, rule.End.TimeSeconds);
        Assert.Equal(TimeZonePosixDateKind.Julian, rule.Start.Kind);
        Assert.Equal(TimeZonePosixDateKind.DayOfYear, rule.End.Kind);
        Assert.Equal(60, rule.Start.Day);
        Assert.Equal(10, rule.End.Day);
    }

    [Fact]
    public static void TimeOfTheRule_ReadsMinutesAndSeconds()
    {
        Assert.True(TimeZoneRules.TryParseRule("EST5EDT,M3.2.0/1:30,M11.1.0/-0:00:01"u8, out TimeZonePosixRule? rule));

        Assert.Equal(5_400, rule!.Start.TimeSeconds);
        Assert.Equal(-1, rule.End.TimeSeconds);
        Assert.Equal(new TimeZonePosixDate(TimeZonePosixDateKind.MonthWeekDay, 3, 2, 0, 5_400), rule.Start);
    }

    // A zone that has no transitions takes the rule of its footer when it has one, else the offset of the first type.
    [Fact]
    public static void ZoneWithoutTransitions_UsesTheRuleOrTheFirstType()
    {
        ZoneData withRule = TzifFile.Read(TzifFile.Fixed(0, "<+0545>-5:45"));
        ZoneData withoutRule = TzifFile.Read(TzifFile.Fixed(3_600, string.Empty));

        Assert.Equal(20_700, TimeZoneRules.OffsetAt(withRule, 0));
        Assert.Equal(3_600, TimeZoneRules.OffsetAt(withoutRule, long.MinValue));
        Assert.Equal(3_600, TimeZoneRules.OffsetAt(withoutRule, long.MaxValue));
    }

    // Instants before the first transition have the type 0, whatever the later types are; with no rule, the last type stays.
    [Fact]
    public static void Transitions_GiveTheTypeBeforeAndAtEachOne()
    {
        ZoneData zone = TzifFile.Read(TzifFile.Build([100, 200, 300], [2, 1, 2], [-50, 5_000, 7_000], string.Empty));

        Assert.Equal(-50, TimeZoneRules.OffsetAt(zone, long.MinValue));
        Assert.Equal(-50, TimeZoneRules.OffsetAt(zone, 99));
        Assert.Equal(7_000, TimeZoneRules.OffsetAt(zone, 100));
        Assert.Equal(7_000, TimeZoneRules.OffsetAt(zone, 199));
        Assert.Equal(5_000, TimeZoneRules.OffsetAt(zone, 200));
        Assert.Equal(5_000, TimeZoneRules.OffsetAt(zone, 299));
        Assert.Equal(7_000, TimeZoneRules.OffsetAt(zone, 300));
        Assert.Equal(7_000, TimeZoneRules.OffsetAt(zone, long.MaxValue));
    }

    // After the last transition the rule governs, not the type of that transition.
    [Fact]
    public static void AfterTheLastTransition_TheRuleGoverns()
    {
        ZoneData zone = TzifFile.Read(TzifFile.Build([100], [1], [0, 3_600], "<+02>-2"));

        Assert.Equal(0, TimeZoneRules.OffsetAt(zone, 99));
        Assert.Equal(7_200, TimeZoneRules.OffsetAt(zone, 100));
        Assert.Equal(7_200, TimeZoneRules.OffsetAt(zone, 1_700_000_000));
    }

    // The time zone of the footer keeps its second: an offset of one hour and a few seconds.
    [Fact]
    public static void OffsetsKeepTheirSeconds()
    {
        ZoneData zone = TzifFile.Read(TzifFile.Build([0], [1], [-17_762, -18_000], string.Empty));

        Assert.Equal(-17_762, TimeZoneRules.OffsetAt(zone, -1));
        Assert.Equal(-18_000, TimeZoneRules.OffsetAt(zone, 0));
    }

    // The year of the rule is the year of the instant in UTC: an instant at the very start of a year has that year's dates.
    [Fact]
    public static void RuleOfTheNorthernHemisphere_AtTheTurnOfTheYear()
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule("EST5EDT,M3.2.0,M11.1.0", 0));
        long newYear2024 = 1_704_067_200;

        Assert.Equal(-18_000, TimeZoneRules.OffsetAt(zone, newYear2024 - 1));
        Assert.Equal(-18_000, TimeZoneRules.OffsetAt(zone, newYear2024));
    }

    [Fact]
    public static void RuleOfTheSouthernHemisphere_IsDaylightAtTheTurnOfTheYear()
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule("AEST-10AEDT,M10.1.0,M4.1.0/3", 0));
        long newYear2024 = 1_704_067_200;

        Assert.Equal(39_600, TimeZoneRules.OffsetAt(zone, newYear2024 - 1));
        Assert.Equal(39_600, TimeZoneRules.OffsetAt(zone, newYear2024));
    }

    // An instant far outside the range of timestamp does not overflow.
    [Fact]
    public static void InstantsAtTheEdgesOfLong_AreNotAnError()
    {
        ZoneData zone = TzifFile.Read(TzifFile.WithRule("EST5EDT,M3.2.0,M11.1.0", 0));

        Assert.True(TimeZoneRules.OffsetAt(zone, long.MaxValue) is -18_000 or -14_400);
        Assert.Equal(0, TimeZoneRules.OffsetAt(zone, long.MinValue));
    }
}

using System;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The proleptic Gregorian calendar over days from 1970-01-01, with astronomical years: against DateTime over every day of
// its range (a test may use DateTime; the library may not), and at the points where a calendar goes wrong.
public sealed class CalendarRulesTests
{
    private const long FirstDay = -719_162;
    private const long LastDay = 2_932_896;

    [Fact]
    public static void EveryDayOfTheRangeOfTimestamp_AgreesWithDateTime()
    {
        for (long days = FirstDay; days <= LastDay; days++)
        {
            DateTime date = DateTime.UnixEpoch.AddDays(days);

            CalendarRules.CivilFromDays(days, out long year, out int month, out int day);

            Assert.Equal((date.Year, date.Month, date.Day), ((int)year, month, day));
            Assert.Equal(days, CalendarRules.DaysFromCivil(year, month, day));
            Assert.Equal((int)date.DayOfWeek, CalendarRules.DayOfWeek(days));
            Assert.Equal(date.DayOfYear - 1, CalendarRules.DayOfYear(year, month, day));
        }
    }

    [Theory]
    [InlineData(1900, false)]
    [InlineData(1904, true)]
    [InlineData(2000, true)]
    [InlineData(2023, false)]
    [InlineData(2024, true)]
    [InlineData(2100, false)]
    [InlineData(9999, false)]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(-4, true)]
    [InlineData(-100, false)]
    [InlineData(-400, true)]
    public static void IsLeap_FollowsTheGregorianRule_AndYearZeroIsLeap(long year, bool expected)
    {
        Assert.Equal(expected, CalendarRules.IsLeap(year));
    }

    [Fact]
    public static void DaysInMonth_GivesTheLengthOfEachMonth()
    {
        int[] common = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

        for (int month = 1; month <= 12; month++)
        {
            Assert.Equal(common[month - 1], CalendarRules.DaysInMonth(2023, month));
            Assert.Equal(month == 2 ? 29 : common[month - 1], CalendarRules.DaysInMonth(2024, month));
        }

        Assert.Equal(29, CalendarRules.DaysInMonth(0, 2));
    }

    // Year 0 and negative years exist: the day before 0001-01-01 is 0000-12-31, and 0000-03-01 starts the shifted year of
    // the algorithm.
    [Fact]
    public static void YearsBeforeOne_AreAstronomical()
    {
        CalendarRules.CivilFromDays(FirstDay - 1, out long year, out int month, out int day);

        Assert.Equal((0L, 12, 31), (year, month, day));
        Assert.Equal(365, CalendarRules.DayOfYear(0, 12, 31));
        Assert.Equal(FirstDay - 1, CalendarRules.DaysFromCivil(0, 12, 31));
        Assert.Equal(FirstDay - 366, CalendarRules.DaysFromCivil(0, 1, 1));

        CalendarRules.CivilFromDays(CalendarRules.DaysFromCivil(-4, 2, 29), out long leapYear, out int leapMonth, out int leapDay);
        Assert.Equal((-4L, 2, 29), (leapYear, leapMonth, leapDay));
        CalendarRules.CivilFromDays(LastDay + 1, out long over, out int overMonth, out int overDay);
        Assert.Equal((10000L, 1, 1), (over, overMonth, overDay));
    }

    [Fact]
    public static void DayOfWeek_StartsAtSundayAndThe1970EpochIsAThursday()
    {
        Assert.Equal(4, CalendarRules.DayOfWeek(0));
        Assert.Equal(0, CalendarRules.DayOfWeek(3));
        Assert.Equal(6, CalendarRules.DayOfWeek(2));
        Assert.Equal(3, CalendarRules.DayOfWeek(-1));
        Assert.Equal(1, CalendarRules.DayOfWeek(-3));
    }

    [Theory]
    [InlineData(7, 2, 3, 1)]
    [InlineData(-7, 2, -4, 1)]
    [InlineData(-1, 86400, -1, 86399)]
    [InlineData(0, 86400, 0, 0)]
    [InlineData(-86400, 86400, -1, 0)]
    public static void FloorDivide_RoundsTowardNegativeInfinity(long dividend, long divisor, long quotient, long remainderCheck)
    {
        long floor = CalendarRules.FloorDivide(dividend, divisor);

        Assert.Equal(quotient, floor);
        Assert.Equal(remainderCheck, dividend - (floor * divisor));
    }
}

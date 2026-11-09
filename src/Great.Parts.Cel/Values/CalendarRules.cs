namespace Great.Parts.Cel.Values;

/// <summary>
/// The proleptic Gregorian calendar over days counted from 1970-01-01, with astronomical years: year 0 exists and is a
/// leap year. It reads no clock, zone or culture, so no platform changes a result.
/// </summary>
internal static class CalendarRules
{
    /// <summary>The number of seconds in a day.</summary>
    internal const long SecondsPerDay = 86_400;

    // The days from 0000-03-01 to 1970-01-01, the shift of the civil algorithms below.
    private const long DaysToEpoch = 719_468;

    private static readonly int[] DaysBeforeMonth = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];

    /// <summary>
    /// Tells whether a year is a leap year: divisible by 4 and not by 100, or divisible by 400.
    /// </summary>
    /// <param name="year">The year, which can be zero or negative.</param>
    /// <returns><see langword="true"/> for a leap year.</returns>
    internal static bool IsLeap(long year) => (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

    /// <summary>
    /// The number of days of a month.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <returns>The days of the month, 28 to 31.</returns>
    internal static int DaysInMonth(long year, int month) => month switch
    {
        2 => IsLeap(year) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        _ => 31,
    };

    /// <summary>
    /// The days from 1970-01-01 to a date; negative before it.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day of the month, 1 to the length of the month.</param>
    /// <returns>The number of days.</returns>
    internal static long DaysFromCivil(long year, int month, int day)
    {
        // The year starts in March, so the leap day is the last day of the year.
        long shifted = month <= 2 ? year - 1 : year;
        long era = FloorDivide(shifted, 400);
        long yearOfEra = shifted - (era * 400);
        long dayOfYear = (((153 * (month > 2 ? month - 3 : month + 9)) + 2) / 5) + day - 1;
        long dayOfEra = (yearOfEra * 365) + (yearOfEra / 4) - (yearOfEra / 100) + dayOfYear;
        return (era * 146_097) + dayOfEra - DaysToEpoch;
    }

    /// <summary>
    /// The date of a day counted from 1970-01-01.
    /// </summary>
    /// <param name="days">The number of days; negative before 1970-01-01.</param>
    /// <param name="year">The year, which can be zero or negative.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    internal static void CivilFromDays(long days, out long year, out int month, out int day)
    {
        long shifted = days + DaysToEpoch;
        long era = FloorDivide(shifted, 146_097);
        long dayOfEra = shifted - (era * 146_097);
        long yearOfEra = (dayOfEra - (dayOfEra / 1_460) + (dayOfEra / 36_524) - (dayOfEra / 146_096)) / 365;
        long dayOfYear = dayOfEra - ((365 * yearOfEra) + (yearOfEra / 4) - (yearOfEra / 100));
        long monthFromMarch = ((5 * dayOfYear) + 2) / 153;
        day = (int)(dayOfYear - (((153 * monthFromMarch) + 2) / 5) + 1);
        month = (int)(monthFromMarch < 10 ? monthFromMarch + 3 : monthFromMarch - 9);
        year = yearOfEra + (era * 400) + (month <= 2 ? 1 : 0);
    }

    /// <summary>
    /// The day of the week of a day counted from 1970-01-01.
    /// </summary>
    /// <param name="days">The number of days.</param>
    /// <returns>0 for Sunday to 6 for Saturday; 1970-01-01 is a Thursday, 4.</returns>
    internal static int DayOfWeek(long days) => (int)(((days + 4) % 7 + 7) % 7);

    /// <summary>
    /// The zero-based day of the year of a date.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day of the month.</param>
    /// <returns>0 for 1 January, 365 for 31 December of a leap year.</returns>
    internal static int DayOfYear(long year, int month, int day) =>
        DaysBeforeMonth[month - 1] + (month > 2 && IsLeap(year) ? 1 : 0) + day - 1;

    /// <summary>
    /// Divides rounding toward negative infinity.
    /// </summary>
    /// <param name="dividend">The dividend.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The floor of the quotient.</returns>
    internal static long FloorDivide(long dividend, long divisor)
    {
        long quotient = dividend / divisor;
        return dividend % divisor < 0 ? quotient - 1 : quotient;
    }
}

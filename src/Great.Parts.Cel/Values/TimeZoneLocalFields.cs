namespace Great.Parts.Cel.Values;

/// <summary>
/// The fields of the local date and time of an instant in a zone, from the library's own calendar. The fields are not
/// clamped to the range of <c>timestamp</c>: a moment at the edge can have year 0 or year 10000.
/// </summary>
internal readonly record struct TimeZoneLocalFields(
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    int Second,
    int Millisecond,
    int DayOfWeek,
    int DayOfYear)
{
    /// <summary>
    /// Finds the local fields of an instant for an offset.
    /// </summary>
    /// <param name="seconds">The instant in Unix seconds.</param>
    /// <param name="nanoseconds">The nanoseconds of the instant, 0 to 999999999.</param>
    /// <param name="offsetSeconds">The offset east of UTC in seconds.</param>
    /// <returns>The year, month, day, hour, minute, second, millisecond, weekday (0 is Sunday) and zero-based day of the year.</returns>
    internal static TimeZoneLocalFields Of(long seconds, int nanoseconds, int offsetSeconds)
    {
        long local = seconds + offsetSeconds;
        long days = CalendarRules.FloorDivide(local, CalendarRules.SecondsPerDay);
        int secondOfDay = (int)(local - (days * CalendarRules.SecondsPerDay));
        CalendarRules.CivilFromDays(days, out long year, out int month, out int day);
        return new TimeZoneLocalFields(
            (int)year,
            month,
            day,
            secondOfDay / 3600,
            secondOfDay % 3600 / 60,
            secondOfDay % 60,
            nanoseconds / 1_000_000,
            CalendarRules.DayOfWeek(days),
            CalendarRules.DayOfYear(year, month, day));
    }
}

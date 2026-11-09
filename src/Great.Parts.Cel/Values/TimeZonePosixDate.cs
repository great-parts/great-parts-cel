namespace Great.Parts.Cel.Values;

/// <summary>
/// The form of a date in a POSIX TZ rule.
/// </summary>
internal enum TimeZonePosixDateKind
{
    /// <summary><c>Jn</c>: the day of the year from 1, without 29 February.</summary>
    Julian,

    /// <summary><c>n</c>: the zero-based day of the year, with 29 February.</summary>
    DayOfYear,

    /// <summary><c>Mm.w.d</c>: the day of a week of a month.</summary>
    MonthWeekDay,
}

/// <summary>
/// The date and the time of day of one end of a daylight saving period in a POSIX TZ rule.
/// </summary>
/// <param name="Kind">The form of the date.</param>
/// <param name="Month">The month 1 to 12 of <see cref="TimeZonePosixDateKind.MonthWeekDay"/>.</param>
/// <param name="Week">The week 1 to 5 of <see cref="TimeZonePosixDateKind.MonthWeekDay"/>; 5 is the last.</param>
/// <param name="Day">The <c>n</c> of the day forms, or the weekday 0 (Sunday) to 6 of <see cref="TimeZonePosixDateKind.MonthWeekDay"/>.</param>
/// <param name="TimeSeconds">The time of day in seconds, which can be negative or above a day.</param>
internal readonly record struct TimeZonePosixDate(TimeZonePosixDateKind Kind, int Month, int Week, int Day, int TimeSeconds);

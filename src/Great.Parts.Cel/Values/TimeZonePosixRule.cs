namespace Great.Parts.Cel.Values;

/// <summary>
/// A POSIX TZ rule of a TZif footer: the standard offset and, for a zone with daylight saving time, the daylight offset and
/// the dates where it starts and ends. The names of the rule are not kept.
/// </summary>
/// <param name="StandardOffset">The standard offset in seconds east of UTC.</param>
/// <param name="HasDaylight"><see langword="true"/> when the rule has a daylight saving period.</param>
/// <param name="DaylightOffset">The daylight offset in seconds east of UTC; meaningful with <paramref name="HasDaylight"/>.</param>
/// <param name="Start">The date where daylight saving time starts, in standard time.</param>
/// <param name="End">The date where daylight saving time ends, in daylight time.</param>
internal sealed record TimeZonePosixRule(
    int StandardOffset,
    bool HasDaylight,
    int DaylightOffset,
    TimeZonePosixDate Start,
    TimeZonePosixDate End);

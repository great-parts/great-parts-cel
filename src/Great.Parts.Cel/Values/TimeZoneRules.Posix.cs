using System;

namespace Great.Parts.Cel.Values;

// Reading and applying a POSIX TZ rule, the footer of a TZif file: std offset [dst [offset] [,start,end]].
internal static partial class TimeZoneRules
{
    // The most hours a rule offset or a rule time can have: 24 * 7, the bound of Go tzset (RFC 9636 names 167).
    private const int MaxRuleHours = 168;

    // The rule that applies when a daylight name has no dates: the second Sunday of March to the first Sunday of November.
    private static ReadOnlySpan<byte> DefaultDates => ",M3.2.0,M11.1.0"u8;

    /// <summary>
    /// Reads the rule of a TZif footer.
    /// </summary>
    /// <param name="text">The bytes between the two line feeds of the footer.</param>
    /// <param name="rule">The rule, or <see langword="null"/> for an empty footer.</param>
    /// <returns><see langword="false"/> when the text is not a POSIX TZ rule.</returns>
    internal static bool TryParseRule(ReadOnlySpan<byte> text, out TimeZonePosixRule? rule)
    {
        rule = null;
        if (text.IsEmpty)
        {
            return true;
        }

        ReadOnlySpan<byte> rest = text;
        if (!SkipName(ref rest) || !TryReadOffset(ref rest, out int standard))
        {
            return false;
        }

        // The number of the rule is added to local time to get UTC, so the offset east of UTC is its negation.
        standard = -standard;
        if (rest.IsEmpty || rest[0] == (byte)',')
        {
            // The footer has no daylight saving time; the dates after the comma, if any, are not used.
            rule = new TimeZonePosixRule(standard, false, standard, default, default);
            return true;
        }

        return TryParseDaylight(ref rest, standard, out rule);
    }

    // The part of a rule after the standard offset: a daylight name, its offset and the dates.
    private static bool TryParseDaylight(ref ReadOnlySpan<byte> rest, int standard, out TimeZonePosixRule? rule)
    {
        rule = null;
        if (!SkipName(ref rest))
        {
            return false;
        }

        int daylight;
        if (rest.IsEmpty || rest[0] == (byte)',')
        {
            daylight = standard + 3600;
        }
        else
        {
            if (!TryReadOffset(ref rest, out daylight))
            {
                return false;
            }

            daylight = -daylight;
        }

        if (rest.IsEmpty)
        {
            rest = DefaultDates;
        }

        // The POSIX TZ definition has only a comma here; the reference implementation of the time zone database accepts a semicolon.
        if (rest[0] != (byte)',' && rest[0] != (byte)';')
        {
            return false;
        }

        rest = rest[1..];
        if (!TryReadDate(ref rest, out TimeZonePosixDate start) || rest.IsEmpty || rest[0] != (byte)',')
        {
            return false;
        }

        rest = rest[1..];
        if (!TryReadDate(ref rest, out TimeZonePosixDate end) || !rest.IsEmpty)
        {
            return false;
        }

        rule = new TimeZonePosixRule(standard, true, daylight, start, end);
        return true;
    }

    /// <summary>
    /// The offset a rule gives at an instant.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="seconds">The instant in Unix seconds.</param>
    /// <returns>The offset in seconds east of UTC.</returns>
    internal static int RuleOffsetAt(TimeZonePosixRule rule, long seconds)
    {
        if (!rule.HasDaylight)
        {
            return rule.StandardOffset;
        }

        // The year of the instant in UTC; the dates of the rule count from the start of that year.
        CalendarRules.CivilFromDays(CalendarRules.FloorDivide(seconds, CalendarRules.SecondsPerDay), out long year, out _, out _);
        long yearStart = CalendarRules.DaysFromCivil(year, 1, 1) * CalendarRules.SecondsPerDay;
        long starts = yearStart + SecondsIntoYear(rule.Start, year) + rule.Start.TimeSeconds - rule.StandardOffset;
        long ends = yearStart + SecondsIntoYear(rule.End, year) + rule.End.TimeSeconds - rule.DaylightOffset;
        if (starts <= ends)
        {
            return seconds >= starts && seconds < ends ? rule.DaylightOffset : rule.StandardOffset;
        }

        // The southern hemisphere: standard time lies between the end and the start.
        return seconds >= ends && seconds < starts ? rule.StandardOffset : rule.DaylightOffset;
    }

    // The seconds from the start of a year to the start of the day of a date, without the time of day.
    private static long SecondsIntoYear(TimeZonePosixDate date, long year)
    {
        long day;
        switch (date.Kind)
        {
            case TimeZonePosixDateKind.Julian:
                day = date.Day - 1;
                if (date.Day >= 60 && CalendarRules.IsLeap(year))
                {
                    day++;
                }

                break;
            case TimeZonePosixDateKind.DayOfYear:
                day = date.Day;
                break;
            default:
                int firstWeekday = CalendarRules.DayOfWeek(CalendarRules.DaysFromCivil(year, date.Month, 1));
                int dayOfMonth = (date.Day - firstWeekday + 7) % 7;
                int length = CalendarRules.DaysInMonth(year, date.Month);
                for (int week = 1; week < date.Week && dayOfMonth + 7 < length; week++)
                {
                    dayOfMonth += 7;
                }

                day = dayOfMonth + CalendarRules.DayOfYear(year, date.Month, 1);
                break;
        }

        return day * CalendarRules.SecondsPerDay;
    }

    // A name is either <...> with any characters but '>', or at least three characters up to the first digit, comma, plus or minus.
    private static bool SkipName(ref ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        if (text[0] == (byte)'<')
        {
            for (int close = 1; close < text.Length; close++)
            {
                if (text[close] == (byte)'>')
                {
                    text = text[(close + 1)..];
                    return true;
                }
            }

            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            byte b = text[i];
            if ((b >= (byte)'0' && b <= (byte)'9') || b == (byte)',' || b == (byte)'-' || b == (byte)'+')
            {
                if (i < 3)
                {
                    return false;
                }

                text = text[i..];
                return true;
            }
        }

        if (text.Length < 3)
        {
            return false;
        }

        text = default;
        return true;
    }

    // [+-]hh[:mm[:ss]]; the value is returned as written, so with the sign of the number and not of the offset east of UTC.
    private static bool TryReadOffset(ref ReadOnlySpan<byte> text, out int seconds)
    {
        seconds = 0;
        if (text.IsEmpty)
        {
            return false;
        }

        bool negative = false;
        if (text[0] == (byte)'+')
        {
            text = text[1..];
        }
        else if (text[0] == (byte)'-')
        {
            text = text[1..];
            negative = true;
        }

        if (!TryReadNumber(ref text, 0, MaxRuleHours, out int hours))
        {
            return false;
        }

        int value = hours * 3600;
        if (!text.IsEmpty && text[0] == (byte)':')
        {
            text = text[1..];
            if (!TryReadNumber(ref text, 0, 59, out int minutes))
            {
                return false;
            }

            value += minutes * 60;
            if (!text.IsEmpty && text[0] == (byte)':')
            {
                text = text[1..];
                if (!TryReadNumber(ref text, 0, 59, out int secondsPart))
                {
                    return false;
                }

                value += secondsPart;
            }
        }

        seconds = negative ? -value : value;
        return true;
    }

    // Jn, n or Mm.w.d, then an optional /time.
    private static bool TryReadDate(ref ReadOnlySpan<byte> text, out TimeZonePosixDate date)
    {
        date = default;
        if (text.IsEmpty)
        {
            return false;
        }

        TimeZonePosixDateKind kind;
        int month = 0;
        int week = 0;
        int day;
        if (text[0] == (byte)'J')
        {
            text = text[1..];
            kind = TimeZonePosixDateKind.Julian;
            if (!TryReadNumber(ref text, 1, 365, out day))
            {
                return false;
            }
        }
        else if (text[0] == (byte)'M')
        {
            text = text[1..];
            kind = TimeZonePosixDateKind.MonthWeekDay;
            if (!TryReadNumber(ref text, 1, 12, out month) || !TryReadDot(ref text)
                || !TryReadNumber(ref text, 1, 5, out week) || !TryReadDot(ref text)
                || !TryReadNumber(ref text, 0, 6, out day))
            {
                return false;
            }
        }
        else
        {
            kind = TimeZonePosixDateKind.DayOfYear;
            if (!TryReadNumber(ref text, 0, 365, out day))
            {
                return false;
            }
        }

        int time = 2 * 3600;
        if (!text.IsEmpty && text[0] == (byte)'/')
        {
            text = text[1..];
            if (!TryReadOffset(ref text, out time))
            {
                return false;
            }
        }

        date = new TimeZonePosixDate(kind, month, week, day, time);
        return true;
    }

    private static bool TryReadDot(ref ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty || text[0] != (byte)'.')
        {
            return false;
        }

        text = text[1..];
        return true;
    }

    // Decimal digits with a value from min to max; a value above max stops reading at once, so long digit runs cannot overflow.
    private static bool TryReadNumber(ref ReadOnlySpan<byte> text, int min, int max, out int value)
    {
        value = 0;
        int length = 0;
        while (length < text.Length && text[length] >= (byte)'0' && text[length] <= (byte)'9')
        {
            value = (value * 10) + (text[length] - (byte)'0');
            if (value > max)
            {
                return false;
            }

            length++;
        }

        if (length == 0 || value < min)
        {
            return false;
        }

        text = text[length..];
        return true;
    }
}

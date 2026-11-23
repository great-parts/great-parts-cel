using System;

namespace Great.Parts.Cel.Values;

// The text of the types date and time_of_day of the calendar set, read and written with ASCII digits and no culture: DateOnly and
// TimeOnly parse and format by the culture of the process, so none of their parsing and formatting members is used (they are banned).
internal static class CalendarTextRules
{
    /// <summary>The length of the text of a date: <c>YYYY-MM-DD</c>.</summary>
    internal const int DateLength = 10;

    /// <summary>The length of the longest text of a time of day: <c>HH:MM:SS.fffffff</c>.</summary>
    internal const int MaxTimeOfDayLength = 16;

    /// <summary>The ticks of 100 nanoseconds in a second.</summary>
    internal const long TicksPerSecond = 10_000_000;

    private const int FractionDigits = 7;

    /// <summary>
    /// Reads a date written exactly as <c>YYYY-MM-DD</c>: four ASCII digits for the year from 0001 to 9999, two for the month, two for a
    /// day that the month has.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The date when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    internal static bool TryParseDate(ReadOnlySpan<char> text, out DateOnly value)
    {
        value = default;
        if (text.Length != DateLength || text[4] != '-' || text[7] != '-'
            || !TryDigits(text, 0, 4, out int year) || !TryDigits(text, 5, 2, out int month) || !TryDigits(text, 8, 2, out int day))
        {
            return false;
        }

        if (year < 1 || month is < 1 or > 12 || day < 1 || day > CalendarRules.DaysInMonth(year, month))
        {
            return false;
        }

        value = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>
    /// Reads a time of day written as <c>HH:MM</c>, <c>HH:MM:SS</c> or <c>HH:MM:SS.f</c> with one to seven digits of fraction.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The time of day when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the text is a time of day.</returns>
    internal static bool TryParseTimeOfDay(ReadOnlySpan<char> text, out TimeOnly value)
    {
        value = default;
        if (text.Length < 5 || text[2] != ':' || !TryDigits(text, 0, 2, out int hours) || !TryDigits(text, 3, 2, out int minutes) || hours > 23 || minutes > 59)
        {
            return false;
        }

        long ticks = ((hours * 3600L) + (minutes * 60L)) * TicksPerSecond;
        if (text.Length > 5)
        {
            if (text.Length < 8 || text[5] != ':' || !TryDigits(text, 6, 2, out int seconds) || seconds > 59)
            {
                return false;
            }

            ticks += seconds * TicksPerSecond;
            if (text.Length > 8 && !TryAddFraction(text[8..], ref ticks))
            {
                return false;
            }
        }

        value = new TimeOnly(ticks);
        return true;
    }

    // The point and one to seven digits, as ticks.
    private static bool TryAddFraction(ReadOnlySpan<char> fraction, ref long ticks)
    {
        if (fraction[0] != '.' || fraction.Length < 2 || fraction.Length > FractionDigits + 1)
        {
            return false;
        }

        long scale = TicksPerSecond / 10;
        for (int i = 1; i < fraction.Length; i++)
        {
            int digit = fraction[i] - '0';
            if (digit is < 0 or > 9)
            {
                return false;
            }

            ticks += digit * scale;
            scale /= 10;
        }

        return true;
    }

    private static bool TryDigits(ReadOnlySpan<char> text, int start, int count, out int value)
    {
        value = 0;
        for (int i = start; i < start + count; i++)
        {
            int digit = text[i] - '0';
            if (digit is < 0 or > 9)
            {
                return false;
            }

            value = (value * 10) + digit;
        }

        return true;
    }

    /// <summary>
    /// Writes a date as <c>YYYY-MM-DD</c>.
    /// </summary>
    /// <param name="value">The date.</param>
    /// <param name="destination">At least <see cref="DateLength"/> characters.</param>
    /// <returns>The number of characters written, <see cref="DateLength"/>.</returns>
    internal static int WriteDate(DateOnly value, Span<char> destination)
    {
        WriteDigits(destination, 0, 4, value.Year);
        destination[4] = '-';
        WriteDigits(destination, 5, 2, value.Month);
        destination[7] = '-';
        WriteDigits(destination, 8, 2, value.Day);
        return DateLength;
    }

    /// <summary>
    /// The length of the text of a time of day, as <see cref="WriteTimeOfDay"/> writes it: 8, or 9 and the digits of a fraction that is
    /// not zero.
    /// </summary>
    /// <param name="value">The time of day.</param>
    /// <returns>The length, 8 to <see cref="MaxTimeOfDayLength"/>.</returns>
    internal static int TimeOfDayLength(TimeOnly value)
    {
        long fraction = value.Ticks % TicksPerSecond;
        return fraction == 0 ? 8 : 9 + SignificantDigits(fraction);
    }

    /// <summary>
    /// Writes a time of day as <c>HH:MM:SS</c>, followed by the point and the fraction without the zeros at its end when the
    /// fraction is not zero.
    /// </summary>
    /// <param name="value">The time of day.</param>
    /// <param name="destination">At least <see cref="MaxTimeOfDayLength"/> characters.</param>
    /// <returns>The number of characters written.</returns>
    internal static int WriteTimeOfDay(TimeOnly value, Span<char> destination)
    {
        long ticks = value.Ticks;
        long seconds = ticks / TicksPerSecond;
        long fraction = ticks % TicksPerSecond;
        WriteDigits(destination, 0, 2, (int)(seconds / 3600));
        destination[2] = ':';
        WriteDigits(destination, 3, 2, (int)(seconds / 60 % 60));
        destination[5] = ':';
        WriteDigits(destination, 6, 2, (int)(seconds % 60));
        if (fraction == 0)
        {
            return 8;
        }

        int digits = SignificantDigits(fraction);
        for (int i = digits; i < FractionDigits; i++)
        {
            fraction /= 10;
        }

        destination[8] = '.';
        WriteDigits(destination, 9, digits, (int)fraction);
        return 9 + digits;
    }

    // The digits of a fraction of seven digits that is not zero, without the zeros at its end.
    private static int SignificantDigits(long fraction)
    {
        int digits = FractionDigits;
        while (fraction % 10 == 0)
        {
            fraction /= 10;
            digits--;
        }

        return digits;
    }

    private static void WriteDigits(Span<char> destination, int start, int count, int value)
    {
        for (int i = start + count - 1; i >= start; i--)
        {
            destination[i] = (char)('0' + (value % 10));
            value /= 10;
        }
    }
}

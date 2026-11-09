using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The outcome of reading the text of a timestamp or a duration.
/// </summary>
internal enum TimeParseResult
{
    /// <summary>The text is valid and the value is set.</summary>
    Parsed,

    /// <summary>The text is not of the form: <c>InvalidArgument</c>.</summary>
    Invalid,

    /// <summary>The text is of the form but denotes a value outside the range of its type: <c>ConversionRange</c>.</summary>
    OutOfRange,
}

/// <summary>
/// RFC 3339 and duration text: reading, printing and the length of the printed text, over spans, without a culture, a
/// time zone, <c>DateTime</c> parsing or floating-point formatting.
/// </summary>
internal static class TimeTextRules
{
    /// <summary>The longest text of a timestamp, <c>9999-12-31T23:59:59.999999999Z</c>.</summary>
    internal const int MaxTimestampLength = 30;

    /// <summary>The longest text of a duration, <c>-9223372036.854775808s</c>.</summary>
    internal const int MaxDurationLength = 22;

    private const ulong TwoToThe63 = 1UL << 63;

    private const long SecondsPerHour = 3_600;

    /// <summary>
    /// Reads a timestamp from <c>YYYY-MM-DDThh:mm:ss</c>, an optional fraction of one or more digits, and <c>Z</c> or
    /// <c>±hh:mm</c>: ASCII digits, <c>T</c> and <c>Z</c> in upper case, month 01–12, a day of the month by the Gregorian
    /// rule (year 0000 is a leap year), hour 00–23, minute and second 00–59, offset hour 00–23 and minute 00–59. Digits of
    /// the fraction after the ninth are dropped. The syntax is checked first, then the day of the month, then the range.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The timestamp when the result is <see cref="TimeParseResult.Parsed"/>; otherwise the default value.</param>
    /// <returns>The outcome.</returns>
    internal static TimeParseResult TryParseTimestamp(ReadOnlySpan<char> text, out CelTimestamp value)
    {
        value = default;
        if (!TryReadFields(text, out TimestampFields fields))
        {
            return TimeParseResult.Invalid;
        }

        long local = (CalendarRules.DaysFromCivil(fields.Year, fields.Month, fields.Day) * CalendarRules.SecondsPerDay)
            + (fields.Hour * SecondsPerHour) + (fields.Minute * 60L) + fields.Second;
        long seconds = local - fields.Offset;
        if (seconds < CelTimestamp.MinSeconds || seconds > CelTimestamp.MaxSeconds)
        {
            return TimeParseResult.OutOfRange;
        }

        value = CelTimestamp.FromChecked(seconds, fields.Nanoseconds);
        return TimeParseResult.Parsed;
    }

    /// <summary>
    /// The number of characters of the text of a timestamp: 20, or 21 plus the digits of the fraction when the nanoseconds
    /// are not zero.
    /// </summary>
    /// <param name="value">The timestamp.</param>
    /// <returns>The length, at most <see cref="MaxTimestampLength"/>.</returns>
    internal static int TimestampLength(CelTimestamp value) =>
        value.Nanoseconds == 0 ? 20 : 21 + FractionDigits(value.Nanoseconds);

    /// <summary>
    /// Writes the text of a timestamp, <c>YYYY-MM-DDThh:mm:ssZ</c> in UTC and, when the nanoseconds are not zero, a
    /// <c>.</c> and the nine-digit nanoseconds without trailing zeros.
    /// </summary>
    /// <param name="value">The timestamp.</param>
    /// <param name="destination">The buffer, at least <see cref="TimestampLength"/> characters.</param>
    /// <returns>The number of characters written.</returns>
    internal static int WriteTimestamp(CelTimestamp value, Span<char> destination)
    {
        long days = CalendarRules.FloorDivide(value.Seconds, CalendarRules.SecondsPerDay);
        int secondOfDay = (int)(value.Seconds - (days * CalendarRules.SecondsPerDay));
        CalendarRules.CivilFromDays(days, out long year, out int month, out int day);

        WriteNumber(destination, 0, (int)year, 4);
        destination[4] = '-';
        WriteNumber(destination, 5, month, 2);
        destination[7] = '-';
        WriteNumber(destination, 8, day, 2);
        destination[10] = 'T';
        WriteNumber(destination, 11, secondOfDay / 3_600, 2);
        destination[13] = ':';
        WriteNumber(destination, 14, secondOfDay % 3_600 / 60, 2);
        destination[16] = ':';
        WriteNumber(destination, 17, secondOfDay % 60, 2);
        int length = 19;
        if (value.Nanoseconds != 0)
        {
            int digits = FractionDigits(value.Nanoseconds);
            destination[length++] = '.';
            WriteNumber(destination, length, value.Nanoseconds / Pow10(9 - digits), digits);
            length += digits;
        }

        destination[length++] = 'Z';
        return length;
    }

    /// <summary>
    /// Reads a duration by the grammar and the algorithm of <c>time.ParseDuration</c> of Go: an optional sign, then one or
    /// more groups of digits, an optional fraction and a unit (<c>ns</c>, <c>us</c>, <c>µs</c> U+00B5, <c>μs</c> U+03BC,
    /// <c>ms</c>, <c>s</c>, <c>m</c>, <c>h</c>); <c>0</c> alone is zero. A fraction is the only floating-point step, kept as Go has
    /// it so that it is cut the same way. An overflow at any step is <see cref="TimeParseResult.OutOfRange"/>, any other
    /// failure <see cref="TimeParseResult.Invalid"/>; the first failing check decides.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The duration when the result is <see cref="TimeParseResult.Parsed"/>; otherwise zero.</param>
    /// <returns>The outcome.</returns>
    internal static TimeParseResult TryParseDuration(ReadOnlySpan<char> text, out CelDuration value)
    {
        value = default;
        ReadOnlySpan<char> rest = text;
        bool negative = false;
        if (!rest.IsEmpty && rest[0] is '-' or '+')
        {
            negative = rest[0] == '-';
            rest = rest[1..];
        }

        if (rest.Length == 1 && rest[0] == '0')
        {
            return TimeParseResult.Parsed;
        }

        if (rest.IsEmpty)
        {
            return TimeParseResult.Invalid;
        }

        ulong total = 0;
        while (!rest.IsEmpty)
        {
            TimeParseResult group = ReadGroup(ref rest, out ulong nanoseconds);
            if (group != TimeParseResult.Parsed)
            {
                return group;
            }

            // The sum of two values of at most 2^63 wraps at exactly 2^64, as the uint64 of Go does.
            total = unchecked(total + nanoseconds);
            if (total > TwoToThe63)
            {
                return TimeParseResult.OutOfRange;
            }
        }

        if (negative)
        {
            value = new CelDuration(unchecked((long)(0 - total)));
            return TimeParseResult.Parsed;
        }

        if (total > long.MaxValue)
        {
            return TimeParseResult.OutOfRange;
        }

        value = new CelDuration((long)total);
        return TimeParseResult.Parsed;
    }

    /// <summary>
    /// The number of characters of the text of a duration: the sign, the seconds, and a <c>.</c> with the nine-digit
    /// remainder without trailing zeros when it is not zero, then <c>s</c>.
    /// </summary>
    /// <param name="nanoseconds">The duration.</param>
    /// <returns>The length, at most <see cref="MaxDurationLength"/>.</returns>
    internal static int DurationLength(long nanoseconds)
    {
        ulong magnitude = Magnitude(nanoseconds);
        int length = (nanoseconds < 0 ? 1 : 0) + DecimalDigits(magnitude / 1_000_000_000) + 1;
        uint remainder = (uint)(magnitude % 1_000_000_000);
        return remainder == 0 ? length : length + 1 + FractionDigits((int)remainder);
    }

    /// <summary>
    /// Writes the text of a duration, exactly and without floating point: <c>1.5s</c>, <c>-0.000000001s</c>, <c>3600s</c>, <c>0s</c>.
    /// </summary>
    /// <param name="nanoseconds">The duration.</param>
    /// <param name="destination">The buffer, at least <see cref="DurationLength"/> characters.</param>
    /// <returns>The number of characters written.</returns>
    internal static int WriteDuration(long nanoseconds, Span<char> destination)
    {
        ulong magnitude = Magnitude(nanoseconds);
        int length = 0;
        if (nanoseconds < 0)
        {
            destination[length++] = '-';
        }

        ulong seconds = magnitude / 1_000_000_000;
        int digits = DecimalDigits(seconds);
        for (int index = digits - 1; index >= 0; index--)
        {
            destination[length + index] = (char)('0' + (int)(seconds % 10));
            seconds /= 10;
        }

        length += digits;
        uint remainder = (uint)(magnitude % 1_000_000_000);
        if (remainder != 0)
        {
            int fractionDigits = FractionDigits((int)remainder);
            destination[length++] = '.';
            WriteNumber(destination, length, (int)(remainder / (uint)Pow10(9 - fractionDigits)), fractionDigits);
            length += fractionDigits;
        }

        destination[length++] = 's';
        return length;
    }

    // The fields of a timestamp text.
    private struct TimestampFields
    {
        internal int Year;
        internal int Month;
        internal int Day;
        internal int Hour;
        internal int Minute;
        internal int Second;
        internal int Nanoseconds;
        internal int Offset;
    }

    // The syntax of the date and the time, the offset, the ranges of the fields, then the day of the month.
    private static bool TryReadFields(ReadOnlySpan<char> text, out TimestampFields fields)
    {
        fields = default;
        if (text.Length < 20 || text[4] != '-' || text[7] != '-' || text[10] != 'T' || text[13] != ':' || text[16] != ':')
        {
            return false;
        }

        fields.Year = Digits(text, 0, 4);
        fields.Month = Digits(text, 5, 2);
        fields.Day = Digits(text, 8, 2);
        fields.Hour = Digits(text, 11, 2);
        fields.Minute = Digits(text, 14, 2);
        fields.Second = Digits(text, 17, 2);
        if ((fields.Year | fields.Month | fields.Day | fields.Hour | fields.Minute | fields.Second) < 0)
        {
            return false;
        }

        int position = 19;
        if (text[position] == '.')
        {
            position++;
            int digits = ReadNanoseconds(text[position..], out fields.Nanoseconds);
            if (digits == 0)
            {
                return false;
            }

            position += digits;
        }

        return TryReadOffset(text[position..], out fields.Offset) && AreInRange(fields);
    }

    // The ranges of the fields, then the day of the month by the Gregorian rule.
    private static bool AreInRange(TimestampFields fields) =>
        fields.Month is >= 1 and <= 12
        && fields.Hour <= 23
        && fields.Minute <= 59
        && fields.Second <= 59
        && fields.Day >= 1
        && fields.Day <= CalendarRules.DaysInMonth(fields.Year, fields.Month);

    // The digits of a fraction of a second at the start of the text: the first nine are the nanoseconds, padded with zeros
    // on the right, and the rest are dropped without rounding. Returns the number of digits read.
    private static int ReadNanoseconds(ReadOnlySpan<char> text, out int nanoseconds)
    {
        nanoseconds = 0;
        int digits = 0;
        while (digits < text.Length && IsDigit(text[digits]))
        {
            if (digits < 9)
            {
                nanoseconds = (nanoseconds * 10) + (text[digits] - '0');
            }

            digits++;
        }

        for (int padding = digits; padding < 9; padding++)
        {
            nanoseconds *= 10;
        }

        return digits;
    }

    // One group of a duration: digits, an optional fraction, a unit. Gives the nanoseconds of the group and moves the text
    // past it; the first failing check decides, an overflow being OutOfRange.
    private static TimeParseResult ReadGroup(ref ReadOnlySpan<char> rest, out ulong nanoseconds)
    {
        nanoseconds = 0;
        if (rest[0] != '.' && !IsDigit(rest[0]))
        {
            return TimeParseResult.Invalid;
        }

        TimeParseResult read = ReadWhole(ref rest, out ulong integer, out int whole);
        if (read != TimeParseResult.Parsed)
        {
            return read;
        }

        ulong fraction = 0;
        double scale = 1;
        int fractionDigits = 0;
        if (!rest.IsEmpty && rest[0] == '.')
        {
            rest = rest[1..];
            fractionDigits = ReadFraction(rest, out fraction, out scale);
            rest = rest[fractionDigits..];
        }

        if (whole == 0 && fractionDigits == 0)
        {
            return TimeParseResult.Invalid;
        }

        ulong unit = ReadUnit(ref rest);
        return unit == 0 ? TimeParseResult.Invalid : Scale(integer, fraction, scale, unit, out nanoseconds);
    }

    // The digits before the point as an unsigned integer; more than 2^63 is OutOfRange. Gives the number of digits.
    private static TimeParseResult ReadWhole(ref ReadOnlySpan<char> rest, out ulong integer, out int digits)
    {
        integer = 0;
        digits = 0;
        for (; digits < rest.Length && IsDigit(rest[digits]); digits++)
        {
            if (integer > TwoToThe63 / 10)
            {
                return TimeParseResult.OutOfRange;
            }

            integer = (integer * 10) + (ulong)(rest[digits] - '0');
            if (integer > TwoToThe63)
            {
                return TimeParseResult.OutOfRange;
            }
        }

        rest = rest[digits..];
        return TimeParseResult.Parsed;
    }

    // The unit at the start of the text, up to the next digit or point, and moves the text past it; 0 for an empty text or
    // a text that is no unit.
    private static ulong ReadUnit(ref ReadOnlySpan<char> rest)
    {
        int length = 0;
        while (length < rest.Length && rest[length] != '.' && !IsDigit(rest[length]))
        {
            length++;
        }

        ulong unit = length == 0 ? 0 : UnitOf(rest[..length]);
        rest = rest[length..];
        return unit;
    }

    // The nanoseconds of a group: the whole part times the unit, plus the fraction in the one floating-point step that Go has.
    private static TimeParseResult Scale(ulong integer, ulong fraction, double scale, ulong unit, out ulong nanoseconds)
    {
        nanoseconds = 0;
        if (integer > TwoToThe63 / unit)
        {
            return TimeParseResult.OutOfRange;
        }

        integer *= unit;
        if (fraction > 0)
        {
            integer += (ulong)((double)fraction * ((double)unit / scale));
            if (integer > TwoToThe63)
            {
                return TimeParseResult.OutOfRange;
            }
        }

        nanoseconds = integer;
        return TimeParseResult.Parsed;
    }

    // The digits of a fraction after the point: their value and the power of ten that they stand for. A digit that would take
    // the value above 2^63, and every digit after it, is skipped without a factor of ten, as Go does.
    private static int ReadFraction(ReadOnlySpan<char> text, out ulong fraction, out double scale)
    {
        fraction = 0;
        scale = 1;
        bool overflow = false;
        int count = 0;
        for (; count < text.Length && IsDigit(text[count]); count++)
        {
            if (overflow)
            {
                continue;
            }

            if (fraction > (TwoToThe63 - 1) / 10)
            {
                overflow = true;
                continue;
            }

            ulong next = (fraction * 10) + (ulong)(text[count] - '0');
            if (next > TwoToThe63)
            {
                overflow = true;
                continue;
            }

            fraction = next;
            scale *= 10;
        }

        return count;
    }

    // The number of nanoseconds of a unit; 0 for a text that is no unit.
    private static ulong UnitOf(ReadOnlySpan<char> unit) => unit.Length switch
    {
        1 => unit[0] switch
        {
            's' => 1_000_000_000UL,
            'm' => 60_000_000_000UL,
            'h' => 3_600_000_000_000UL,
            _ => 0,
        },
        2 when unit[1] == 's' => unit[0] switch
        {
            'n' => 1UL,
            'u' or 'µ' or 'μ' => 1_000UL,
            'm' => 1_000_000UL,
            _ => 0,
        },
        _ => 0,
    };

    // The offset east of UTC in seconds from "Z" or "±hh:mm", the whole rest of the text.
    private static bool TryReadOffset(ReadOnlySpan<char> text, out int offset)
    {
        offset = 0;
        if (text.Length == 1)
        {
            return text[0] == 'Z';
        }

        if (text.Length != 6 || (text[0] != '+' && text[0] != '-') || text[3] != ':')
        {
            return false;
        }

        int hours = Digits(text, 1, 2);
        int minutes = Digits(text, 4, 2);
        if (hours is < 0 or > 23 || minutes is < 0 or > 59)
        {
            return false;
        }

        int seconds = (hours * 3_600) + (minutes * 60);
        offset = text[0] == '-' ? -seconds : seconds;
        return true;
    }

    // The value of count ASCII digits at the position, or -1 when one of them is not a digit.
    private static int Digits(ReadOnlySpan<char> text, int start, int count)
    {
        int value = 0;
        for (int index = start; index < start + count; index++)
        {
            if (!IsDigit(text[index]))
            {
                return -1;
            }

            value = (value * 10) + (text[index] - '0');
        }

        return value;
    }

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

    // The magnitude of a duration as an unsigned number, so that long.MinValue is no special case.
    private static ulong Magnitude(long value) => value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;

    // The digits of a nine-digit fraction of a second after the trailing zeros are dropped.
    private static int FractionDigits(int nanoseconds)
    {
        int digits = 9;
        while (nanoseconds % 10 == 0)
        {
            nanoseconds /= 10;
            digits--;
        }

        return digits;
    }

    private static int Pow10(int exponent)
    {
        int result = 1;
        for (int index = 0; index < exponent; index++)
        {
            result *= 10;
        }

        return result;
    }

    private static int DecimalDigits(ulong value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }

    // Writes a non-negative number with leading zeros in exactly width characters.
    private static void WriteNumber(Span<char> destination, int start, int value, int width)
    {
        for (int index = start + width - 1; index >= start; index--)
        {
            destination[index] = (char)('0' + (value % 10));
            value /= 10;
        }
    }
}

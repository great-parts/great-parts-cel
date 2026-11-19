using System;

namespace Great.Parts.Cel.Values;

// The resolution of a time zone name and the offset of a zone at an instant (docs/design/time-zones.md).
internal static partial class TimeZoneRules
{
    /// <summary>
    /// Resolves a time zone name: <c>UTC</c>, a fixed offset <c>[+-]HH:MM</c>, a name with <c>:</c> that is no offset, or
    /// a name to look up in the database. It allocates nothing for a name that is not looked up in the operating system, or that
    /// the database of the operating system has kept.
    /// </summary>
    /// <param name="database">The database of the environment, or <see langword="null"/> when it has none.</param>
    /// <param name="name">The name; the case counts.</param>
    /// <param name="meter">
    /// The meter of an evaluation with a cost limit, charged <see cref="SystemZoneSource.Cost"/> before a name is looked up in
    /// the database of the operating system; <see langword="null"/> without a limit.
    /// </param>
    /// <returns>The offset, the zone, or the reason there is none.</returns>
    internal static TimeZoneResolution Resolve(CelTimeZoneDatabase? database, ReadOnlySpan<char> name, IEvaluationMeter? meter = null)
    {
        if (name.SequenceEqual("UTC"))
        {
            return TimeZoneResolution.Fixed(0);
        }

        if (TryReadFixedOffset(name, out int offset))
        {
            return TimeZoneResolution.Fixed(offset);
        }

        if (HasColon(name))
        {
            return TimeZoneResolution.Invalid;
        }

        if (database is { IsSystem: true })
        {
            // The charge comes before the search and does not depend on its outcome.
            meter?.Charge(SystemZoneSource.Cost);
            return SystemZoneSource.Find(name) is { } system ? TimeZoneResolution.InSystemZone(system) : TimeZoneResolution.NotFound;
        }

        ZoneData? zone = database?.Find(name);
        return zone is null ? TimeZoneResolution.NotFound : TimeZoneResolution.InZone(zone);
    }

    /// <summary>
    /// The offset of a zone at an instant.
    /// </summary>
    /// <param name="zone">The zone.</param>
    /// <param name="seconds">The instant in Unix seconds.</param>
    /// <returns>The offset in seconds east of UTC.</returns>
    internal static int OffsetAt(ZoneData zone, long seconds)
    {
        long[] transitions = zone.Transitions;
        if (transitions.Length == 0)
        {
            return zone.Rule is { } only ? RuleOffsetAt(only, seconds) : zone.TypeOffsets[0];
        }

        if (seconds < transitions[0])
        {
            return zone.TypeOffsets[0];
        }

        // The last transition at or before the instant.
        int low = 0;
        int high = transitions.Length - 1;
        while (low < high)
        {
            int middle = low + ((high - low + 1) >> 1);
            if (transitions[middle] <= seconds)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (low == transitions.Length - 1 && zone.Rule is { } rule)
        {
            return RuleOffsetAt(rule, seconds);
        }

        return zone.TypeOffsets[zone.TransitionTypes[low]];
    }

    /// <summary>
    /// Tells whether a name contains a colon, which would make it read as a fixed offset.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name has <c>:</c>.</returns>
    internal static bool HasColon(ReadOnlySpan<char> name)
    {
        foreach (char c in name)
        {
            if (c == ':')
            {
                return true;
            }
        }

        return false;
    }

    // [+-]?HH:MM: an optional sign, two digits 00 to 23, a colon, two digits 00 to 59.
    private static bool TryReadFixedOffset(ReadOnlySpan<char> name, out int seconds)
    {
        seconds = 0;
        int start = 0;
        bool negative = false;
        if (name.Length == 6 && (name[0] == '+' || name[0] == '-'))
        {
            negative = name[0] == '-';
            start = 1;
        }
        else if (name.Length != 5)
        {
            return false;
        }

        if (!IsDigit(name[start]) || !IsDigit(name[start + 1]) || name[start + 2] != ':'
            || !IsDigit(name[start + 3]) || !IsDigit(name[start + 4]))
        {
            return false;
        }

        int hours = ((name[start] - '0') * 10) + (name[start + 1] - '0');
        int minutes = ((name[start + 3] - '0') * 10) + (name[start + 4] - '0');
        if (hours > 23 || minutes > 59)
        {
            return false;
        }

        int value = (hours * 3600) + (minutes * 60);
        seconds = negative ? -value : value;
        return true;
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';
}

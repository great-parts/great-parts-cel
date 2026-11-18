using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL <c>google.protobuf.Duration</c>: a signed time span of whole seconds and nanoseconds, in the range of the protobuf
/// type — from -315 576 000 000.999999999 to 315 576 000 000.999999999 seconds, about ±10 000 years.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TimeSpan"/> counts in ticks of 100 nanoseconds and has a wider range, so a
/// <see cref="TimeSpan"/> converts to a duration exactly when it is within the range of a duration,
/// while a duration converts back only when its nanoseconds are a multiple of 100. The functions of time are on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/standard-library.md#time">Standard library</see>, the conversions on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md#time">Type mapping</see>.
/// </para>
/// <para>The structure is immutable and can be shared between threads. <c>default(CelDuration)</c> is zero.</para>
/// </remarks>
public readonly struct CelDuration : IEquatable<CelDuration>, IComparable<CelDuration>
{
    // The range of the seconds of a duration, as google.protobuf.Duration defines it.
    internal const long MaxSeconds = 315_576_000_000;

    private const long NanosecondsPerSecond = 1_000_000_000;
    private const long TicksPerSecond = 10_000_000;

    // The nanoseconds have the sign of the seconds, or the seconds are zero.
    private readonly long _seconds;
    private readonly int _nanoseconds;

    // Rebuilds a duration whose parts a check has already accepted: a payload of a value, the result of an operation.
    private CelDuration(long seconds, int nanoseconds, bool _)
    {
        _seconds = seconds;
        _nanoseconds = nanoseconds;
    }

    /// <summary>
    /// Creates a duration of <paramref name="totalNanoseconds"/> nanoseconds.
    /// </summary>
    /// <param name="totalNanoseconds">The length of the time span in nanoseconds; negative for a negative span.</param>
    public CelDuration(long totalNanoseconds)
    {
        _seconds = totalNanoseconds / NanosecondsPerSecond;
        _nanoseconds = (int)(totalNanoseconds % NanosecondsPerSecond);
    }

    /// <summary>
    /// Creates a duration from seconds and nanoseconds, as <c>google.protobuf.Duration</c> stores it.
    /// </summary>
    /// <param name="seconds">The whole seconds, from -315 576 000 000 to 315 576 000 000; negative for a negative span.</param>
    /// <param name="nanoseconds">
    /// The nanoseconds, from -999 999 999 to 999 999 999, with the sign of <paramref name="seconds"/> or zero when the seconds are zero.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="seconds"/> or <paramref name="nanoseconds"/> is out of its range, or the nanoseconds have the other sign than a
    /// non-zero number of seconds.
    /// </exception>
    public CelDuration(long seconds, int nanoseconds)
    {
        if (seconds is < -MaxSeconds or > MaxSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "The seconds are not from -315 576 000 000 to 315 576 000 000.");
        }

        if (nanoseconds is < -999_999_999 or > 999_999_999 || (seconds > 0 && nanoseconds < 0) || (seconds < 0 && nanoseconds > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(nanoseconds), nanoseconds, "The nanoseconds are not from -999 999 999 to 999 999 999 with the sign of the seconds.");
        }

        _seconds = seconds;
        _nanoseconds = nanoseconds;
    }

    // The parts are in range and the nanoseconds have the sign of the seconds.
    internal static CelDuration FromChecked(long seconds, int nanoseconds) => new(seconds, nanoseconds, false);

    // The duration of ticks of 100 nanoseconds, or false when its seconds are out of the range of a duration.
    internal static bool TryFromTicks(long ticks, out CelDuration value)
    {
        long seconds = ticks / TicksPerSecond;
        if (seconds is < -MaxSeconds or > MaxSeconds)
        {
            value = default;
            return false;
        }

        value = new CelDuration(seconds, (int)(ticks % TicksPerSecond) * 100, false);
        return true;
    }

    /// <summary>
    /// The whole seconds of the duration; negative for a negative span.
    /// </summary>
    public long Seconds => _seconds;

    /// <summary>
    /// The nanoseconds after <see cref="Seconds"/>, from -999 999 999 to 999 999 999, with the sign of the duration.
    /// </summary>
    public int Nanoseconds => _nanoseconds;

    /// <summary>
    /// The length of the time span in nanoseconds; negative for a negative span.
    /// </summary>
    /// <exception cref="InvalidOperationException">The length is not a <see cref="long"/>: the duration is longer than about 292 years.</exception>
    public long TotalNanoseconds
    {
        get
        {
            if (!TryGetTotalNanoseconds(out long total))
            {
                throw new InvalidOperationException("The duration is longer than a 64-bit number of nanoseconds; read Seconds and Nanoseconds.");
            }

            return total;
        }
    }

    // The length in nanoseconds, or false when it does not fit a long. A product that overflows implies a total that overflows, since the
    // nanoseconds have the sign of the seconds.
    internal bool TryGetTotalNanoseconds(out long total)
    {
        if (_seconds is > long.MaxValue / NanosecondsPerSecond or < long.MinValue / NanosecondsPerSecond)
        {
            total = 0;
            return false;
        }

        long scaled = _seconds * NanosecondsPerSecond;
        if ((_nanoseconds > 0 && scaled > long.MaxValue - _nanoseconds) || (_nanoseconds < 0 && scaled < long.MinValue - _nanoseconds))
        {
            total = 0;
            return false;
        }

        total = scaled + _nanoseconds;
        return true;
    }

    /// <summary>
    /// Converts a <see cref="TimeSpan"/> to a duration, exactly.
    /// </summary>
    /// <param name="value">The time span.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> does not fit the range of a duration: it is longer than 315 576 000 000 seconds.
    /// </exception>
    public static CelDuration FromTimeSpan(TimeSpan value)
    {
        if (!TryFromTicks(value.Ticks, out CelDuration duration))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The time span does not fit the range of a duration, 315 576 000 000 seconds.");
        }

        return duration;
    }

    /// <summary>
    /// Converts the duration to a <see cref="TimeSpan"/>, if it can be represented exactly.
    /// </summary>
    /// <param name="value">The time span when the method returns <see langword="true"/>; otherwise zero.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="Nanoseconds"/> is a multiple of 100, the resolution of
    /// <see cref="TimeSpan"/>; the conversion never rounds. The range of a <see cref="TimeSpan"/> holds every duration.
    /// </returns>
    public bool TryGetTimeSpan(out TimeSpan value)
    {
        if (_nanoseconds % 100 != 0)
        {
            value = default;
            return false;
        }

        value = TimeSpan.FromTicks((_seconds * TicksPerSecond) + (_nanoseconds / 100));
        return true;
    }

    /// <summary>
    /// Compares two durations by length.
    /// </summary>
    /// <param name="other">The duration to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this duration is shorter than, as long as or longer than <paramref name="other"/>.</returns>
    public int CompareTo(CelDuration other)
    {
        int bySeconds = _seconds.CompareTo(other._seconds);
        return bySeconds != 0 ? bySeconds : _nanoseconds.CompareTo(other._nanoseconds);
    }

    /// <summary>
    /// Compares two durations: they are equal when they have the same length.
    /// </summary>
    /// <param name="other">The duration to compare with.</param>
    /// <returns><see langword="true"/> when the durations are equal.</returns>
    public bool Equals(CelDuration other)
    {
        return _seconds == other._seconds && _nanoseconds == other._nanoseconds;
    }

    /// <summary>
    /// Compares this duration with an object, as <see cref="Equals(CelDuration)"/> does when the object is a <see cref="CelDuration"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal duration.</returns>
    public override bool Equals(object? obj)
    {
        return obj is CelDuration other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelDuration)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_seconds, _nanoseconds);
    }

    /// <summary>
    /// Formats the duration as <c>string()</c> does in CEL: the exact number of seconds as a decimal number
    /// without trailing zeros, followed by <c>s</c>, for example <c>1.5s</c>, <c>-0.000000001s</c> or
    /// <c>3600s</c>. The text does not depend on the current culture.
    /// </summary>
    /// <returns>The duration in seconds.</returns>
    public override string ToString()
    {
        return string.Create(TimeTextRules.DurationLength(this), this, static (span, value) => TimeTextRules.WriteDuration(value, span));
    }

    /// <summary>
    /// Tells whether two durations are equal, as <see cref="Equals(CelDuration)"/> does.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when the durations are equal.</returns>
    public static bool operator ==(CelDuration left, CelDuration right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tells whether two durations differ, as the negation of <see cref="Equals(CelDuration)"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when the durations differ.</returns>
    public static bool operator !=(CelDuration left, CelDuration right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is shorter than <paramref name="right"/>; a negative duration is shorter than zero.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is shorter.</returns>
    public static bool operator <(CelDuration left, CelDuration right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is longer than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is longer.</returns>
    public static bool operator >(CelDuration left, CelDuration right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is shorter than or as long as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not longer.</returns>
    public static bool operator <=(CelDuration left, CelDuration right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is longer than or as long as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not shorter.</returns>
    public static bool operator >=(CelDuration left, CelDuration right)
    {
        return left.CompareTo(right) >= 0;
    }
}

using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL <c>google.protobuf.Duration</c>: a signed time span of a whole number of nanoseconds, any
/// <see cref="long"/> value — about ±292 years.
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
    private readonly long _nanoseconds;

    /// <summary>
    /// Creates a duration of <paramref name="totalNanoseconds"/> nanoseconds.
    /// </summary>
    /// <param name="totalNanoseconds">The length of the time span in nanoseconds; negative for a negative span.</param>
    public CelDuration(long totalNanoseconds)
    {
        _nanoseconds = totalNanoseconds;
    }

    /// <summary>
    /// The length of the time span in nanoseconds; negative for a negative span.
    /// </summary>
    public long TotalNanoseconds => _nanoseconds;

    /// <summary>
    /// Converts a <see cref="TimeSpan"/> to a duration, exactly.
    /// </summary>
    /// <param name="value">The time span.</param>
    /// <returns>The duration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> does not fit the range of a duration: its length in nanoseconds is not a <see cref="long"/>.
    /// </exception>
    public static CelDuration FromTimeSpan(TimeSpan value)
    {
        long ticks = value.Ticks;
        if (ticks is < long.MinValue / 100 or > long.MaxValue / 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The time span does not fit the range of a duration, a 64-bit number of nanoseconds.");
        }

        return new CelDuration(ticks * 100);
    }

    /// <summary>
    /// Converts the duration to a <see cref="TimeSpan"/>, if it can be represented exactly.
    /// </summary>
    /// <param name="value">The time span when the method returns <see langword="true"/>; otherwise zero.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="TotalNanoseconds"/> is a multiple of 100, the resolution of
    /// <see cref="TimeSpan"/>; the conversion never rounds.
    /// </returns>
    public bool TryGetTimeSpan(out TimeSpan value)
    {
        if (_nanoseconds % 100 != 0)
        {
            value = default;
            return false;
        }

        value = TimeSpan.FromTicks(_nanoseconds / 100);
        return true;
    }

    /// <summary>
    /// Compares two durations by length.
    /// </summary>
    /// <param name="other">The duration to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this duration is shorter than, as long as or longer than <paramref name="other"/>.</returns>
    public int CompareTo(CelDuration other)
    {
        return _nanoseconds.CompareTo(other._nanoseconds);
    }

    /// <summary>
    /// Compares two durations: they are equal when they have the same number of nanoseconds.
    /// </summary>
    /// <param name="other">The duration to compare with.</param>
    /// <returns><see langword="true"/> when the durations are equal.</returns>
    public bool Equals(CelDuration other)
    {
        return _nanoseconds == other._nanoseconds;
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
        return _nanoseconds.GetHashCode();
    }

    /// <summary>
    /// Formats the duration as <c>string()</c> does in CEL: the exact number of seconds as a decimal number
    /// without trailing zeros, followed by <c>s</c>, for example <c>1.5s</c>, <c>-0.000000001s</c> or
    /// <c>3600s</c>. The text does not depend on the current culture.
    /// </summary>
    /// <returns>The duration in seconds.</returns>
    public override string ToString()
    {
        return string.Create(TimeTextRules.DurationLength(_nanoseconds), _nanoseconds, static (span, value) => TimeTextRules.WriteDuration(value, span));
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
        return left._nanoseconds < right._nanoseconds;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is longer than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is longer.</returns>
    public static bool operator >(CelDuration left, CelDuration right)
    {
        return left._nanoseconds > right._nanoseconds;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is shorter than or as long as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not longer.</returns>
    public static bool operator <=(CelDuration left, CelDuration right)
    {
        return left._nanoseconds <= right._nanoseconds;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is longer than or as long as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first duration.</param>
    /// <param name="right">The second duration.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not shorter.</returns>
    public static bool operator >=(CelDuration left, CelDuration right)
    {
        return left._nanoseconds >= right._nanoseconds;
    }
}

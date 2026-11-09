using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL <c>google.protobuf.Timestamp</c>: a point in time in UTC with nanosecond precision, from
/// 0001-01-01T00:00:00Z to 9999-12-31T23:59:59.999999999Z, without leap seconds.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DateTimeOffset"/> counts in ticks of 100 nanoseconds, so every <see cref="DateTimeOffset"/>
/// converts to a timestamp exactly, while a timestamp converts back only when its nanoseconds are a
/// multiple of 100. A timestamp carries no time zone or offset; functions such as <c>getHours</c> take
/// the time zone as an argument. The functions of time are on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/standard-library.md#time">Standard library</see>, the conversions to and from the types of .NET
/// on the page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md#time">Type mapping</see>.
/// </para>
/// <para>
/// The structure is immutable and can be shared between threads. <c>default(CelTimestamp)</c> is the
/// Unix epoch, 1970-01-01T00:00:00Z.
/// </para>
/// </remarks>
public readonly struct CelTimestamp : IEquatable<CelTimestamp>, IComparable<CelTimestamp>
{
    // The range of Unix seconds of a timestamp, 0001-01-01T00:00:00Z to 9999-12-31T23:59:59Z.
    internal const long MinSeconds = -62_135_596_800;
    internal const long MaxSeconds = 253_402_300_799;

    // The seconds from 0001-01-01T00:00:00Z to the Unix epoch, and the ticks of a second.
    private const long UnixEpochSeconds = -MinSeconds;
    private const long TicksPerSecond = 10_000_000;

    private readonly long _seconds;
    private readonly int _nanoseconds;

    // Rebuilds a timestamp whose parts a check has already accepted: a payload of a value, the result of reading a text.
    private CelTimestamp(long seconds, int nanoseconds, bool _)
    {
        _seconds = seconds;
        _nanoseconds = nanoseconds;
    }

    /// <summary>
    /// Creates a timestamp from seconds since the Unix epoch, 1970-01-01T00:00:00Z, and nanoseconds
    /// within the second, as <c>google.protobuf.Timestamp</c> stores it.
    /// </summary>
    /// <param name="seconds">Seconds since the Unix epoch; negative before it.</param>
    /// <param name="nanoseconds">Nanoseconds after <paramref name="seconds"/>, from 0 to 999 999 999.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="nanoseconds"/> is out of its range, or the point in time is before
    /// 0001-01-01T00:00:00Z or after 9999-12-31T23:59:59.999999999Z.
    /// </exception>
    public CelTimestamp(long seconds, int nanoseconds)
    {
        if (nanoseconds is < 0 or > 999_999_999)
        {
            throw new ArgumentOutOfRangeException(nameof(nanoseconds), nanoseconds, "The nanoseconds are not from 0 to 999 999 999.");
        }

        if (seconds is < MinSeconds or > MaxSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "The point in time is not from 0001-01-01T00:00:00Z to 9999-12-31T23:59:59.999999999Z.");
        }

        _seconds = seconds;
        _nanoseconds = nanoseconds;
    }

    internal static CelTimestamp FromChecked(long seconds, int nanoseconds) => new(seconds, nanoseconds, false);

    // The moment of ticks of 100 nanoseconds since 0001-01-01T00:00:00Z, which every DateTime and DateTimeOffset holds in range.
    internal static CelTimestamp FromUtcTicks(long ticks) =>
        new((ticks / TicksPerSecond) - UnixEpochSeconds, (int)(ticks % TicksPerSecond) * 100, false);

    /// <summary>
    /// Seconds since the Unix epoch, 1970-01-01T00:00:00Z; negative before it.
    /// </summary>
    public long Seconds => _seconds;

    /// <summary>
    /// Nanoseconds after <see cref="Seconds"/>, from 0 to 999 999 999.
    /// </summary>
    public int Nanoseconds => _nanoseconds;

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> to the same point in time, exactly. The offset is not kept.
    /// </summary>
    /// <param name="value">The point in time.</param>
    /// <returns>The timestamp.</returns>
    public static CelTimestamp FromDateTimeOffset(DateTimeOffset value)
    {
        return FromUtcTicks(value.UtcTicks);
    }

    /// <summary>
    /// Converts the timestamp to a <see cref="DateTimeOffset"/> with offset zero, if it can be represented exactly.
    /// </summary>
    /// <param name="value">
    /// The point in time when the method returns <see langword="true"/>; otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <see cref="Nanoseconds"/> is a multiple of 100, the resolution of
    /// <see cref="DateTimeOffset"/>; the conversion never rounds.
    /// </returns>
    public bool TryGetDateTimeOffset(out DateTimeOffset value)
    {
        if (_nanoseconds % 100 != 0)
        {
            value = default;
            return false;
        }

        value = new DateTimeOffset(((_seconds + UnixEpochSeconds) * TicksPerSecond) + (_nanoseconds / 100), TimeSpan.Zero);
        return true;
    }

    /// <summary>
    /// Compares two timestamps by the point in time.
    /// </summary>
    /// <param name="other">The timestamp to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this timestamp is earlier than, the same as or later than <paramref name="other"/>.</returns>
    public int CompareTo(CelTimestamp other)
    {
        int bySeconds = _seconds.CompareTo(other._seconds);
        return bySeconds != 0 ? bySeconds : _nanoseconds.CompareTo(other._nanoseconds);
    }

    /// <summary>
    /// Compares two timestamps: they are equal when they denote the same point in time.
    /// </summary>
    /// <param name="other">The timestamp to compare with.</param>
    /// <returns><see langword="true"/> when the timestamps are equal.</returns>
    public bool Equals(CelTimestamp other)
    {
        return _seconds == other._seconds && _nanoseconds == other._nanoseconds;
    }

    /// <summary>
    /// Compares this timestamp with an object, as <see cref="Equals(CelTimestamp)"/> does when the object is a <see cref="CelTimestamp"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal timestamp.</returns>
    public override bool Equals(object? obj)
    {
        return obj is CelTimestamp other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelTimestamp)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_seconds, _nanoseconds);
    }

    /// <summary>
    /// Formats the timestamp as <c>string()</c> does in CEL: RFC 3339 in UTC with the suffix <c>Z</c> and
    /// the fraction of a second without trailing zeros, for example <c>2026-10-24T08:15:30.5Z</c>.
    /// The text does not depend on the current culture.
    /// </summary>
    /// <returns>The timestamp in RFC 3339 form.</returns>
    public override string ToString()
    {
        return string.Create(TimeTextRules.TimestampLength(this), this, static (span, value) => TimeTextRules.WriteTimestamp(value, span));
    }

    /// <summary>
    /// Tells whether two timestamps are equal, as <see cref="Equals(CelTimestamp)"/> does.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when the timestamps are equal.</returns>
    public static bool operator ==(CelTimestamp left, CelTimestamp right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tells whether two timestamps differ, as the negation of <see cref="Equals(CelTimestamp)"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when the timestamps differ.</returns>
    public static bool operator !=(CelTimestamp left, CelTimestamp right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is earlier than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is earlier.</returns>
    public static bool operator <(CelTimestamp left, CelTimestamp right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is later than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is later.</returns>
    public static bool operator >(CelTimestamp left, CelTimestamp right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is earlier than or the same as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not later.</returns>
    public static bool operator <=(CelTimestamp left, CelTimestamp right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is later than or the same as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not earlier.</returns>
    public static bool operator >=(CelTimestamp left, CelTimestamp right)
    {
        return left.CompareTo(right) >= 0;
    }
}

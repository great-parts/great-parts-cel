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
/// the time zone as an argument.
/// </para>
/// <para>
/// The structure is immutable and can be shared between threads. <c>default(CelTimestamp)</c> is the
/// Unix epoch, 1970-01-01T00:00:00Z.
/// </para>
/// </remarks>
public readonly struct CelTimestamp : IEquatable<CelTimestamp>, IComparable<CelTimestamp>
{
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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Seconds since the Unix epoch, 1970-01-01T00:00:00Z; negative before it.
    /// </summary>
    public long Seconds => throw new NotImplementedException();

    /// <summary>
    /// Nanoseconds after <see cref="Seconds"/>, from 0 to 999 999 999.
    /// </summary>
    public int Nanoseconds => throw new NotImplementedException();

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> to the same point in time, exactly. The offset is not kept.
    /// </summary>
    /// <param name="value">The point in time.</param>
    /// <returns>The timestamp.</returns>
    public static CelTimestamp FromDateTimeOffset(DateTimeOffset value)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    /// <summary>
    /// Compares two timestamps by the point in time.
    /// </summary>
    /// <param name="other">The timestamp to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this timestamp is earlier than, the same as or later than <paramref name="other"/>.</returns>
    public int CompareTo(CelTimestamp other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Compares two timestamps: they are equal when they denote the same point in time.
    /// </summary>
    /// <param name="other">The timestamp to compare with.</param>
    /// <returns><see langword="true"/> when the timestamps are equal.</returns>
    public bool Equals(CelTimestamp other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Compares this timestamp with an object, as <see cref="Equals(CelTimestamp)"/> does when the object is a <see cref="CelTimestamp"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal timestamp.</returns>
    public override bool Equals(object? obj)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelTimestamp)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Formats the timestamp as <c>string()</c> does in CEL: RFC 3339 in UTC with the suffix <c>Z</c> and
    /// the fraction of a second without trailing zeros, for example <c>2026-10-24T08:15:30.5Z</c>.
    /// The text does not depend on the current culture.
    /// </summary>
    /// <returns>The timestamp in RFC 3339 form.</returns>
    public override string ToString()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether two timestamps are equal, as <see cref="Equals(CelTimestamp)"/> does.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when the timestamps are equal.</returns>
    public static bool operator ==(CelTimestamp left, CelTimestamp right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether two timestamps differ, as the negation of <see cref="Equals(CelTimestamp)"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when the timestamps differ.</returns>
    public static bool operator !=(CelTimestamp left, CelTimestamp right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is earlier than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is earlier.</returns>
    public static bool operator <(CelTimestamp left, CelTimestamp right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is later than <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is later.</returns>
    public static bool operator >(CelTimestamp left, CelTimestamp right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is earlier than or the same as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not later.</returns>
    public static bool operator <=(CelTimestamp left, CelTimestamp right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether <paramref name="left"/> is later than or the same as <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first timestamp.</param>
    /// <param name="right">The second timestamp.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not earlier.</returns>
    public static bool operator >=(CelTimestamp left, CelTimestamp right)
    {
        throw new NotImplementedException();
    }
}

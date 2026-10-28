using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Values;

/// <summary>
/// An IANA time zone database built from TZif data that the application supplies, for CEL functions
/// that take a time zone, such as <c>timestamp.getHours('Europe/Moscow')</c>.
/// </summary>
/// <remarks>
/// <para>
/// The library never reads time zone data itself: not from the operating system, files or environment
/// variables, and it does not use <c>TimeZoneInfo</c>, whose data and names differ between
/// operating systems. The application reads TZif files (RFC 9636) that it trusts — on Linux and macOS the
/// system zoneinfo directory, elsewhere and for reproducible results the files of a pinned tzdata
/// release shipped with the application — and passes their bytes here. The database is then added to
/// an environment builder and becomes part of the immutable environment.
/// </para>
/// <para>
/// Time zone names in expressions resolve as follows: <c>UTC</c> needs no database; <c>[+-]HH:MM</c> is a
/// fixed offset and needs no database; any other name is an IANA identifier, matched by ordinal,
/// case-sensitive comparison against <see cref="ZoneIds"/>. A name that is not in the database, or any
/// IANA name when the environment has no database, is an evaluation error
/// <see cref="Diagnostics.CelErrorCode.TimeZoneNotFound"/>, never a result for another time zone.
/// Adding a different database to an environment that already has one is a build error
/// <see cref="Diagnostics.CelErrorCode.TimeZoneDatabaseConflict"/>.
/// </para>
/// <para>The database is immutable and can be shared between threads and environments.</para>
/// </remarks>
public sealed class CelTimeZoneDatabase
{
    private CelTimeZoneDatabase()
    {
    }

    /// <summary>
    /// A database without time zones: only <c>UTC</c> and fixed offsets resolve.
    /// </summary>
    public static CelTimeZoneDatabase Empty => throw new NotImplementedException();

    /// <summary>
    /// The version of the data given by the application, for example <c>2026a</c>, or <see langword="null"/>.
    /// It is informational: the library does not interpret it, and the message of
    /// <see cref="Diagnostics.CelErrorCode.TimeZoneNotFound"/> shows it.
    /// </summary>
    public string? Version => throw new NotImplementedException();

    /// <summary>
    /// The identifiers of the time zones in ordinal order. A link such as <c>US/Central</c> is an identifier
    /// of its own, with the same data as its target.
    /// </summary>
    public ImmutableArray<string> ZoneIds => throw new NotImplementedException();

    /// <summary>
    /// Creates a database from pairs of an IANA identifier and the TZif data of that time zone. Every
    /// entry is read and validated at once, so an invalid file is reported here and not at evaluation.
    /// </summary>
    /// <param name="zones">
    /// Pairs of an identifier, such as <c>Europe/Moscow</c>, and the bytes of its TZif file, version 2 or
    /// later. A link is given as a separate pair with the bytes of its target. The bytes are copied.
    /// </param>
    /// <param name="version">The version of the data, for messages only; <see langword="null"/> when unknown.</param>
    /// <returns>The database.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="zones"/> or an identifier is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The message names the identifier: the data is not valid TZif; the data has leap second records
    /// (CEL time has no leap seconds, as files from the <c>right/</c> directory have); the identifier is
    /// empty or contains <c>:</c>, which makes it read as a fixed offset; the same identifier is given
    /// twice with different data.
    /// </exception>
    public static CelTimeZoneDatabase Create(
        IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>> zones,
        string? version = null)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether the database has a time zone with this identifier, by ordinal, case-sensitive comparison.
    /// </summary>
    /// <param name="zoneId">The IANA identifier.</param>
    /// <returns><see langword="true"/> when the identifier is in <see cref="ZoneIds"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="zoneId"/> is <see langword="null"/>.</exception>
    public bool Contains(string zoneId)
    {
        throw new NotImplementedException();
    }
}

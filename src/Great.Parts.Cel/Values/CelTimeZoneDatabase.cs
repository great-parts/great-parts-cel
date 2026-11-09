using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

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
/// <para>
/// The recipe <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/time-zone-database.md">Time zone database</see> shows how to read the files and
/// build a database; the rules for a name are on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/standard-library.md#time-zone-names">Standard library</see>.
/// </para>
/// <para>The database is immutable and can be shared between threads and environments.</para>
/// </remarks>
public sealed class CelTimeZoneDatabase
{
    private static readonly CelTimeZoneDatabase EmptyInstance = new([], [], null);

    // The identifiers in ordinal order and the zone of each, at the same index.
    private readonly string[] _ids;
    private readonly ZoneData[] _zones;

    private CelTimeZoneDatabase(string[] ids, ZoneData[] zones, string? version)
    {
        _ids = ids;
        _zones = zones;
        Version = version;
        ZoneIds = ImmutableCollectionsMarshal.AsImmutableArray(ids);
    }

    /// <summary>
    /// A database without time zones: only <c>UTC</c> and fixed offsets resolve.
    /// </summary>
    public static CelTimeZoneDatabase Empty => EmptyInstance;

    /// <summary>
    /// The version of the data given by the application, for example <c>2026a</c>, or <see langword="null"/>.
    /// It is informational: the library does not interpret it, and the message of
    /// <see cref="Diagnostics.CelErrorCode.TimeZoneNotFound"/> shows it.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// The identifiers of the time zones in ordinal order. A link such as <c>US/Central</c> is an identifier
    /// of its own, with the same data as its target.
    /// </summary>
    public ImmutableArray<string> ZoneIds { get; }

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
        ArgumentNullException.ThrowIfNull(zones);
        Dictionary<string, (ReadOnlyMemory<byte> Data, ZoneData Zone)> seen = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, ReadOnlyMemory<byte>> pair in zones)
        {
            string id = pair.Key;
            ArgumentNullException.ThrowIfNull(id, nameof(zones));
            if (id.Length == 0)
            {
                throw new ArgumentException("a time zone identifier is empty", nameof(zones));
            }

            if (TimeZoneRules.HasColon(id))
            {
                throw new ArgumentException(
                    $"time zone identifier '{id}' has ':', so the name would read as a fixed offset", nameof(zones));
            }

            bool known = seen.TryGetValue(id, out (ReadOnlyMemory<byte> Data, ZoneData Zone) previous);
            if (known && previous.Data.Span.SequenceEqual(pair.Value.Span))
            {
                continue;
            }

            TimeZoneRules.TzifFailure failure = TimeZoneRules.TryReadTzif(pair.Value.Span, out ZoneData? zone);
            if (failure != TimeZoneRules.TzifFailure.None)
            {
                throw new ArgumentException(FailureMessage(id, failure), nameof(zones));
            }

            if (known)
            {
                throw new ArgumentException($"time zone '{id}' is given twice with different data", nameof(zones));
            }

            seen.Add(id, (pair.Value, zone!));
        }

        string[] ids = new string[seen.Count];
        ZoneData[] data = new ZoneData[seen.Count];
        int index = 0;
        foreach (KeyValuePair<string, (ReadOnlyMemory<byte> Data, ZoneData Zone)> entry in seen)
        {
            ids[index] = entry.Key;
            data[index] = entry.Value.Zone;
            index++;
        }

        Array.Sort(ids, data, StringComparer.Ordinal);
        return new CelTimeZoneDatabase(ids, data, version);
    }

    /// <summary>
    /// Tells whether the database has a time zone with this identifier, by ordinal, case-sensitive comparison.
    /// </summary>
    /// <param name="zoneId">The IANA identifier.</param>
    /// <returns><see langword="true"/> when the identifier is in <see cref="ZoneIds"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="zoneId"/> is <see langword="null"/>.</exception>
    public bool Contains(string zoneId)
    {
        ArgumentNullException.ThrowIfNull(zoneId);
        return Find(zoneId) is not null;
    }

    /// <summary>
    /// Finds the zone of an identifier by a binary search over the ordered identifiers, by ordinal comparison and without allocation.
    /// </summary>
    /// <param name="zoneId">The identifier.</param>
    /// <returns>The zone, or <see langword="null"/> when the database has none.</returns>
    internal ZoneData? Find(ReadOnlySpan<char> zoneId)
    {
        int low = 0;
        int high = _ids.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int order = zoneId.SequenceCompareTo(_ids[middle].AsSpan());
            if (order == 0)
            {
                return _zones[middle];
            }

            if (order < 0)
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return null;
    }

    private static string FailureMessage(string id, TimeZoneRules.TzifFailure failure)
    {
        if (failure == TimeZoneRules.TzifFailure.LeapSeconds)
        {
            return $"time zone '{id}': the data has leap second records, which CEL time does not have (files of the right/ directory are not supported)";
        }

        string detail = failure switch
        {
            TimeZoneRules.TzifFailure.BadMagic => "bad magic",
            TimeZoneRules.TzifFailure.UnsupportedVersion => "unsupported version",
            TimeZoneRules.TzifFailure.Version1 => "version 1 has no 64-bit data",
            TimeZoneRules.TzifFailure.Cut => "the data is cut",
            TimeZoneRules.TzifFailure.TransitionsOrder => "transitions are not in increasing order",
            TimeZoneRules.TzifFailure.UnknownType => "a transition has an unknown local time type",
            TimeZoneRules.TzifFailure.NoType => "no local time type",
            TimeZoneRules.TzifFailure.FooterMissing => "the footer is missing",
            _ => "the footer is not a POSIX TZ rule",
        };
        return $"time zone '{id}': the data is not valid TZif: {detail}";
    }
}

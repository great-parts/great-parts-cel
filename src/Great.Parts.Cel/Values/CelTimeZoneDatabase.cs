using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Values;

/// <summary>
/// An IANA time zone database for CEL functions that take a time zone, such as
/// <c>timestamp.getHours('Europe/Moscow')</c>: built from TZif data that the application supplies, or the database of the
/// operating system that <see cref="FromSystem"/> returns.
/// </summary>
/// <remarks>
/// <para>
/// A database from <see cref="Create"/> is the data the application gives: the library does not read time zone data from
/// the operating system, files or environment variables for it. The application reads TZif files (RFC 9636) that it
/// trusts — on Linux and macOS the system zoneinfo directory, elsewhere and for reproducible results the files of a pinned
/// tzdata release shipped with the application — and passes their bytes here. The only database whose data the .NET
/// platform reads, through <c>TimeZoneInfo</c>, is the one of <see cref="FromSystem"/>, by an explicit call of the
/// application; its results depend on the machine. The database is then added to an environment builder and becomes part
/// of the immutable environment.
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

    // The one database of the zones of the operating system. Making it reads nothing: the platform is asked only for a name.
    private static readonly CelTimeZoneDatabase SystemInstance = new([], [], null, isSystem: true);

    // The identifiers in ordinal order and the zone of each, at the same index.
    private readonly string[] _ids;
    private readonly ZoneData[] _zones;

    private CelTimeZoneDatabase(string[] ids, ZoneData[] zones, string? version, bool isSystem = false)
    {
        _ids = ids;
        _zones = zones;
        Version = version;
        ZoneIds = ImmutableCollectionsMarshal.AsImmutableArray(ids);
        IsSystem = isSystem;
    }

    // Whether this is the database of the operating system, whose zones SystemZoneSource finds and which has no data of its own.
    internal bool IsSystem { get; }

    /// <summary>
    /// A database without time zones: only <c>UTC</c> and fixed offsets resolve.
    /// </summary>
    public static CelTimeZoneDatabase Empty => EmptyInstance;

    /// <summary>
    /// The version of the data given by the application, for example <c>2026a</c>, or <see langword="null"/>; always
    /// <see langword="null"/> for the database of <see cref="FromSystem"/>. It is informational: the library does not
    /// interpret it, and the message of <see cref="Diagnostics.CelErrorCode.TimeZoneNotFound"/> shows it.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// The identifiers of the time zones in ordinal order. A link such as <c>US/Central</c> is an identifier
    /// of its own, with the same data as its target. Empty for the database of <see cref="FromSystem"/>: the operating
    /// system gives no complete list of IANA identifiers, so that database has none.
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
    /// <remarks>
    /// For the database of <see cref="FromSystem"/> the method checks the name and asks the platform exactly as an
    /// expression does, so it answers whether an expression would find the zone now.
    /// </remarks>
    /// <param name="zoneId">The IANA identifier.</param>
    /// <returns>
    /// <see langword="true"/> when the identifier is in <see cref="ZoneIds"/>, or, for the database of
    /// <see cref="FromSystem"/>, when the platform finds a zone with this IANA identifier.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="zoneId"/> is <see langword="null"/>.</exception>
    public bool Contains(string zoneId)
    {
        ArgumentNullException.ThrowIfNull(zoneId);
        return IsSystem ? SystemZoneSource.Find(zoneId) is not null : Find(zoneId) is not null;
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

    /// <summary>
    /// Returns the database of the time zones of the operating system, which the .NET platform reads with
    /// <c>TimeZoneInfo</c>: an application that adds it to an environment lets expressions use the IANA time zones of the
    /// machine without shipping TZif files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The platform reads the data, not the library: on Linux and macOS the zoneinfo directory, <c>/usr/share/zoneinfo</c> or
    /// the directory in the environment variable <c>TZDIR</c>; on Windows the registry, with IANA identifiers through ICU; in
    /// a browser the database built into the runtime. The library passes the platform only a name that passed the check of
    /// IANA names: 1 to 64 UTF-16 code units, parts separated by <c>/</c>, each part not empty and made of Latin letters,
    /// digits, <c>_</c>, <c>-</c> and <c>+</c>, and a first part other than <c>posix</c> and <c>right</c>. Any other name is
    /// the evaluation error <see cref="Diagnostics.CelErrorCode.TimeZoneNotFound"/> without a request to the platform.
    /// </para>
    /// <para>
    /// A zone is found only by its IANA identifier: a zone that the platform finds but that has no IANA identifier, such as
    /// a Windows identifier like <c>Russian Standard Time</c>, is <see cref="Diagnostics.CelErrorCode.TimeZoneNotFound"/> on
    /// every operating system. On Windows without ICU, such as Windows Server 2019, no IANA identifier is found, so every
    /// zone name other than <c>UTC</c> and fixed offsets is that error; so is every name in a browser application that
    /// publishes with invariant time zones. A zone that is not found is never replaced by the local zone or by UTC.
    /// </para>
    /// <para>
    /// Results depend on the time zone data of the machine, unlike with a database from <see cref="Create"/>. A found zone is
    /// kept by the database, so the answer for a found name does not change until the process ends; a name that was not
    /// found is not remembered and can be found later if the machine gets the zone. The number of kept zones is bounded by
    /// the number of zones of the operating system. An access to a zone of this database costs the same constant number of
    /// cost units whether the zone is kept, found or not found, as the page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">Cost</see> gives.
    /// </para>
    /// <para>
    /// The database has no list of zones: <see cref="ZoneIds"/> is empty and <see cref="Version"/> is
    /// <see langword="null"/>. <see cref="Contains(string)"/> checks a name and asks the platform as an expression does. The
    /// method returns the same instance on every call; the instance can be shared between threads and environments.
    /// </para>
    /// </remarks>
    /// <returns>The database of the operating system.</returns>
    public static CelTimeZoneDatabase FromSystem() => SystemInstance;
}

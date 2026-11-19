#pragma warning disable RS0030 // The only file that reads TimeZoneInfo: the database of CelTimeZoneDatabase.FromSystem() is the one place where the platform supplies zones, by an explicit call of the application, and only for a name that passed TimeZoneNameRules.

using System;
using System.Collections.Concurrent;
using System.Security;

namespace Great.Parts.Cel.Values;

// The zones of the operating system, read through TimeZoneInfo (docs/design/time-zones-system.md). A name goes to the platform
// only after the check of TimeZoneNameRules; the zones that the platform found are kept for the life of the process, so the
// memory is bounded by the zones of the operating system, and a miss is not kept.
internal static class SystemZoneSource
{
    /// <summary>
    /// The units that an access to the database of the operating system costs beyond the receiver: a constant, whether the name is
    /// found, kept, missed or refused by the check of the form (docs/cost.md).
    /// </summary>
    internal const ulong Cost = 2_000;

    private static readonly ConcurrentDictionary<string, Zone> Found = new(StringComparer.Ordinal);

    // Looks up a kept zone by a span of the name, without a string.
    private static readonly ConcurrentDictionary<string, Zone>.AlternateLookup<ReadOnlySpan<char>> FoundByText =
        Found.GetAlternateLookup<ReadOnlySpan<char>>();

    // Counted per thread, so that a test sees only its own requests while other tests resolve zones in parallel.
    [ThreadStatic]
    private static long _platformCalls;

    /// <summary>
    /// The number of requests of the current thread to <see cref="TimeZoneInfo.FindSystemTimeZoneById(string)"/>, for tests of the
    /// names that must not reach the platform and of the zones that are kept.
    /// </summary>
    internal static long PlatformCalls => _platformCalls;

    /// <summary>
    /// Finds a zone of the operating system by its IANA identifier. A name outside the grammar of IANA names, a name the platform
    /// does not know, and a zone without an IANA identifier, such as one with a Windows identifier, are not found.
    /// </summary>
    /// <param name="name">The name; the case counts.</param>
    /// <returns>The zone, or <see langword="null"/>.</returns>
    internal static Zone? Find(ReadOnlySpan<char> name)
    {
        if (!TimeZoneNameRules.IsIanaName(name))
        {
            return null;
        }

        return FoundByText.TryGetValue(name, out Zone? kept) ? kept : AskPlatform(name.ToString());
    }

    // The platform is asked only for a name that passed the check; a zone it found is kept, a miss is not.
    private static Zone? AskPlatform(string name)
    {
        _platformCalls++;
        TimeZoneInfo information;
        try
        {
            information = TimeZoneInfo.FindSystemTimeZoneById(name);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or SecurityException)
        {
            return null;
        }

        if (!information.HasIanaId)
        {
            return null;
        }

        Zone zone = new(information);
        return Found.GetOrAdd(name, zone);
    }

    /// <summary>
    /// A zone of the operating system: the offset at an instant comes from the platform; the calendar of the library makes the
    /// fields from the instant and the offset.
    /// </summary>
    internal sealed class Zone
    {
        private readonly TimeZoneInfo _information;

        internal Zone(TimeZoneInfo information)
        {
            _information = information;
        }

        /// <summary>
        /// The offset of the zone at an instant, in whole seconds east of UTC. The instants of a timestamp are inside the range
        /// of <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="seconds">The instant in Unix seconds.</param>
        /// <returns>The offset in seconds.</returns>
        internal int OffsetAt(long seconds) =>
            (int)(_information.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(seconds)).Ticks / TimeSpan.TicksPerSecond);
    }
}

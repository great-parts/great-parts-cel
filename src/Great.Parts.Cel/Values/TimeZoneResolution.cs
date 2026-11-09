namespace Great.Parts.Cel.Values;

/// <summary>
/// What a time zone name stands for.
/// </summary>
internal enum TimeZoneResolutionKind
{
    /// <summary>A fixed offset: <c>UTC</c>, or the form <c>[+-]HH:MM</c>.</summary>
    FixedOffset,

    /// <summary>A zone of the time zone database.</summary>
    Zone,

    /// <summary>The name contains <c>:</c> and is not a fixed offset.</summary>
    Invalid,

    /// <summary>The name is not in the database, or there is no database.</summary>
    NotFound,
}

/// <summary>
/// The result of resolving a time zone name: a fixed offset, a zone, or one of two failures. The offset at an instant is
/// found without allocation.
/// </summary>
internal readonly struct TimeZoneResolution
{
    private readonly ZoneData? _zone;
    private readonly int _fixedOffset;

    private TimeZoneResolution(TimeZoneResolutionKind kind, int fixedOffset, ZoneData? zone)
    {
        Kind = kind;
        _fixedOffset = fixedOffset;
        _zone = zone;
    }

    /// <summary>What the name stands for.</summary>
    internal TimeZoneResolutionKind Kind { get; }

    /// <summary>The name is a fixed offset in seconds east of UTC.</summary>
    internal static TimeZoneResolution Fixed(int offsetSeconds) => new(TimeZoneResolutionKind.FixedOffset, offsetSeconds, null);

    /// <summary>The name is a zone of the database.</summary>
    internal static TimeZoneResolution InZone(ZoneData zone) => new(TimeZoneResolutionKind.Zone, 0, zone);

    /// <summary>The name has <c>:</c> and is no fixed offset.</summary>
    internal static TimeZoneResolution Invalid => new(TimeZoneResolutionKind.Invalid, 0, null);

    /// <summary>The name is not in the database.</summary>
    internal static TimeZoneResolution NotFound => new(TimeZoneResolutionKind.NotFound, 0, null);

    /// <summary>
    /// The offset of the zone or of the fixed offset at an instant; for a failure, 0.
    /// </summary>
    /// <param name="seconds">The instant in Unix seconds.</param>
    /// <returns>The offset in seconds east of UTC.</returns>
    internal int OffsetAt(long seconds) =>
        _zone is null ? _fixedOffset : TimeZoneRules.OffsetAt(_zone, seconds);
}

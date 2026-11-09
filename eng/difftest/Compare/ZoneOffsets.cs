using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsCheck;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>
/// The comparison of time zone offsets, <c>ENG822</c> (docs/design/eng-difftest.md, "Findings and output"): the offsets of
/// every zone of the time zone data file at generated instants. The path of the file is a parameter.
/// </summary>
internal static class ZoneOffsets
{
    // Generated instants lie in the years 1800 to 2199, in whole seconds.
    private static readonly long FirstSecond = new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    private static readonly long EndSecond = new DateTimeOffset(2200, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>
    /// <paramref name="count"/> instants in whole seconds of the years 1800 to 2199 that depend on the seed of the run alone, and
    /// after them five fixed instants: 1800-01-01, 1900-01-01, 1970-01-01, 2037-01-01 and 2100-01-01 UTC.
    /// </summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="count">The number of instants drawn from the seed.</param>
    internal static IReadOnlyList<DateTimeOffset> Instants(ulong seed, int count)
    {
        PCG pcg = CaseGenerator.CaseState(seed, ulong.MaxValue);
        return [.. Enumerable.Range(0, count)
            .Select(_ => DateTimeOffset.FromUnixTimeSeconds(FirstSecond + (long)(pcg.Next64() % (ulong)(EndSecond - FirstSecond)))),
            .. FixedInstants];
    }

    /// <summary>The five fixed instants that join the drawn ones, in UTC.</summary>
    internal static IReadOnlyList<DateTimeOffset> FixedInstants { get; } =
    [
        new(1800, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new(2037, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero),
    ];

    /// <summary>
    /// Compares the offsets of every zone of the time zone data file at the instants. A zone gives one finding, at its first
    /// instant with another offset, at the place <c>(1,1)</c> of the file.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <param name="path">The path of the time zone data file relative to the root, with <c>/</c>: a zip archive with one entry per zone.</param>
    /// <param name="instants">The instants.</param>
    /// <param name="library">The library.</param>
    /// <param name="oracle">The oracle.</param>
    /// <returns>The <c>ENG822</c> findings in the order of the zones in the file.</returns>
    internal static IReadOnlyList<Finding> Compare(
        string root,
        string path,
        IReadOnlyList<DateTimeOffset> instants,
        ICaseSide library,
        ICaseSide oracle)
    {
        List<Finding> findings = [];
        foreach (string zone in ZoneData.Names(Path.Combine(root, path)))
        {
            foreach (DateTimeOffset instant in instants)
            {
                TimeSpan go = oracle.ZoneOffset(zone, instant);
                TimeSpan we = library.ZoneOffset(zone, instant);
                if (go != we)
                {
                    findings.Add(new Finding(
                        path,
                        1,
                        1,
                        FindingSeverity.Error,
                        EngCodes.Eng822,
                        $"time zone offset differs from Go: zone '{zone}' at {Instant(instant)}: Go {Offset(go)}, we {Offset(we)}"));
                    break;
                }
            }
        }

        return findings;
    }

    /// <summary>An instant in UTC in ISO 8601 with <c>Z</c>: <c>2026-11-04T12:00:00Z</c>.</summary>
    /// <param name="instant">The instant.</param>
    internal static string Instant(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>An offset as <c>±hh:mm</c>, or <c>±hh:mm:ss</c> when it has seconds.</summary>
    /// <param name="offset">The offset.</param>
    internal static string Offset(TimeSpan offset)
    {
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        TimeSpan magnitude = offset.Duration();
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"{sign}{(int)magnitude.TotalHours:00}:{magnitude.Minutes:00}");
        return magnitude.Seconds == 0 ? text : string.Create(CultureInfo.InvariantCulture, $"{text}:{magnitude.Seconds:00}");
    }
}

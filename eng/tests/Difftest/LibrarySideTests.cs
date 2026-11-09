using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using Any = Google.Protobuf.WellKnownTypes.Any;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The library side of eng/difftest (docs/design/eng-difftest.md, "A run", step 2, and "Findings and output"): the database of the
// time zone data file and the offsets of its zones, with a database written in the test.
public sealed class LibrarySideTests
{
    [Fact]
    public void LibraryZoneOffset_GivesTheOffsetsOfTheRowsOfAZoneIncludingSeconds()
    {
        using TempDirectory root = new();
        string file = WriteZones(
            root,
            ("America/New_York", Tzif([-2_717_650_800], [1], [-17_762, -18_000], "EST5")),
            ("Asia/Kolkata", Tzif([], [], [19_800], "IST-5:30")),
            ("Europe/Berlin", Tzif([0], [1], [0, 3600], "CET-1CEST,M3.5.0,M10.5.0/3")));
        LibrarySide side = Build(file);

        Assert.Equal(new TimeSpan(0, 0, -17_762), side.ZoneOffset("America/New_York", new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromHours(-5), side.ZoneOffset("America/New_York", new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new TimeSpan(5, 30, 0), side.ZoneOffset("Asia/Kolkata", new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new TimeSpan(5, 30, 0), side.ZoneOffset("Asia/Kolkata", new DateTimeOffset(2199, 12, 31, 23, 59, 59, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromHours(1), side.ZoneOffset("Europe/Berlin", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromHours(2), side.ZoneOffset("Europe/Berlin", new DateTimeOffset(2024, 7, 15, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void LibraryZoneOffset_OfAZoneThatTheDatabaseLacks_IsAFailureOfTheSide()
    {
        using TempDirectory root = new();
        LibrarySide side = Build(WriteZones(root, ("Asia/Kolkata", Tzif([], [], [19_800], "IST-5:30"))));

        SideFailureException failure = Assert.Throws<SideFailureException>(() => side.ZoneOffset("Mars/Base", DateTimeOffset.UnixEpoch));

        Assert.StartsWith("the library gives no local time for zone 'Mars/Base' at 1970-01-01T00:00:00Z: ", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZoneThatTheLibraryRefuses_IsAProblemOfStep2NamingTheZone()
    {
        using TempDirectory root = new();
        string file = WriteZones(root, ("Asia/Kolkata", Tzif([], [], [19_800], "IST-5:30")), ("Bad/Zone", [1, 2, 3]));

        bool built = LibrarySide.TryBuild(Manifest(), file, out LibrarySide? side, out string problem);

        Assert.False(built);
        Assert.Null(side);
        Assert.StartsWith("the library refuses the time zone data file: ", problem, StringComparison.Ordinal);
        Assert.Contains("Bad/Zone", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void LibrarySide_EvaluatesTimeThroughTheDatabaseOfTheFile()
    {
        using TempDirectory root = new();
        LibrarySide side = Build(WriteZones(root, ("Europe/Berlin", Tzif([0], [1], [0, 3600], "CET-1CEST,M3.5.0,M10.5.0/3"))));

        SideOutcome known = side.Evaluate("timestamp('2024-07-15T12:00:00Z').getHours('Europe/Berlin')", new Dictionary<string, Value>());
        SideOutcome unknown = side.Evaluate("timestamp('2024-07-15T12:00:00Z').getHours('Mars/Base')", new Dictionary<string, Value>());
        SideOutcome time = side.Evaluate("timestamp('2024-07-15T12:00:00Z') + duration('1h')", new Dictionary<string, Value>());

        Assert.Equal(new Value { Int64Value = 14 }, known.Result);
        Assert.Equal(OutcomeClass.EvaluationError, unknown.Class);
        Assert.Equal(Time(new Timestamp { Seconds = 1_721_048_400 }), time.Result);
    }

    // ---- Helpers ----

    private static Value Time(Timestamp timestamp) => new() { ObjectValue = Any.Pack(timestamp) };

    private static byte[] Manifest() => File.ReadAllBytes(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath));

    private static LibrarySide Build(string zoneInfoFile)
    {
        Assert.True(LibrarySide.TryBuild(Manifest(), zoneInfoFile, out LibrarySide? side, out string problem), problem);
        return side!;
    }

    // A zip archive of the zones in the temporary repository: the entry of a directory, and one entry per zone.
    private static string WriteZones(TempDirectory root, params (string Zone, byte[] Data)[] zones)
    {
        string file = Path.Combine(root.Path, "zones.zip");
        using ZipArchive archive = ZipFile.Open(file, ZipArchiveMode.Create);
        _ = archive.CreateEntry("Europe/");
        foreach ((string zone, byte[] data) in zones)
        {
            using Stream entry = archive.CreateEntry(zone).Open();
            entry.Write(data);
        }

        return file;
    }

    // A small TZif file (RFC 9636) written byte by byte: the version 1 block with one empty type, as zic writes it in slim files,
    // the version 2 block with the transitions (the time and the type index of each) and the offsets of the types, and the
    // footer, a line feed, the POSIX TZ rule, a line feed.
    private static byte[] Tzif(long[] times, int[] indexes, int[] offsets, string footer)
    {
        List<byte> data = [];
        Header(data, 0, 1, 1);
        data.AddRange(new byte[7]);
        Header(data, times.Length, offsets.Length, 1);
        foreach (long time in times)
        {
            AddInt64(data, time);
        }

        data.AddRange(indexes.Select(index => (byte)index));
        foreach (int offset in offsets)
        {
            AddInt32(data, offset);
            data.AddRange(new byte[] { 0, 0 });
        }

        data.Add(0);
        data.AddRange(Encoding.ASCII.GetBytes("\n" + footer + "\n"));
        return [.. data];
    }

    private static void Header(List<byte> data, int timeCount, int typeCount, int charCount)
    {
        data.AddRange(Encoding.ASCII.GetBytes("TZif2"));
        data.AddRange(new byte[15]);
        foreach (int count in new[] { 0, 0, 0, timeCount, typeCount, charCount })
        {
            AddInt32(data, count);
        }
    }

    private static void AddInt32(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        data.AddRange(bytes.ToArray());
    }

    private static void AddInt64(List<byte> data, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        data.AddRange(bytes.ToArray());
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// ENG822 of eng/difftest (docs/design/eng-difftest.md): the time zone offsets of both sides, with fake sides that give fixed
// offsets per zone.
public sealed class ZoneOffsetTests
{
    [Fact]
    public void AnotherZoneOffset_GivesEng822AtThePathTheTestGives()
    {
        using TempDirectory root = new();
        const string path = "data/zones/zoneinfo.zip";
        WriteZones(root, path, "Europe/Berlin", "Asia/Kolkata", "UTC");
        DateTimeOffset summer = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset winter = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        SignatureTests.FakeSide oracle = Zones(("Europe/Berlin", 1), ("Asia/Kolkata", 5.5), ("UTC", 0));
        SignatureTests.FakeSide library = Zones(("Europe/Berlin", 2), ("Asia/Kolkata", 5.5), ("UTC", 0));

        IReadOnlyList<Finding> findings = ZoneOffsets.Compare(root.Path, path, [winter, summer], library, oracle);

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng822,
                path,
                1,
                1,
                "time zone offset differs from Go: zone 'Europe/Berlin' at 2026-01-01T12:00:00Z: Go +01:00, we +02:00"));
    }

    [Fact]
    public void EqualZoneOffsets_GiveNoFinding()
    {
        using TempDirectory root = new();
        WriteZones(root, "zones.zip", "Europe/Berlin");
        SignatureTests.FakeSide side = Zones(("Europe/Berlin", 1));

        FindingAssert.None(ZoneOffsets.Compare(root.Path, "zones.zip", ZoneOffsets.Instants(SignatureTests.RunSeed, 5), side, side));
    }

    [Fact]
    public void Offsets_AreHoursAndMinutesWithSecondsWhenTheyHaveSeconds()
    {
        Assert.Equal("+00:00", ZoneOffsets.Offset(TimeSpan.Zero));
        Assert.Equal("+05:30", ZoneOffsets.Offset(new TimeSpan(5, 30, 0)));
        Assert.Equal("-03:30", ZoneOffsets.Offset(new TimeSpan(-3, -30, 0)));
        Assert.Equal("+00:53:28", ZoneOffsets.Offset(new TimeSpan(0, 53, 28)));
        Assert.Equal("-00:00:36", ZoneOffsets.Offset(TimeSpan.FromSeconds(-36)));
        Assert.Equal("1800-01-01T00:00:00Z", ZoneOffsets.Instant(new DateTimeOffset(1800, 1, 1, 3, 0, 0, TimeSpan.FromHours(3))));
    }

    [Fact]
    public void Instants_DependOnTheSeedAloneAndLieIn1800To2199()
    {
        IReadOnlyList<DateTimeOffset> instants = ZoneOffsets.Instants(SignatureTests.RunSeed, 50);

        Assert.Equal(instants, ZoneOffsets.Instants(SignatureTests.RunSeed, 50));
        Assert.NotEqual(instants, ZoneOffsets.Instants(SignatureTests.RunSeed + 1, 50));
        Assert.All(instants, instant => Assert.InRange(instant.Year, 1800, 2199));
        Assert.Equal(55, instants.Count);
    }

    [Fact]
    public void Instants_EndWithTheFiveFixedInstants()
    {
        IReadOnlyList<DateTimeOffset> instants = ZoneOffsets.Instants(SignatureTests.RunSeed, 256);

        Assert.Equal(261, instants.Count);
        Assert.Equal(
            ["1800-01-01T00:00:00Z", "1900-01-01T00:00:00Z", "1970-01-01T00:00:00Z", "2037-01-01T00:00:00Z", "2100-01-01T00:00:00Z"],
            instants.TakeLast(5).Select(ZoneOffsets.Instant));
        Assert.Equal(instants.Take(256), ZoneOffsets.Instants(SignatureTests.RunSeed, 256).Take(256));
    }

    [Fact]
    public void ZoneInfoPath_NamesTheZipOfTheRepository()
    {
        Assert.Equal("tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip", ProfileTables.ZoneInfoPath);
        Assert.True(File.Exists(Path.Combine(RepositoryRoot.Find(), ProfileTables.ZoneInfoPath)));
    }

    private static SignatureTests.FakeSide Zones(params (string Zone, double Hours)[] offsets) => new(
        (_, _) => SideOutcome.Of(SignatureTests.Int(0)),
        (zone, _) => TimeSpan.FromHours(offsets.Single(offset => offset.Zone == zone).Hours));

    private static void WriteZones(TempDirectory root, string path, params string[] zones)
    {
        string file = Path.Combine(root.Path, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using ZipArchive archive = ZipFile.Open(file, ZipArchiveMode.Create);
        _ = archive.CreateEntry("Europe/");
        foreach (string zone in zones)
        {
            _ = archive.CreateEntry(zone);
        }
    }
}

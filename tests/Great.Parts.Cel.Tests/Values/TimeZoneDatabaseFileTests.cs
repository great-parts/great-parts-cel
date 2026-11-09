using System.Collections.Generic;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The database made from the whole test archive (docs/design/tests.md, "Time zone data"): every file reads, the identifiers are
// in ordinal order, and the zones the other tests rely on are there.
public sealed class TimeZoneDatabaseFileTests
{
    [Fact]
    public static void Create_ReadsEveryZoneOfTheArchive()
    {
        IReadOnlyList<KeyValuePair<string, byte[]>> entries = ZoneInfoFile.Entries();

        CelTimeZoneDatabase database = ZoneInfoFile.Database();

        Assert.True(entries.Count > 500, $"the archive has {entries.Count} entries");
        Assert.Equal(entries.Count, database.ZoneIds.Length);
        Assert.Equal(ZoneInfoFile.Version, database.Version);
        foreach (KeyValuePair<string, byte[]> entry in entries)
        {
            Assert.True(database.Contains(entry.Key), entry.Key);
        }
    }

    [Fact]
    public static void EveryFileOfTheArchive_IsReadByTheReaderAlone()
    {
        foreach (KeyValuePair<string, byte[]> entry in ZoneInfoFile.Entries())
        {
            TimeZoneRules.TzifFailure failure = TimeZoneRules.TryReadTzif(entry.Value, out ZoneData? zone);

            Assert.True(failure == TimeZoneRules.TzifFailure.None, $"{entry.Key}: {failure}");
            Assert.NotNull(zone);
            Assert.NotEmpty(zone.TypeOffsets);
            Assert.Equal(zone.Transitions.Length, zone.TransitionTypes.Length);
        }
    }

    [Fact]
    public static void ZoneIds_AreInOrdinalOrderAndWithoutRepeats()
    {
        CelTimeZoneDatabase database = ZoneInfoFile.Database();

        for (int i = 1; i < database.ZoneIds.Length; i++)
        {
            Assert.True(string.CompareOrdinal(database.ZoneIds[i - 1], database.ZoneIds[i]) < 0, database.ZoneIds[i]);
        }
    }

    [Theory]
    [InlineData("Asia/Kathmandu")]
    [InlineData("America/New_York")]
    [InlineData("Europe/Moscow")]
    [InlineData("Australia/Lord_Howe")]
    [InlineData("Europe/London")]
    [InlineData("Pacific/Auckland")]
    [InlineData("Africa/Casablanca")]
    [InlineData("UTC")]
    public static void TheZonesOfTheOtherTests_AreInTheArchive(string zoneId)
    {
        Assert.True(ZoneInfoFile.Database().Contains(zoneId));
    }

    // Identifiers match by ordinal, case-sensitive comparison: a different case, a space or a prefix is another name.
    [Theory]
    [InlineData("asia/kathmandu")]
    [InlineData("ASIA/KATHMANDU")]
    [InlineData("Asia/Kathmandu ")]
    [InlineData(" Asia/Kathmandu")]
    [InlineData("Asia/Kathman")]
    [InlineData("Asia")]
    [InlineData("")]
    [InlineData("Local")]
    [InlineData("+03:00")]
    public static void NamesThatAreNotIdentifiers_AreNotFound(string name)
    {
        Assert.False(ZoneInfoFile.Database().Contains(name));
    }

    // A link of the zoneinfo directory is an identifier of its own with the data of its target.
    [Theory]
    [InlineData("US/Eastern", "America/New_York")]
    [InlineData("Asia/Katmandu", "Asia/Kathmandu")]
    [InlineData("Europe/Kiev", "Europe/Kyiv")]
    public static void Link_IsAnIdentifierWithTheOffsetsOfItsTarget(string link, string target)
    {
        CelTimeZoneDatabase database = ZoneInfoFile.Database();
        ZoneData linked = database.Find(link)!;
        ZoneData original = database.Find(target)!;

        Assert.True(database.Contains(link));
        Assert.NotNull(linked);
        Assert.NotNull(original);
        for (long instant = -3_000_000_000; instant < 4_200_000_000; instant += 7_000_003)
        {
            Assert.Equal(TimeZoneRules.OffsetAt(original, instant), TimeZoneRules.OffsetAt(linked, instant));
        }
    }

    // The same bytes under another identifier and the same identifier with the same bytes are both allowed.
    [Fact]
    public static void AnEntryGivenTwiceWithTheSameBytes_IsOneZone()
    {
        KeyValuePair<string, byte[]> first = ZoneInfoFile.Entries()[0];

        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create(
        [
            TzifFile.Pair(first.Key, first.Value),
            TzifFile.Pair(first.Key, (byte[])first.Value.Clone()),
            TzifFile.Pair("Test/Copy", first.Value),
        ]);

        Assert.Equal(2, database.ZoneIds.Length);
        Assert.True(database.Contains("Test/Copy"));
    }
}

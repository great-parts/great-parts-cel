using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// CelTimeZoneDatabase: an immutable database of IANA time zones built from TZif data that the application
// passes as pairs of an identifier and bytes. Create reads and checks every entry at once, so an invalid
// file is reported by Create and not at evaluation. The identifiers are kept in ordinal order; a link is an
// identifier of its own with the bytes of its target; the identifier is matched by ordinal comparison, so
// the case counts. Empty is a database without zones; Version is the text the application gave, for
// messages only. The bytes are copied. The errors of Create are ArgumentException whose message names the
// identifier (the empty identifier has no name to show): TZif that is not valid or not of the version 2
// or later, TZif with leap second records (CEL time has none, as the files of the right directory have),
// an identifier that is empty or has ":" (it would read as a fixed offset), the same identifier twice
// with different bytes. The bytes of the examples are those of TimeZoneData.
[Trait("Requirement", "+R-4")]
[Trait("Requirement", "R-5")]
public sealed class TimeZoneDatabaseTests
{
    private static readonly string[] SortedIds = ["Etc/Z", "Test/B", "Test/_x", "Test/a"];
    private static readonly string[] OnlyFixed = ["Test/Fixed"];

    [Fact]
    public void Empty_IsADatabaseWithoutZones()
    {
        CelTimeZoneDatabase empty = CelTimeZoneDatabase.Empty;

        Assert.Empty(empty.ZoneIds);
        Assert.Null(empty.Version);
        Assert.False(empty.Contains("UTC"));
        Assert.False(empty.Contains("Test/Fixed"));
        Assert.Same(empty, CelTimeZoneDatabase.Empty);
    }

    [Fact]
    public void Create_KeepsTheVersionAndTheIdentifiersInOrdinalOrder()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create(
            [
                TimeZoneData.Pair("Test/a", TimeZoneData.Fixed),
                TimeZoneData.Pair("Test/_x", TimeZoneData.Rule),
                TimeZoneData.Pair("Test/B", TimeZoneData.Moved),
                TimeZoneData.Pair("Etc/Z", TimeZoneData.Fixed),
            ],
            "2026a");

        Assert.Equal("2026a", database.Version);
        Assert.Equal<IEnumerable<string>>(SortedIds, database.ZoneIds);
    }

    [Fact]
    public void Create_WithoutAVersion_HasNone()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([TimeZoneData.Pair("Test/Fixed", TimeZoneData.Fixed)]);

        Assert.Null(database.Version);
        Assert.Equal<IEnumerable<string>>(OnlyFixed, database.ZoneIds);
    }

    [Fact]
    public void Create_OfNoZones_IsADatabaseWithoutZones()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([], "2026a");

        Assert.Empty(database.ZoneIds);
        Assert.Equal("2026a", database.Version);
    }

    [Fact]
    public void ALink_IsAnIdentifierOfItsOwn()
    {
        CelTimeZoneDatabase database = TimeZoneData.Database();

        Assert.Contains("Test/Fixed", database.ZoneIds);
        Assert.Contains("Test/Link", database.ZoneIds);
        Assert.Equal(8, database.ZoneIds.Length);
    }

    [Fact]
    public void Contains_MatchesByOrdinalCaseSensitiveComparison()
    {
        CelTimeZoneDatabase database = TimeZoneData.Database();

        Assert.True(database.Contains("Test/Rule"));
        Assert.True(database.Contains("Test/Link"));
        Assert.False(database.Contains("test/rule"));
        Assert.False(database.Contains("TEST/RULE"));
        Assert.False(database.Contains("Test/Rule "));
        Assert.False(database.Contains("Test/Missing"));
        Assert.False(database.Contains(string.Empty));
        Assert.False(database.Contains("UTC"));
        Assert.False(database.Contains("+03:00"));
    }

    [Fact]
    public void ANullArgument_IsArgumentNullException()
    {
        CelTimeZoneDatabase database = TimeZoneData.Database();

        Assert.Throws<ArgumentNullException>(() => database.Contains(null!));
        Assert.Throws<ArgumentNullException>(() => CelTimeZoneDatabase.Create(null!));
        Assert.Throws<ArgumentNullException>(() => CelTimeZoneDatabase.Create([new KeyValuePair<string, ReadOnlyMemory<byte>>(null!, TimeZoneData.Fixed)]));
    }

    [Fact]
    public void TheSameIdentifierTwice_IsOneZoneWithTheSameBytesAndAnErrorWithOthers()
    {
        CelTimeZoneDatabase same = CelTimeZoneDatabase.Create(
        [
            TimeZoneData.Pair("Test/Fixed", TimeZoneData.Fixed),
            TimeZoneData.Pair("Test/Fixed", (byte[])TimeZoneData.Fixed.Clone()),
        ]);
        ArgumentException different = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
        [
            TimeZoneData.Pair("Test/Fixed", TimeZoneData.Fixed),
            TimeZoneData.Pair("Test/Fixed", TimeZoneData.Rule),
        ]));

        Assert.Equal<IEnumerable<string>>(OnlyFixed, same.ZoneIds);
        Assert.Contains("Test/Fixed", different.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidFiles))]
    public void FileThatIsNotValidTzif_IsArgumentExceptionNamingTheIdentifier(string description, byte[] data)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
        [
            TimeZoneData.Pair("Test/Fixed", TimeZoneData.Fixed),
            TimeZoneData.Pair("Test/Bad", data),
        ]));

        Assert.NotEmpty(description);
        Assert.Contains("Test/Bad", error.Message, StringComparison.Ordinal);
    }

    // The files that Create refuses: no data, a wrong magic, a cut header, a cut block, data of the version 1
    // alone, a footer with a rule that is not POSIX TZ (month 13), a footer that is not a rule, and a file
    // with a leap second record, as the files of the right directory have.
    public static TheoryData<string, byte[]> InvalidFiles()
    {
        byte[] wrongMagic = (byte[])TimeZoneData.Rule.Clone();
        wrongMagic[0] = (byte)'X';
        byte[] cutBlock = TimeZoneData.Rule[..(TimeZoneData.Rule.Length - 40)];
        return new TheoryData<string, byte[]>
        {
            { "no bytes", [] },
            { "wrong magic", wrongMagic },
            { "cut header", TimeZoneData.Rule[..30] },
            { "cut block", cutBlock },
            { "version 1 only", TimeZoneData.Tzif([], [(3600, false, "+01")], "<+01>-1", version: 0) },
            { "month 13 in the rule", TimeZoneData.Tzif([(0, 1)], [(0, false, "LMT"), (3600, false, "+01")], "<+01>-1<+02>,M13.1.0,M10.5.0") },
            { "footer that is not a rule", TimeZoneData.Tzif([(0, 1)], [(0, false, "LMT"), (3600, false, "+01")], "garbage") },
            { "leap second record", TimeZoneData.Tzif([(0, 1)], [(0, false, "LMT"), (3600, false, "+01")], "<+01>-1", leapSeconds: 1) },
        };
    }

    [Theory]
    [InlineData("Test:Zone")]
    [InlineData("+03:00")]
    [InlineData(":")]
    [InlineData("Test/Zone:")]
    public static void IdentifierWithAColon_IsArgumentExceptionNamingTheIdentifier(string identifier)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create([TimeZoneData.Pair(identifier, TimeZoneData.Fixed)]));

        Assert.Contains(identifier, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyIdentifier_IsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create([TimeZoneData.Pair(string.Empty, TimeZoneData.Fixed)]));
    }

    // The bytes are copied: a change of the array after Create does not change the zone.
    [Fact]
    public void Create_CopiesTheBytes()
    {
        byte[] data = (byte[])TimeZoneData.Fixed.Clone();
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([TimeZoneData.Pair("Test/Fixed", data)]);
        Array.Clear(data);
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(database).Build().GetEnvironmentOrThrow();

        long hours = environment.Compile<long>("timestamp('2009-02-13T23:31:30Z').getHours('Test/Fixed')").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();

        Assert.Equal(5L, hours);
    }
}

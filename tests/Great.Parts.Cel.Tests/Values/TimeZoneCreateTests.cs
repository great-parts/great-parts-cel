using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// CelTimeZoneDatabase.Create: what it keeps, and one test per row of the table of failures — each throws ArgumentException with
// the parameter name "zones" and a message that names the identifier and the cause.
public sealed class TimeZoneCreateTests
{
    private static readonly byte[] Valid = TzifFile.Build([0], [1], [0, 3_600], "<+01>-1");

    [Fact]
    public static void Empty_IsOneInstanceWithoutZones()
    {
        Assert.Same(CelTimeZoneDatabase.Empty, CelTimeZoneDatabase.Empty);
        Assert.Empty(CelTimeZoneDatabase.Empty.ZoneIds);
        Assert.Null(CelTimeZoneDatabase.Empty.Version);
        Assert.False(CelTimeZoneDatabase.Empty.Contains("UTC"));
        Assert.False(CelTimeZoneDatabase.Empty.Contains("Europe/Moscow"));
    }

    [Fact]
    public static void NoPairs_IsAnotherDatabaseThanEmpty()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([], "2026c");

        Assert.NotSame(CelTimeZoneDatabase.Empty, database);
        Assert.Empty(database.ZoneIds);
        Assert.Equal("2026c", database.Version);
    }

    [Fact]
    public static void TwoCallsWithTheSameData_GiveTwoObjects()
    {
        CelTimeZoneDatabase first = CelTimeZoneDatabase.Create([TzifFile.Pair("Test/A", Valid)]);
        CelTimeZoneDatabase second = CelTimeZoneDatabase.Create([TzifFile.Pair("Test/A", Valid)]);

        Assert.NotSame(first, second);
        Assert.Null(first.Version);
    }

    [Fact]
    public static void ZoneIds_AreInOrdinalOrder_WhateverTheOrderOfTheInput()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create(
        [
            TzifFile.Pair("b/zone", Valid),
            TzifFile.Pair("Zone", Valid),
            TzifFile.Pair("a/zone", Valid),
            TzifFile.Pair("B/zone", Valid),
            TzifFile.Pair("_x", Valid),
        ]);

        Assert.Equal<IEnumerable<string>>(["B/zone", "Zone", "_x", "a/zone", "b/zone"], database.ZoneIds);
    }

    [Fact]
    public static void Contains_MatchesByOrdinalCaseSensitiveComparison()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([TzifFile.Pair("Test/Zone", Valid)]);

        Assert.True(database.Contains("Test/Zone"));
        Assert.False(database.Contains("test/zone"));
        Assert.False(database.Contains("Test/Zone "));
        Assert.False(database.Contains("Test/Zon"));
        Assert.False(database.Contains(string.Empty));
        Assert.Throws<ArgumentNullException>(() => database.Contains(null!));
    }

    // The bytes are copied: a change of the array after Create does not change the zone.
    [Fact]
    public static void Create_CopiesWhatItKeeps()
    {
        byte[] data = TzifFile.Build([0], [1], [0, 3_600], "<+01>-1");
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([TzifFile.Pair("Test/Zone", data)]);

        Array.Clear(data);

        Assert.Equal(3_600, TimeZoneRules.Resolve(database, "Test/Zone").OffsetAt(1_000));
    }

    [Fact]
    public static void NullCollection_IsArgumentNullException()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => CelTimeZoneDatabase.Create(null!));

        Assert.Equal("zones", error.ParamName);
    }

    [Fact]
    public static void NullIdentifier_IsArgumentNullException()
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(
            () => CelTimeZoneDatabase.Create([new KeyValuePair<string, ReadOnlyMemory<byte>>(null!, Valid)]));

        Assert.Equal("zones", error.ParamName);
    }

    // The failures. Each case gives an identifier, the bytes, and a part of the message that names the cause.
    public static TheoryData<string, byte[], string> Failures()
    {
        byte[] wrongMagic = (byte[])Valid.Clone();
        wrongMagic[0] = (byte)'X';
        byte[] wrongMagicInSecondBlock = (byte[])Valid.Clone();
        wrongMagicInSecondBlock[44 + 7] = (byte)'X';
        byte[] unsupportedVersion = (byte[])Valid.Clone();
        unsupportedVersion[4] = (byte)'5';
        byte[] version1Digit = (byte[])Valid.Clone();
        version1Digit[4] = (byte)'1';
        byte[] cutInFirstBlock = Valid[..60];
        byte[] cutInSecondBody = Valid[..(Valid.Length - 12)];
        byte[] counterBeyondTheData = (byte[])Valid.Clone();
        counterBeyondTheData[44 + 7 + 32 + 3] = 100; // the number of transitions of the second header
        byte[] hugeCounter = (byte[])Valid.Clone();
        hugeCounter[44 + 7 + 20] = 0xFF; // the number of UT indicators of the second header: 4 billion
        string footerNotEndingWithLineFeed = "<+01>-1";
        byte[] noFinalLineFeed = Valid[..^1];
        byte[] noFooter = Valid[..(Valid.Length - footerNotEndingWithLineFeed.Length - 2)];
        byte[] garbageAfterTheFooter = [.. Valid, .. "x"u8];
        byte[] footerWithAnotherLine = TzifFile.Build([0], [1], [0, 3_600], "<+01>-1\nM3.2.0");
        byte[] notARule = TzifFile.Build([0], [1], [0, 3_600], "garbage");
        byte[] monthThirteen = TzifFile.Build([0], [1], [0, 3_600], "<+01>-1<+02>,M13.1.0,M10.5.0");
        byte[] notIncreasing = TzifFile.Build([100, 100], [1, 1], [0, 3_600], string.Empty);
        byte[] decreasing = TzifFile.Build([200, 100], [1, 1], [0, 3_600], string.Empty);
        byte[] unknownType = TzifFile.Build([100], [2], [0, 3_600], string.Empty);
        byte[] noType = TzifFile.Build([], [], [], string.Empty);
        return new TheoryData<string, byte[], string>
        {
            { "Test/Empty", [], "bad magic" },
            { "Test/Magic", wrongMagic, "bad magic" },
            { "Test/Magic2", wrongMagicInSecondBlock, "bad magic" },
            { "Test/Unsupported", unsupportedVersion, "unsupported version" },
            { "Test/VersionDigit1", version1Digit, "unsupported version" },
            { "Test/Version1", TzifFile.Build([], [], [0], string.Empty, version: 0), "version 1 has no 64-bit data" },
            { "Test/CutHeader", Valid[..30], "the data is cut" },
            { "Test/CutBlock", cutInFirstBlock, "the data is cut" },
            { "Test/CutBody", cutInSecondBody, "the data is cut" },
            { "Test/CounterBeyondTheData", counterBeyondTheData, "the data is cut" },
            { "Test/HugeCounter", hugeCounter, "the data is cut" },
            { "Test/NotIncreasing", notIncreasing, "transitions are not in increasing order" },
            { "Test/Decreasing", decreasing, "transitions are not in increasing order" },
            { "Test/UnknownType", unknownType, "a transition has an unknown local time type" },
            { "Test/NoType", noType, "no local time type" },
            { "Test/NoFinalLineFeed", noFinalLineFeed, "the footer is missing" },
            { "Test/NoFooter", noFooter, "the footer is missing" },
            { "Test/Garbage", garbageAfterTheFooter, "the footer is missing" },
            { "Test/TwoLines", footerWithAnotherLine, "the footer is missing" },
            { "Test/NotARule", notARule, "the footer is not a POSIX TZ rule" },
            { "Test/Month13", monthThirteen, "the footer is not a POSIX TZ rule" },
            { "Test/Leap", TzifFile.Build([0], [1], [0, 3_600], "<+01>-1", leapSeconds: 1), "leap second records" },
            { "Test/Leap1", TzifFile.Build([0], [1], [0, 3_600], "<+01>-1", firstBlockLeapSeconds: 1), "leap second records" },
        };
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public static void DataThatIsNotValid_IsArgumentExceptionNamingTheIdentifierAndTheCause(string id, byte[] data, string cause)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
        [
            TzifFile.Pair("Test/Good", Valid),
            TzifFile.Pair(id, data),
        ]));

        Assert.Equal("zones", error.ParamName);
        Assert.Contains($"'{id}'", error.Message, StringComparison.Ordinal);
        Assert.Contains(cause, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void NotValidTzif_HasTheMessageOfTheTable()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => CelTimeZoneDatabase.Create([TzifFile.Pair("Test/Bad", [])]));

        Assert.StartsWith("time zone 'Test/Bad': the data is not valid TZif: bad magic", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void LeapSecondRecords_HaveTheMessageOfTheTable()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
            [TzifFile.Pair("Test/Right", TzifFile.Build([0], [1], [0, 3_600], "<+01>-1", leapSeconds: 2))]));

        Assert.StartsWith(
            "time zone 'Test/Right': the data has leap second records, which CEL time does not have (files of the right/ directory are not supported)",
            error.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Test:Zone")]
    [InlineData("+03:00")]
    [InlineData(":")]
    [InlineData("Test/Zone:")]
    [InlineData(":Test")]
    public static void IdentifierWithAColon_IsArgumentException(string id)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create([TzifFile.Pair(id, Valid)]));

        Assert.Equal("zones", error.ParamName);
        Assert.StartsWith($"time zone identifier '{id}' has ':', so the name would read as a fixed offset", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void EmptyIdentifier_IsArgumentException()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create([TzifFile.Pair(string.Empty, Valid)]));

        Assert.Equal("zones", error.ParamName);
        Assert.StartsWith("a time zone identifier is empty", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void TheSameIdentifierWithDifferentBytes_IsArgumentException()
    {
        byte[] other = TzifFile.Build([0], [1], [0, 7_200], "<+02>-2");

        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
            [TzifFile.Pair("Test/Zone", Valid), TzifFile.Pair("Test/Zone", other)]));

        Assert.Equal("zones", error.ParamName);
        Assert.StartsWith("time zone 'Test/Zone' is given twice with different data", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void TheSameIdentifierWithEqualBytes_IsOneZone()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create(
            [TzifFile.Pair("Test/Zone", Valid), TzifFile.Pair("Test/Zone", (byte[])Valid.Clone())]);

        Assert.Equal<IEnumerable<string>>(["Test/Zone"], database.ZoneIds);
    }

    // The checks run per pair in order, and the first failure is the one reported.
    [Fact]
    public static void FirstFailureInTheOrderOfThePairs_IsTheOneReported()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
        [
            TzifFile.Pair("Test/First", Valid),
            TzifFile.Pair("Test/Second", []),
            TzifFile.Pair("Third:", Valid),
        ]));

        Assert.Contains("'Test/Second'", error.Message, StringComparison.Ordinal);
    }

    // For one pair the identifier is checked before the data, and a repeat with other bytes that is not TZif is a TZif failure.
    [Fact]
    public static void IdentifierIsCheckedBeforeTheData_AndTheDataBeforeTheRepeat()
    {
        ArgumentException colon = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create([TzifFile.Pair("A:B", [])]));
        ArgumentException repeat = Assert.Throws<ArgumentException>(() => CelTimeZoneDatabase.Create(
            [TzifFile.Pair("Test/Zone", Valid), TzifFile.Pair("Test/Zone", [1, 2, 3])]));

        Assert.Contains("has ':'", colon.Message, StringComparison.Ordinal);
        Assert.Contains("is not valid TZif", repeat.Message, StringComparison.Ordinal);
    }

    // A file of version 3 or 4 is read as one of version 2: the version byte only has to be 2, 3 or 4.
    [Theory]
    [InlineData('2')]
    [InlineData('3')]
    [InlineData('4')]
    public static void VersionsTwoToFour_AreRead(char version)
    {
        byte[] data = TzifFile.Build([0], [1], [0, 3_600], "<+01>-1", (byte)version);

        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([TzifFile.Pair("Test/Zone", data)]);

        Assert.True(database.Contains("Test/Zone"));
    }
}

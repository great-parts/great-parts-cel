using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Values;

// Small TZif files (RFC 9636) written byte by byte, for the examples of the time zone database. A file has
// the version 1 block, which the library skips and which holds one empty type here as zic writes it in
// slim files, then the version 2 block with 64-bit transitions, then the footer: a newline, the POSIX TZ
// rule of the times after the last transition, a newline. The zones of this file:
//
// Test/Fixed      +05:45 all the time ("+0545"), no transition, footer "<+0545>-5:45".
// Test/Link       the bytes of Test/Fixed under another identifier, as the zoneinfo files keep a link.
// Test/Rule       LMT -04:56:02 (a second of precision) before 1970-01-01T00:00:00Z, then the rule of the
//                 east of the United States: EST -05:00, EDT -04:00 from the second Sunday of March 02:00
//                 to the first Sunday of November 02:00 (form Mm.w.d).
// Test/Moved      LMT +02:30 before 2000-01-01T00:00:00Z, +03:00 until 2010-01-01T00:00:00Z, then +04:00
//                 by two transitions and by the footer "<+04>-4".
// Test/Julian     +01:00, DST +02:00 from 1 March 02:00 to 27 October 02:00 (DST) by the form Jn:
//                 J60 is 1 March in every year, as J59 is 28 February and 29 February is never counted.
// Test/ZeroBased  the same by the form n, which counts 29 February: 59 is 29 February in a leap year and
//                 1 March otherwise; 300 is 27 October in a leap year and 28 October otherwise.
// Test/Nuuk       -02:00, DST -01:00 with a negative rule time: from the last Sunday of March at -1:00
//                 (23:00 of the Saturday) to the last Sunday of October at 00:00 DST.
// Test/Over24     +01:00, DST +02:00 with rule times over 24 hours: the last Sunday of March and of
//                 October at 25:00, that is 01:00 of the next day (RFC 9636 allows -167 to 167 hours).
//
// Every zone but Test/Fixed and Test/Link has the type 0 (LMT) for the times before the first transition:
// with the offset of the comment where it names one, else with the offset 0 and from 1970 on the zone
// of the comment. The transition at second 0 makes the type 1 the zone from 1970 on.
internal static class TimeZoneData
{
    private const string FixedFooter = "<+0545>-5:45";

    public static byte[] Fixed { get; } = Tzif([], [(20700, false, "+0545")], FixedFooter);

    public static byte[] Rule { get; } = Tzif(
        [(0, 1)],
        [(-17762, false, "LMT"), (-18000, false, "EST")],
        "EST5EDT,M3.2.0,M11.1.0");

    public static byte[] Moved { get; } = Tzif(
        [(946684800, 1), (1262304000, 2)],
        [(9000, false, "LMT"), (10800, false, "+03"), (14400, false, "+04")],
        "<+04>-4");

    public static byte[] Julian { get; } = FromRule("<+01>-1<+02>,J60,J300");

    public static byte[] ZeroBased { get; } = FromRule("<+01>-1<+02>,59,300");

    // The rule times -1 and 25 hours are the extension of version 3, as zic writes such files.
    public static byte[] Nuuk { get; } = Tzif(
        [(0, 1)],
        [(-10000, false, "LMT"), (-7200, false, "-02")],
        "<-02>2<-01>,M3.5.0/-1,M10.5.0/0",
        version: (byte)'3');

    public static byte[] Over24 { get; } = FromRule("<+01>-1<+02>,M3.5.0/25,M10.5.0/25", (byte)'3');

    // A database of all the zones above and the link, with the version 2026a.
    public static CelTimeZoneDatabase Database() => CelTimeZoneDatabase.Create(Zones(), "2026a");

    public static IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>> Zones() =>
    [
        Pair("Test/Fixed", Fixed),
        Pair("Test/Link", Fixed),
        Pair("Test/Rule", Rule),
        Pair("Test/Moved", Moved),
        Pair("Test/Julian", Julian),
        Pair("Test/ZeroBased", ZeroBased),
        Pair("Test/Nuuk", Nuuk),
        Pair("Test/Over24", Over24),
    ];

    public static KeyValuePair<string, ReadOnlyMemory<byte>> Pair(string id, byte[] data) => new(id, data);

    // A zone with the offset +01:00 from 1970 on and the given rule after the transition at second 0.
    private static byte[] FromRule(string footer, byte version = (byte)'2') =>
        Tzif([(0, 1)], [(0, false, "LMT"), (3600, false, "+01")], footer, version: version);

    // The bytes of a TZif file of the version 2 with the given transitions (second, index of the type),
    // types (offset in seconds, DST flag, abbreviation), footer and number of leap second records. With
    // leapSeconds above 0 the file is one that CEL time refuses, as the files of the right/ directory.
    public static byte[] Tzif(
        (long At, int Type)[] transitions,
        (int Offset, bool IsDst, string Abbreviation)[] types,
        string footer,
        int leapSeconds = 0,
        byte version = (byte)'2')
    {
        var data = new List<byte>();
        var abbreviations = new StringBuilder();
        var abbreviationIndexes = new int[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            abbreviationIndexes[i] = abbreviations.Length;
            abbreviations.Append(types[i].Abbreviation).Append('\0');
        }

        // Version 1 block: no transitions, one type, one character.
        AddHeader(data, version, 0, 0, 0, 0, 1, 1);
        data.AddRange(new byte[] { 0, 0, 0, 0, 0, 0 });
        data.Add(0);
        if (version == 0)
        {
            return [.. data];
        }

        // Version 2 block.
        AddHeader(data, version, 0, 0, leapSeconds, transitions.Length, types.Length, abbreviations.Length);
        foreach ((long at, _) in transitions)
        {
            AddInt64(data, at);
        }

        foreach ((_, int type) in transitions)
        {
            data.Add((byte)type);
        }

        for (int i = 0; i < types.Length; i++)
        {
            AddInt32(data, types[i].Offset);
            data.Add(types[i].IsDst ? (byte)1 : (byte)0);
            data.Add((byte)abbreviationIndexes[i]);
        }

        data.AddRange(Encoding.ASCII.GetBytes(abbreviations.ToString()));
        for (int i = 0; i < leapSeconds; i++)
        {
            AddInt64(data, 78796800 + i * 31536000L);
            AddInt32(data, i + 1);
        }

        data.AddRange(Encoding.ASCII.GetBytes("\n" + footer + "\n"));
        return [.. data];
    }

    private static void AddHeader(List<byte> data, byte version, int isUtCount, int isStdCount, int leapCount, int timeCount, int typeCount, int charCount)
    {
        data.AddRange(Encoding.ASCII.GetBytes("TZif"));
        data.Add(version);
        data.AddRange(new byte[15]);
        AddInt32(data, isUtCount);
        AddInt32(data, isStdCount);
        AddInt32(data, leapCount);
        AddInt32(data, timeCount);
        AddInt32(data, typeCount);
        AddInt32(data, charCount);
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

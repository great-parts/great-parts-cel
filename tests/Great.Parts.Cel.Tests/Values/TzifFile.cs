using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Values;

// Small TZif files (RFC 9636) written byte by byte, for the tests of the time zone database. A file has the version 1 block,
// which the library skips and which holds one empty type here as zic writes it in slim files, then the version 2 block with
// 64-bit transitions, then the footer: a line feed, the POSIX TZ rule, a line feed.
internal static class TzifFile
{
    // A zone with the offset of the first type until second 0 and the second type, with the given rule, from second 0 on:
    // the rule governs every instant from 0 on, because the transition at 0 is the last one.
    internal static byte[] WithRule(string footer, int standardOffset, byte version = (byte)'2') =>
        Build([0], [1], [0, standardOffset], footer, version);

    // A zone without transitions and with the given offset and footer.
    internal static byte[] Fixed(int offset, string footer) => Build([], [], [offset], footer);

    // Builds a file: the transition times, the type index of each transition, the offset of each type, the footer text, the
    // version byte, the number of leap second records of the version 2 block and of the version 1 block.
    internal static byte[] Build(
        long[] times,
        int[] indexes,
        int[] offsets,
        string footer,
        byte version = (byte)'2',
        int leapSeconds = 0,
        int firstBlockLeapSeconds = 0)
    {
        List<byte> data = [];
        AddHeader(data, version, firstBlockLeapSeconds, 0, 1, 1);
        data.AddRange(new byte[7]);
        if (version == 0)
        {
            return [.. data];
        }

        AddHeader(data, version, leapSeconds, times.Length, offsets.Length, 1);
        foreach (long time in times)
        {
            AddInt64(data, time);
        }

        foreach (int index in indexes)
        {
            data.Add((byte)index);
        }

        foreach (int offset in offsets)
        {
            AddInt32(data, offset);
            data.Add(0);
            data.Add(0);
        }

        data.Add(0);
        for (int i = 0; i < leapSeconds; i++)
        {
            AddInt64(data, 78_796_800 + (i * 31_536_000L));
            AddInt32(data, i + 1);
        }

        data.AddRange(Encoding.ASCII.GetBytes("\n" + footer + "\n"));
        return [.. data];
    }

    internal static KeyValuePair<string, ReadOnlyMemory<byte>> Pair(string id, byte[] data) => new(id, data);

    // The zone data of a file, read through the library; the test fails when the file is not valid.
    internal static ZoneData Read(byte[] data)
    {
        TimeZoneRules.TzifFailure failure = TimeZoneRules.TryReadTzif(data, out ZoneData? zone);
        if (failure != TimeZoneRules.TzifFailure.None)
        {
            throw new InvalidOperationException($"the test file is not read: {failure}");
        }

        return zone!;
    }

    private static void AddHeader(List<byte> data, byte version, int leapCount, int timeCount, int typeCount, int charCount)
    {
        data.AddRange(Encoding.ASCII.GetBytes("TZif"));
        data.Add(version);
        data.AddRange(new byte[15]);
        AddInt32(data, 0);
        AddInt32(data, 0);
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

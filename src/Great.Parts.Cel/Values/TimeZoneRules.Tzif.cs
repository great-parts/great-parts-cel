using System;
using System.Buffers.Binary;

namespace Great.Parts.Cel.Values;

// Reading and checking TZif data (RFC 9636) of version 2 or later.
internal static partial class TimeZoneRules
{
    // The length of the header of a block: magic, version, 15 reserved bytes and six 4-byte counts.
    private const int HeaderLength = 44;

    /// <summary>
    /// Why TZif data was refused.
    /// </summary>
    internal enum TzifFailure
    {
        /// <summary>The data was read.</summary>
        None,

        /// <summary>The data does not begin with <c>TZif</c>.</summary>
        BadMagic,

        /// <summary>The version byte is not <c>2</c>, <c>3</c> or <c>4</c>.</summary>
        UnsupportedVersion,

        /// <summary>The version byte is 0: version 1 data has 4-byte times only.</summary>
        Version1,

        /// <summary>The data ends early, or a count does not fit the data.</summary>
        Cut,

        /// <summary>The transition times do not increase strictly.</summary>
        TransitionsOrder,

        /// <summary>A transition has a type index that no type has.</summary>
        UnknownType,

        /// <summary>The data has no local time type.</summary>
        NoType,

        /// <summary>The data does not end with a line feed, a rule and a line feed.</summary>
        FooterMissing,

        /// <summary>The footer is not a POSIX TZ rule.</summary>
        FooterNotRule,

        /// <summary>A block header counts leap second records.</summary>
        LeapSeconds,
    }

    /// <summary>
    /// Reads and checks TZif data of version 2 or later.
    /// </summary>
    /// <param name="data">The bytes of the file.</param>
    /// <param name="zone">The zone, when the data was read.</param>
    /// <returns><see cref="TzifFailure.None"/> or the first reason the data is refused.</returns>
    internal static TzifFailure TryReadTzif(ReadOnlySpan<byte> data, out ZoneData? zone)
    {
        zone = null;

        // The version 1 block is not used: its length is computed and skipped.
        TzifFailure failure = ReadHeader(data, 0, out Counts first);
        if (failure != TzifFailure.None)
        {
            return failure;
        }

        long position = HeaderLength + (first.TransitionCount * 5) + (first.TypeCount * 6) + first.CharCount + first.StandardCount + first.UniversalCount;
        if (position > data.Length)
        {
            return TzifFailure.Cut;
        }

        failure = ReadHeader(data, (int)position, out Counts counts);
        if (failure != TzifFailure.None)
        {
            return failure;
        }

        position += HeaderLength;
        if (counts.TypeCount == 0)
        {
            return TzifFailure.NoType;
        }

        long bodyLength = (counts.TransitionCount * 9) + (counts.TypeCount * 6) + counts.CharCount + counts.StandardCount + counts.UniversalCount;
        if (position + bodyLength > data.Length)
        {
            return TzifFailure.Cut;
        }

        // Every count is below the length of the data now, so the casts and the arrays are bounded by it.
        int transitionCount = (int)counts.TransitionCount;
        int typeCount = (int)counts.TypeCount;
        ReadOnlySpan<byte> body = data.Slice((int)position, (int)bodyLength);
        long[] transitions = new long[transitionCount];
        byte[] transitionTypes = new byte[transitionCount];
        failure = ReadTransitions(body, transitions, transitionTypes, typeCount);
        if (failure != TzifFailure.None)
        {
            return failure;
        }

        // Only the offset of a type is read; the daylight flag and the abbreviation do not change the local time.
        int[] typeOffsets = new int[typeCount];
        int typesAt = transitionCount * 9;
        for (int i = 0; i < typeCount; i++)
        {
            typeOffsets[i] = BinaryPrimitives.ReadInt32BigEndian(body[(typesAt + (i * 6))..]);
        }

        failure = ReadFooter(data[(int)(position + bodyLength)..], out TimeZonePosixRule? rule);
        if (failure != TzifFailure.None)
        {
            return failure;
        }

        zone = new ZoneData(transitions, transitionTypes, typeOffsets, rule);
        return TzifFailure.None;
    }

    // Reads the transition times and their type indexes from the start of the body; the times must increase strictly.
    private static TzifFailure ReadTransitions(ReadOnlySpan<byte> body, long[] transitions, byte[] transitionTypes, int typeCount)
    {
        int transitionCount = transitions.Length;
        for (int i = 0; i < transitionCount; i++)
        {
            transitions[i] = BinaryPrimitives.ReadInt64BigEndian(body[(i * 8)..]);
            if (i > 0 && transitions[i] <= transitions[i - 1])
            {
                return TzifFailure.TransitionsOrder;
            }

            byte type = body[(transitionCount * 8) + i];
            if (type >= typeCount)
            {
                return TzifFailure.UnknownType;
            }

            transitionTypes[i] = type;
        }

        return TzifFailure.None;
    }

    // Checks that the footer is a line feed, a rule and a line feed at the very end, and reads the rule.
    private static TzifFailure ReadFooter(ReadOnlySpan<byte> footer, out TimeZonePosixRule? rule)
    {
        rule = null;
        if (footer.Length < 2 || footer[0] != (byte)'\n' || footer[^1] != (byte)'\n')
        {
            return TzifFailure.FooterMissing;
        }

        ReadOnlySpan<byte> ruleText = footer[1..^1];
        foreach (byte b in ruleText)
        {
            if (b == (byte)'\n')
            {
                return TzifFailure.FooterMissing;
            }
        }

        if (!TryParseRule(ruleText, out rule))
        {
            return TzifFailure.FooterNotRule;
        }

        return TzifFailure.None;
    }

    // Checks the magic, the version and the length of a header, refuses leap second records, and reads the counts.
    private static TzifFailure ReadHeader(ReadOnlySpan<byte> data, int at, out Counts counts)
    {
        counts = default;
        ReadOnlySpan<byte> header = data[at..];
        if (header.Length < 4 || header[0] != (byte)'T' || header[1] != (byte)'Z' || header[2] != (byte)'i' || header[3] != (byte)'f')
        {
            return TzifFailure.BadMagic;
        }

        if (header.Length < 5)
        {
            return TzifFailure.Cut;
        }

        if (header[4] == 0)
        {
            return TzifFailure.Version1;
        }

        if (header[4] < (byte)'2' || header[4] > (byte)'4')
        {
            return TzifFailure.UnsupportedVersion;
        }

        if (header.Length < HeaderLength)
        {
            return TzifFailure.Cut;
        }

        // The counts follow the 15 reserved bytes: UT indicators, standard indicators, leap second records,
        // transitions, local time types, abbreviation characters.
        counts = new Counts(
            BinaryPrimitives.ReadUInt32BigEndian(header[20..]),
            BinaryPrimitives.ReadUInt32BigEndian(header[24..]),
            BinaryPrimitives.ReadUInt32BigEndian(header[32..]),
            BinaryPrimitives.ReadUInt32BigEndian(header[36..]),
            BinaryPrimitives.ReadUInt32BigEndian(header[40..]));
        return BinaryPrimitives.ReadUInt32BigEndian(header[28..]) == 0 ? TzifFailure.None : TzifFailure.LeapSeconds;
    }

    // The counts of a block header that the reader needs; the leap second count is 0 once the header is accepted.
    private readonly record struct Counts(long UniversalCount, long StandardCount, long TransitionCount, long TypeCount, long CharCount);
}

using System;
using Great.Parts.Cel.Library.Extensions.Strings;

namespace Great.Parts.Cel.Library.Extensions;

// The base64 of the extension set encoders: the standard alphabet. The decoder is the code's own: Convert.FromBase64String skips spaces
// and tabs and Base64.DecodeFromUtf8 has other rules than the Go decoder, which cel-go uses and which this one follows. Both attempts
// skip \r and \n anywhere, take nothing else outside the alphabet and the padding, and are lenient about the bits that the last
// character leaves unused. Offsets of an error are in UTF-16 units of the input.
internal static class Base64Codec
{
    // The characters of the text of a chunk of bytes that a stack buffer takes: 96 bytes give 128 characters, 256 bytes of stack, and a
    // chunk is a multiple of 3 bytes, so only the last one has padding.
    private const int ChunkBytes = 96;
    private const int ChunkCharacters = 128;

    // The number of characters of the text of n bytes: 4 for each group of 3, the last group padded.
    internal static long EncodedLength(long byteCount) => 4 * ((byteCount + 2) / 3);

    // The text of the bytes, with padding and no line breaks.
    internal static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes);

    // Appends the text of the bytes to the builder in chunks, so the text is charged as it grows.
    internal static void Append(ref ResultBuilder builder, ReadOnlySpan<byte> bytes)
    {
        Span<char> chunk = stackalloc char[ChunkCharacters];
        while (!bytes.IsEmpty)
        {
            ReadOnlySpan<byte> part = bytes[..Math.Min(ChunkBytes, bytes.Length)];
            _ = Convert.TryToBase64Chars(part, chunk, out int written);
            builder.Append(chunk[..written]);
            bytes = bytes[part.Length..];
        }
    }

    // The two attempts of the decoder: with padding, and when that fails without it. Returns whether the text is base64 and, if it is
    // not, the offset at which the second attempt stopped; the length is the number of bytes of the text, known after the pass that
    // validates and counts. Decode with a destination writes them.
    internal static bool TryCount(ReadOnlySpan<char> text, out int length, out int errorOffset, out bool padded)
    {
        padded = true;
        errorOffset = 0;
        if (TryDecode(text, true, [], false, out length, out _))
        {
            return true;
        }

        padded = false;
        return TryDecode(text, false, [], false, out length, out errorOffset);
    }

    // Writes the bytes of a text that TryCount accepted, with the attempt that accepted it.
    internal static void Decode(ReadOnlySpan<char> text, bool padded, Span<byte> destination) =>
        _ = TryDecode(text, padded, destination, true, out _, out _);

    // The decoder of one attempt, as decodeQuantum of Go: groups of four characters, \r and \n skipped, the padding of the last group
    // and what may follow it. With the flag it writes the bytes into the destination, which holds the count of the first pass.
    private static bool TryDecode(ReadOnlySpan<char> text, bool padded, Span<byte> destination, bool write, out int length, out int errorOffset)
    {
        length = 0;
        errorOffset = 0;
        Span<int> group = stackalloc int[4];
        int position = 0;
        while (position < text.Length)
        {
            group.Clear();
            GroupEnd end = ReadGroup(text, padded, ref position, group, out int count, out errorOffset);
            if (end == GroupEnd.Failed)
            {
                return false;
            }

            if (end == GroupEnd.NoGroup)
            {
                return true;
            }

            if (write)
            {
                WriteGroup(group, count, destination[length..]);
            }

            length += count - 1;
        }

        return true;
    }

    // One group of up to four characters, as decodeQuantum of Go reads it: \r and \n are skipped, the end of an unpadded text ends the
    // last group at two or three characters, and the padding ends it too, when it is in its place and nothing but line breaks follows.
    private static GroupEnd ReadGroup(ReadOnlySpan<char> text, bool padded, ref int position, Span<int> group, out int count, out int errorOffset)
    {
        count = 4;
        errorOffset = 0;
        for (int j = 0; j < 4; j++)
        {
            if (position == text.Length)
            {
                if (j == 0)
                {
                    return GroupEnd.NoGroup;
                }

                if (j == 1 || padded)
                {
                    errorOffset = position - j;
                    return GroupEnd.Failed;
                }

                count = j;
                return GroupEnd.Complete;
            }

            char character = text[position++];
            int value = Value(character);
            if (value >= 0)
            {
                group[j] = value;
            }
            else if (character is '\n' or '\r')
            {
                j--;
            }
            else if (padded && character == '=')
            {
                if (!TryEndWithPadding(text, j, ref position, out errorOffset))
                {
                    return GroupEnd.Failed;
                }

                count = j;
                return GroupEnd.Complete;
            }
            else
            {
                errorOffset = position - 1;
                return GroupEnd.Failed;
            }
        }

        return GroupEnd.Complete;
    }

    // The padding after j characters of a group, the first '=' read: not at the first or the second place; at the third the second '='
    // must follow, after any line breaks; and after the padding only line breaks may follow, otherwise the error is at what follows.
    private static bool TryEndWithPadding(ReadOnlySpan<char> text, int j, ref int position, out int errorOffset)
    {
        errorOffset = position - 1;
        if (j < 2)
        {
            return false;
        }

        if (j == 2)
        {
            position = SkipBreaks(text, position);
            if (position == text.Length)
            {
                errorOffset = text.Length;
                return false;
            }

            if (text[position] != '=')
            {
                errorOffset = position - 1;
                return false;
            }

            position++;
        }

        position = SkipBreaks(text, position);
        if (position < text.Length)
        {
            errorOffset = position;
            return false;
        }

        return true;
    }

    // The bytes of a group: three for four characters, two for three, one for two; the bits that the last character leaves unused are
    // ignored.
    private static void WriteGroup(ReadOnlySpan<int> group, int count, Span<byte> destination)
    {
        int bits = (group[0] << 18) | (group[1] << 12) | (group[2] << 6) | group[3];
        destination[0] = (byte)(bits >> 16);
        if (count > 2)
        {
            destination[1] = (byte)(bits >> 8);
        }

        if (count > 3)
        {
            destination[2] = (byte)bits;
        }
    }

    private enum GroupEnd
    {
        Complete,
        NoGroup,
        Failed,
    }

    private static int SkipBreaks(ReadOnlySpan<char> text, int position)
    {
        while (position < text.Length && text[position] is '\n' or '\r')
        {
            position++;
        }

        return position;
    }

    // The value of a character of the standard alphabet, or -1 for any other.
    private static int Value(char character) => character switch
    {
        >= 'A' and <= 'Z' => character - 'A',
        >= 'a' and <= 'z' => character - 'a' + 26,
        >= '0' and <= '9' => character - '0' + 52,
        '+' => 62,
        '/' => 63,
        _ => -1,
    };
}

using System;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// Code points over UTF-16, as the string functions count them: a high surrogate followed by a low one is one code point, and every
// other code unit is one, a lone surrogate included, which only a string of the application can hold. Each method makes one
// pass over what it is given; the cost of a call pays for that pass in the term ⌈Lₐ/10⌉.
internal static class CodePoints
{
    private const ushort HighSurrogateStart = 0xD800;
    private const ushort HighSurrogateEnd = 0xDBFF;

    // The number of code points: the code units less one for each high surrogate followed by a low one. The search runs over the code
    // units as ushort: the char overload of IndexOfAnyInRange allocates 96 bytes a call on .NET 10 and 11, the ushort overload nothing.
    internal static int Count(ReadOnlySpan<char> text)
    {
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(text);
        int count = text.Length;
        int position = 0;
        while (true)
        {
            int high = units[position..].IndexOfAnyInRange(HighSurrogateStart, HighSurrogateEnd);
            if (high < 0)
            {
                return count;
            }

            position += high + 1;
            if (position < text.Length && char.IsLowSurrogate(text[position]))
            {
                count--;
                position++;
            }
        }
    }

    // The number of code units of the code point that starts at the offset, which is below the length: 2 for a pair, 1 otherwise.
    internal static int UnitsAt(ReadOnlySpan<char> text, int offset) =>
        char.IsHighSurrogate(text[offset]) && offset + 1 < text.Length && char.IsLowSurrogate(text[offset + 1]) ? 2 : 1;

    // The offset in code units of the code point with the given index, 0 up to the number of code points, which gives the length;
    // -1 for an index below 0 or above that number.
    internal static int OffsetOf(ReadOnlySpan<char> text, long index)
    {
        if (index < 0)
        {
            return -1;
        }

        int offset = 0;
        for (long passed = 0; passed < index; passed++)
        {
            if (offset >= text.Length)
            {
                return -1;
            }

            offset += UnitsAt(text, offset);
        }

        return offset;
    }

    // Whether a cut before the offset would split a pair: a low surrogate after a high one.
    internal static bool SplitsPair(ReadOnlySpan<char> text, int offset) =>
        offset > 0 && offset < text.Length && char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset]);
}

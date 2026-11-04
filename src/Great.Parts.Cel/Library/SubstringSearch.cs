using System;
using System.Numerics;

namespace Great.Parts.Cel.Library;

// The search for a substring in linear time, Knuth–Morris–Pratt over UTF-16 code units, shared by contains and
// the other searches of strings. The search methods of .NET take O(n · m) in the worst case, and the
// cost of these functions assumes linear time. A step is one comparison of two code units: the prefix function
// of the pattern takes at most 2m and the pass over the text at most 2n.
internal static class SubstringSearch
{
    // A pattern up to this length keeps its prefix function on the stack, one byte for each code unit: each value
    // is below the length of the pattern.
    private const int StackPatternLength = 256;

    // The index of the first occurrence of the pattern in the text, or -1; the steps of the search are added to
    // the counter. An empty pattern is found at 0, and a pattern longer than the text is not found, without steps.
    internal static int IndexOf(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, ref long steps)
    {
        if (pattern.IsEmpty)
        {
            return 0;
        }

        if (pattern.Length > text.Length)
        {
            return -1;
        }

        if (pattern.Length <= StackPatternLength)
        {
            Span<byte> prefix = stackalloc byte[StackPatternLength];
            return Search(text, pattern, prefix[..pattern.Length], ref steps);
        }

        return Search(text, pattern, new int[pattern.Length], ref steps);
    }

    private static int Search<T>(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, Span<T> prefix, ref long steps)
        where T : unmanaged, IBinaryInteger<T>
    {
        FillPrefix(pattern, prefix, ref steps);

        // The last position at which an occurrence can start.
        int lastStart = text.Length - pattern.Length;
        char first = pattern[0];
        int matched = 0;
        int position = 0;
        while (position < text.Length)
        {
            if (matched == 0)
            {
                // Nothing matched: go to the next occurrence of the first code unit, one step for each unit passed.
                if (position > lastStart)
                {
                    return -1;
                }

                int skipped = text[position..(lastStart + 1)].IndexOf(first);
                if (skipped < 0)
                {
                    steps += lastStart + 1 - position;
                    return -1;
                }

                steps += skipped + 1;
                position += skipped + 1;
                matched = 1;
            }
            else
            {
                steps++;
                if (text[position] != pattern[matched])
                {
                    matched = int.CreateTruncating(prefix[matched - 1]);
                    continue;
                }

                position++;
                matched++;
            }

            if (matched == pattern.Length)
            {
                return position - matched;
            }
        }

        return -1;
    }

    // prefix[q] is the length of the longest proper prefix of pattern[..(q + 1)] that is also its suffix.
    private static void FillPrefix<T>(ReadOnlySpan<char> pattern, Span<T> prefix, ref long steps)
        where T : unmanaged, IBinaryInteger<T>
    {
        prefix[0] = T.Zero;
        int length = 0;
        for (int q = 1; q < pattern.Length; q++)
        {
            char unit = pattern[q];
            while (true)
            {
                steps++;
                if (unit == pattern[length])
                {
                    length++;
                    break;
                }

                if (length == 0)
                {
                    break;
                }

                length = int.CreateTruncating(prefix[length - 1]);
            }

            prefix[q] = T.CreateTruncating(length);
        }
    }
}

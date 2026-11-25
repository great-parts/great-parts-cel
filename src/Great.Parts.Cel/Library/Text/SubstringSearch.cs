using System;
using System.Numerics;

namespace Great.Parts.Cel.Library.Text;

// The search for a substring in linear time, which the engine of matches uses for the literal prefix of a pattern. A pattern of more
// than HybridLimit UTF-16 code units is searched by Knuth–Morris–Pratt: the search methods of .NET take O(n · m) in the worst case,
// and the time of matches grows no faster than linearly. A step is one comparison of two code units: the prefix function of the
// pattern takes at most 2m and the pass over the text at most 2n, plus one for each code unit that a jump to the next occurrence of
// the first code unit passes. A pattern of at most HybridLimit code units is searched by the vectorized ordinal search of .NET, whose
// worst case is HybridLimit · n comparisons, a constant factor; each place of the text where an occurrence may start that the search
// passes counts one step. The functions of strings do not use this search: they search through OrdinalSearch and pay for the worst
// case in their cost.
internal static class SubstringSearch
{
    // A pattern up to this length keeps its prefix function on the stack, one byte for each code unit: each value
    // is below the length of the pattern.
    internal const int StackPatternLength = 256;

    // A pattern of at most this many code units is searched by .NET, a longer one by Knuth–Morris–Pratt: on the texts that make the
    // search of .NET slowest, (ab)^n, it is faster than the linear search up to this length, and its worst case is linear with the
    // factor of the length.
    internal const int HybridLimit = 64;

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

        return pattern.Length <= HybridLimit
            ? FindOrdinal(text, pattern, ref steps)
            : IndexOfKmp(text, pattern, ref steps);
    }

    // The search by Knuth–Morris–Pratt for a pattern of any length, which the property tests of linearity check on short patterns too.
    internal static int IndexOfKmp(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, ref long steps)
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
            Span<byte> small = prefix[..pattern.Length];
            FillPrefix<byte>(pattern, small, ref steps);
            return Search<byte>(text, pattern, small, ref steps);
        }

        int[] large = new int[pattern.Length];
        FillPrefix<int>(pattern, large, ref steps);
        return Search<int>(text, pattern, large, ref steps);
    }

    // The first occurrence by the vectorized ordinal search of .NET, or -1; each place where an occurrence may start that the search
    // passes counts one step. The pattern is not empty, has at most HybridLimit code units and is not longer than the text.
    private static int FindOrdinal(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, ref long steps)
    {
        int found = OrdinalSearch.IndexOf(text, pattern);
        steps += found < 0 ? text.Length - pattern.Length + 1 : found + 1;
        return found;
    }

    // The place of the first occurrence, or -1.
    private static int Search<T>(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, ReadOnlySpan<T> prefix, ref long steps)
        where T : unmanaged, IBinaryInteger<T>
    {
        // The last place at which an occurrence can start.
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

                int skipped = text.Slice(position, lastStart + 1 - position).IndexOf(first);
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

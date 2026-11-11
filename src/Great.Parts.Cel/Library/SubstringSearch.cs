using System;
using System.Numerics;

namespace Great.Parts.Cel.Library;

// The search for a substring in linear time, Knuth–Morris–Pratt over UTF-16 code units, shared by contains and
// the other searches of strings. The search methods of .NET take O(n · m) in the worst case, and the
// cost of these functions assumes linear time. A step is one comparison of two code units: the prefix function
// of the pattern takes at most 2m and the pass over the text at most 2n, plus one for each code unit that a jump
// to the next occurrence of the first code unit passes.
internal static class SubstringSearch
{
    // A pattern up to this length keeps its prefix function on the stack, one byte for each code unit: each value
    // is below the length of the pattern.
    internal const int StackPatternLength = 256;

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
            Span<byte> small = prefix[..pattern.Length];
            FillPrefix<Forward, byte>(pattern, small, ref steps);
            return Search<Forward, byte>(text, pattern, small, 0, ref steps);
        }

        int[] large = new int[pattern.Length];
        FillPrefix<Forward, int>(pattern, large, ref steps);
        return Search<Forward, int>(text, pattern, large, 0, ref steps);
    }

    // The search for a pattern that runs many times over the same text, as replace and split do: the prefix function is made once,
    // and a search goes on from any position of the text. The pattern is not empty.
    internal readonly ref struct PreparedPattern
    {
        private readonly ReadOnlySpan<char> _pattern;
        private readonly Span<byte> _small;
        private readonly int[]? _large;

        private PreparedPattern(ReadOnlySpan<char> pattern, Span<byte> small, int[]? large)
        {
            _pattern = pattern;
            _small = small;
            _large = large;
        }

        // Prepares a pattern of at least one code unit; the steps of the preparation, at most 2m, are added to the counter. The
        // buffer is the stack space of the caller, StackPatternLength bytes: a pattern up to that length keeps its prefix function
        // in it, a longer one in an array of m ints. A reversed pattern is searched from the end of the text.
        internal static PreparedPattern Create(ReadOnlySpan<char> pattern, bool reversed, Span<byte> buffer, ref long steps)
        {
            if (pattern.Length <= StackPatternLength)
            {
                Span<byte> small = buffer[..pattern.Length];
                if (reversed)
                {
                    FillPrefix<Backward, byte>(pattern, small, ref steps);
                }
                else
                {
                    FillPrefix<Forward, byte>(pattern, small, ref steps);
                }

                return new PreparedPattern(pattern, small, null);
            }

            int[] large = new int[pattern.Length];
            if (reversed)
            {
                FillPrefix<Backward, int>(pattern, large, ref steps);
            }
            else
            {
                FillPrefix<Forward, int>(pattern, large, ref steps);
            }

            return new PreparedPattern(pattern, default, large);
        }

        // The index of the first occurrence that starts at or after from, or -1; the pattern is prepared forward.
        internal int IndexOf(ReadOnlySpan<char> text, int from, ref long steps) =>
            _large is null
                ? Search<Forward, byte>(text, _pattern, _small, from, ref steps)
                : Search<Forward, int>(text, _pattern, _large, from, ref steps);

        // The index of the last occurrence that ends at or before end, or -1; the pattern is prepared reversed.
        internal int LastIndexOf(ReadOnlySpan<char> text, int end, ref long steps)
        {
            ReadOnlySpan<char> window = text[..end];
            int found = _large is null
                ? Search<Backward, byte>(window, _pattern, _small, 0, ref steps)
                : Search<Backward, int>(window, _pattern, _large, 0, ref steps);
            return found < 0 ? -1 : end - found - _pattern.Length;
        }
    }

    // The direction in which a search reads the text and the pattern: forward, or from the end for the last occurrence. Search runs
    // over the units in the order of the direction, and its results are places in that order.
    private interface IDirection
    {
        // The unit that comes at the given place of the sequence in the direction.
        static abstract char Unit(ReadOnlySpan<char> sequence, int place);

        // The number of units from position to the next unit equal to first among the places position to lastStart, or -1 if there is none.
        static abstract int Skip(ReadOnlySpan<char> text, int position, int lastStart, char first);
    }

    private readonly struct Forward : IDirection
    {
        public static char Unit(ReadOnlySpan<char> sequence, int place) => sequence[place];

        public static int Skip(ReadOnlySpan<char> text, int position, int lastStart, char first) =>
            text.Slice(position, lastStart + 1 - position).IndexOf(first);
    }

    private readonly struct Backward : IDirection
    {
        public static char Unit(ReadOnlySpan<char> sequence, int place) => sequence[sequence.Length - 1 - place];

        public static int Skip(ReadOnlySpan<char> text, int position, int lastStart, char first)
        {
            // The places position to lastStart are the indexes length − 1 − lastStart to length − 1 − position of the text.
            int low = text.Length - 1 - lastStart;
            int found = text.Slice(low, lastStart + 1 - position).LastIndexOf(first);
            return found < 0 ? -1 : lastStart - found - position;
        }
    }

    // The place of the first occurrence at or after from, in the units of the direction, or -1.
    private static int Search<TDirection, T>(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, ReadOnlySpan<T> prefix, int from, ref long steps)
        where TDirection : struct, IDirection
        where T : unmanaged, IBinaryInteger<T>
    {
        // The last place at which an occurrence can start.
        int lastStart = text.Length - pattern.Length;
        char first = TDirection.Unit(pattern, 0);
        int matched = 0;
        int position = from;
        while (position < text.Length)
        {
            if (matched == 0)
            {
                // Nothing matched: go to the next occurrence of the first code unit, one step for each unit passed.
                if (position > lastStart)
                {
                    return -1;
                }

                int skipped = TDirection.Skip(text, position, lastStart, first);
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
                if (TDirection.Unit(text, position) != TDirection.Unit(pattern, matched))
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

    // prefix[q] is the length of the longest proper prefix of pattern[..(q + 1)] that is also its suffix, in the units of the direction.
    private static void FillPrefix<TDirection, T>(ReadOnlySpan<char> pattern, Span<T> prefix, ref long steps)
        where TDirection : struct, IDirection
        where T : unmanaged, IBinaryInteger<T>
    {
        prefix[0] = T.Zero;
        int length = 0;
        for (int q = 1; q < pattern.Length; q++)
        {
            char unit = TDirection.Unit(pattern, q);
            while (true)
            {
                steps++;
                if (unit == TDirection.Unit(pattern, length))
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

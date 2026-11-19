using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Great.Parts.Cel.Library.Text;

// The search for a substring in linear time, shared by contains and the other searches of strings. A pattern of more than
// HybridLimit UTF-16 code units is searched by Knuth–Morris–Pratt: the search methods of .NET take O(n · m) in the worst case, and the
// cost of these functions assumes linear time. A step is one comparison of two code units: the prefix function of the pattern takes at
// most 2m and the pass over the text at most 2n, plus one for each code unit that a jump to the next occurrence of the first code
// unit passes. A pattern of at most HybridLimit code units is searched by the vectorized ordinal search of .NET, whose worst case is
// HybridLimit · n comparisons, a constant factor; each place of the text where an occurrence may start that the search passes
// counts one step.
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
            ? FindForward(text, pattern, 0, ref steps)
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
            FillPrefix<Forward, byte>(pattern, small, ref steps);
            return Search<Forward, byte>(text, pattern, small, 0, ref steps);
        }

        int[] large = new int[pattern.Length];
        FillPrefix<Forward, int>(pattern, large, ref steps);
        return Search<Forward, int>(text, pattern, large, 0, ref steps);
    }

    // The first occurrence at or after from by the vectorized search of .NET, or -1; each place where an occurrence may start that the
    // search passes counts one step. The pattern is not empty and has at most HybridLimit code units.
    private static int FindForward(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, int from, ref long steps)
    {
        int places = text.Length - pattern.Length + 1 - from;
        if (places <= 0)
        {
            return -1;
        }

        int found = VectorizedFind(text[from..], pattern, last: false);
        if (found < 0)
        {
            steps += places;
            return -1;
        }

        steps += found + 1;
        return from + found;
    }

    // The last occurrence that ends at or before end by the vectorized search of .NET, or -1; the places are counted from the last one.
    private static int FindBackward(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, int end, ref long steps)
    {
        int places = end - pattern.Length + 1;
        if (places <= 0)
        {
            return -1;
        }

        int found = VectorizedFind(text[..end], pattern, last: true);
        if (found < 0)
        {
            steps += places;
            return -1;
        }

        steps += places - found;
        return found;
    }

    // The ordinal search of .NET for the first or the last occurrence, the one place in the library that calls it: a pattern of at most
    // HybridLimit code units keeps the worst case of the search at that many comparisons for each code unit of the text, and the search
    // is vectorized.
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "A pattern of at most HybridLimit code units keeps the worst case at HybridLimit · n comparisons, and the search is vectorized.")]
    private static int VectorizedFind(ReadOnlySpan<char> window, ReadOnlySpan<char> pattern, bool last) =>
        last ? window.LastIndexOf(pattern, StringComparison.Ordinal) : window.IndexOf(pattern, StringComparison.Ordinal);

    // The search for a pattern that runs many times over the same text, as replace and split do: the prefix function is made once,
    // and a search goes on from any position of the text. The pattern is not empty. A pattern of at most HybridLimit code units has no
    // prefix function: it is searched by .NET in both directions.
    internal readonly ref struct PreparedPattern
    {
        private readonly ReadOnlySpan<char> _pattern;
        private readonly Span<byte> _small;
        private readonly int[]? _large;
        private readonly bool _vectorized;

        private PreparedPattern(ReadOnlySpan<char> pattern, Span<byte> small, int[]? large, bool vectorized)
        {
            _pattern = pattern;
            _small = small;
            _large = large;
            _vectorized = vectorized;
        }

        // Prepares a pattern of at least one code unit; the steps of the preparation, at most 2m, are added to the counter, and a pattern
        // searched by .NET has none. The buffer is the stack space of the caller, StackPatternLength bytes: a pattern up to that length
        // keeps its prefix function in it, a longer one in an array of m ints. A reversed pattern is searched from the end of the text.
        internal static PreparedPattern Create(ReadOnlySpan<char> pattern, bool reversed, Span<byte> buffer, ref long steps)
        {
            if (pattern.Length <= HybridLimit)
            {
                return new PreparedPattern(pattern, default, null, true);
            }

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

                return new PreparedPattern(pattern, small, null, false);
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

            return new PreparedPattern(pattern, default, large, false);
        }

        // The index of the first occurrence that starts at or after from, or -1; the pattern is prepared forward.
        internal int IndexOf(ReadOnlySpan<char> text, int from, ref long steps)
        {
            if (_vectorized)
            {
                return FindForward(text, _pattern, from, ref steps);
            }

            return _large is null
                ? Search<Forward, byte>(text, _pattern, _small, from, ref steps)
                : Search<Forward, int>(text, _pattern, _large, from, ref steps);
        }

        // The index of the last occurrence that ends at or before end, or -1; the pattern is prepared reversed.
        internal int LastIndexOf(ReadOnlySpan<char> text, int end, ref long steps)
        {
            if (_vectorized)
            {
                return FindBackward(text, _pattern, end, ref steps);
            }

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

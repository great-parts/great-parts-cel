using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The search for repeats in the tokens of one symbol set: a rolling hash over every window, a token by token comparison of
/// the windows with equal hashes, then the joining of overlapping and adjacent matching windows into one longest run.
/// </summary>
internal static class DuplicateSearch
{
    private const ulong Multiplier = 0x100000001B3UL;

    /// <summary>Finds the pairs of equal runs of at least <paramref name="window"/> tokens.</summary>
    /// <param name="stream">The tokens of all the files under one symbol set.</param>
    /// <param name="window">The length of the window in tokens.</param>
    /// <returns>The pairs, the first run earlier than the second one; two runs in one file never overlap.</returns>
    internal static IReadOnlyList<RawDuplicate> Find(TokenStream stream, int window)
    {
        List<(int Distance, int First)> matches = Matches(stream, window);
        matches.Sort();
        List<RawDuplicate> pairs = [];
        int index = 0;
        while (index < matches.Count)
        {
            int last = index;
            while (last + 1 < matches.Count
                && matches[last + 1].Distance == matches[index].Distance
                && matches[last + 1].First == matches[last].First + 1)
            {
                last++;
            }

            AddRun(stream, window, (matches[index].First, matches[index].Distance, last - index + window), pairs);
            index = last + 1;
        }

        return pairs;
    }

    // Every pair of windows with equal tokens, as the index of the first token of the earlier window and the distance to the
    // later one. Windows with equal hashes are compared token by token, so a collision gives no pair.
    private static List<(int Distance, int First)> Matches(TokenStream stream, int window)
    {
        List<(int, int)> matches = [];
        foreach (List<int> starts in Index(stream, window).Values)
        {
            for (int left = 0; left < starts.Count; left++)
            {
                for (int right = left + 1; right < starts.Count; right++)
                {
                    if (stream.Equal(starts[left], starts[right], window))
                    {
                        matches.Add((starts[right] - starts[left], starts[left]));
                    }
                }
            }
        }

        return matches;
    }

    // The start of every window of every file under its hash. A window never crosses the boundary of a file.
    private static Dictionary<ulong, List<int>> Index(TokenStream stream, int window)
    {
        Dictionary<ulong, List<int>> buckets = [];
        ulong top = Power(window);
        int from = 0;
        while (from < stream.Count)
        {
            int to = from;
            while (to < stream.Count && stream.FileOf(to) == stream.FileOf(from))
            {
                to++;
            }

            if (to - from >= window)
            {
                IndexFile(stream, (from, to), window, top, buckets);
            }

            from = to;
        }

        return buckets;
    }

    private static void IndexFile(TokenStream stream, (int From, int To) file, int window, ulong top, Dictionary<ulong, List<int>> buckets)
    {
        unchecked
        {
            ulong hash = 0;
            for (int index = file.From; index < file.From + window; index++)
            {
                hash = (hash * Multiplier) + Value(stream.KindAt(index));
            }

            Add(buckets, hash, file.From);
            for (int start = file.From + 1; start + window <= file.To; start++)
            {
                hash = (hash * Multiplier) - (Value(stream.KindAt(start - 1)) * top) + Value(stream.KindAt(start + window - 1));
                Add(buckets, hash, start);
            }
        }
    }

    private static ulong Value(ushort kind) => (ulong)kind + 1;

    private static ulong Power(int exponent)
    {
        ulong result = 1;
        for (int step = 0; step < exponent; step++)
        {
            result = unchecked(result * Multiplier);
        }

        return result;
    }

    private static void Add(Dictionary<ulong, List<int>> buckets, ulong hash, int start)
    {
        if (!buckets.TryGetValue(hash, out List<int>? starts))
        {
            starts = [];
            buckets[hash] = starts;
        }

        starts.Add(start);
    }

    // One run of equal tokens: the windows from the first one to the last one joined, so the run is the number of windows plus
    // the window minus one tokens. Two runs in one file must not overlap, so the run is cut to the distance between them; a
    // cut run shorter than the window is not a repeat.
    private static void AddRun(TokenStream stream, int window, (int First, int Distance, int Length) run, List<RawDuplicate> pairs)
    {
        int second = run.First + run.Distance;
        int tokens = stream.FileOf(run.First) == stream.FileOf(second) ? Math.Min(run.Length, run.Distance) : run.Length;
        if (tokens < window)
        {
            return;
        }

        pairs.Add(new RawDuplicate(Site(stream, run.First, tokens), Site(stream, second, tokens), tokens));
    }

    private static DuplicateSite Site(TokenStream stream, int first, int tokens) =>
        new(stream.FileOf(first), stream.StartOf(first), stream.EndOf(first + tokens - 1));
}

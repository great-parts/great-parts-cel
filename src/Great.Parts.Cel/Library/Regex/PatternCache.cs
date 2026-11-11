using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Great.Parts.Cel.Library.Regex;

// The programs of the patterns that an environment has seen. The cache does not change a result or a cost: a pattern found in it is
// charged as if it were built, and a pattern that was evicted is built again with the same outcome. It is empty when created, an
// environment has one of its own, and no cache is static.
//
// A lookup takes no lock. An insertion takes the one lock of the cache and, when the entry would pass either limit — the number of
// entries or the sum of their weights — first removes entries by CLOCK: the entries stand in a ring in the order they came, each
// with a flag that a hit sets, and the hand, at the oldest entry, passes an entry whose flag is set — it clears the flag and the
// entry becomes the newest — and removes an entry whose flag is clear. The work of one insertion is a few turns over a ring of at
// most the number of entries a cache may have; nothing happens except in an insertion. Two threads that miss the same pattern may
// both build it: the first insertion wins and the other entry is dropped.
internal sealed class PatternCache
{
    // The number of entries and the sum of their weights that a cache keeps: internal constants, no public parameter.
    internal const int DefaultMaxPatterns = 128;
    internal const int DefaultMaxWeight = 100_000;

    // A pattern longer than this, in UTF-16 code units, is not kept: its key would break the bound of the memory that the weights give.
    internal const int MaxKeyLength = 4096;

    private const int InitialRing = 8;

    private readonly ConcurrentDictionary<string, PatternEntry> entries = new(1, InitialRing * 2, StringComparer.Ordinal);
    private readonly Lock gate = new();
    private readonly int maxPatterns;
    private readonly int maxWeight;

    // The ring: the entries in the order they came, from the one at head, in a circular array that grows to the number of entries.
    private Slot[] ring;
    private int head;
    private int ringCount;
    private int weight;

    internal PatternCache()
        : this(DefaultMaxPatterns, DefaultMaxWeight)
    {
    }

    // A cache of other limits, for the tests of the limits and of the eviction.
    internal PatternCache(int maxPatterns, int maxWeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPatterns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWeight, 1);
        this.maxPatterns = maxPatterns;
        this.maxWeight = maxWeight;
        ring = new Slot[Math.Min(InitialRing, maxPatterns)];
    }

    // The number of entries and the sum of their weights now.
    internal int Count => Volatile.Read(ref ringCount);

    internal int Weight => Volatile.Read(ref weight);

    // The entry of a pattern; a hit marks the entry as used. Takes no lock.
    internal bool TryGet(string pattern, [NotNullWhen(true)] out PatternEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length <= MaxKeyLength && entries.TryGetValue(pattern, out entry))
        {
            entry.MarkReferenced();
            return true;
        }

        entry = null;
        return false;
    }

    // Keeps the entry of a pattern unless the cache has one already. An entry heavier than the capacity, or of a pattern longer than
    // MaxKeyLength, is not kept.
    internal void Add(string pattern, PatternEntry entry)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(entry);
        if (pattern.Length > MaxKeyLength || entry.Weight > maxWeight)
        {
            return;
        }

        lock (gate)
        {
            if (!entries.TryAdd(pattern, entry))
            {
                return;
            }

            while (ringCount >= maxPatterns || weight + entry.Weight > maxWeight)
            {
                EvictOne();
            }

            if (ringCount == ring.Length)
            {
                Grow();
            }

            ring[(head + ringCount) % ring.Length] = new Slot(pattern, entry);
            Volatile.Write(ref ringCount, ringCount + 1);
            Volatile.Write(ref weight, weight + entry.Weight);
        }
    }

    // Takes the oldest entry out of the ring. An entry that a hit has marked since the hand last passed it gets a second chance: the
    // flag is cleared and the entry goes to the end of the ring. The ring is not empty here, and after one turn every flag is clear,
    // so it ends.
    private void EvictOne()
    {
        while (true)
        {
            Slot oldest = ring[head];
            if (oldest.Entry.TakeReferenced())
            {
                // The slot after the last entry is free, or is this very slot when the ring is full.
                ring[(head + ringCount) % ring.Length] = oldest;
                if (ringCount < ring.Length)
                {
                    ring[head] = default;
                }

                head = (head + 1) % ring.Length;
                continue;
            }

            entries.TryRemove(oldest.Key, out _);
            ring[head] = default;
            head = (head + 1) % ring.Length;
            Volatile.Write(ref ringCount, ringCount - 1);
            Volatile.Write(ref weight, weight - oldest.Entry.Weight);
            return;
        }
    }

    // A ring that is full and below the limit of the number of entries gets room for twice as many, up to the limit; the entries
    // move to the front of the new array in their order.
    private void Grow()
    {
        var bigger = new Slot[Math.Min(maxPatterns, ring.Length * 2)];
        for (int i = 0; i < ringCount; i++)
        {
            bigger[i] = ring[(head + i) % ring.Length];
        }

        ring = bigger;
        head = 0;
    }

    private readonly struct Slot(string key, PatternEntry entry)
    {
        internal string Key { get; } = key;

        internal PatternEntry Entry { get; } = entry;
    }
}

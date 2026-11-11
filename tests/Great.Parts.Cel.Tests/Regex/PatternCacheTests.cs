using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The cache of programs: its two limits, the eviction by CLOCK, the entries of errors, the patterns that are not kept, the first
// insertion that wins, and any number of threads at once. An entry weighs the size of its program, and 1 for an error.
public sealed class PatternCacheTests
{
    // An entry of a program of the given size: size - 1 literal characters and the Match.
    private static PatternEntry Entry(int size) => new(ProgramHelper.Compile(new string('a', size - 1)));

    private static PatternEntry ErrorEntry() => new(new RegexError(0, RegexError.MissingParen));

    private static bool Has(PatternCache cache, string pattern) => cache.TryGet(pattern, out _);

    // The number of entries is a limit of its own: with room in the weights, the entry that does not fit by number takes the place
    // of one.
    [Fact]
    public static void Add_PastTheNumberOfEntries_EvictsOne()
    {
        var cache = new PatternCache(3, 1000);

        cache.Add("p1", Entry(2));
        cache.Add("p2", Entry(3));
        cache.Add("p3", Entry(4));
        Assert.Equal(3, cache.Count);
        Assert.Equal(9, cache.Weight);

        cache.Add("p4", Entry(5));
        Assert.Equal(3, cache.Count);
        Assert.Equal(12, cache.Weight);

        Assert.False(Has(cache, "p1"));
        Assert.True(Has(cache, "p2"));
        Assert.True(Has(cache, "p3"));
        Assert.True(Has(cache, "p4"));
    }

    // The sum of the weights is a limit of its own: with room in the number, the entry that does not fit by weight takes the place
    // of as many as it needs.
    [Fact]
    public static void Add_PastTheSumOfWeights_EvictsAsManyAsItTakes()
    {
        var cache = new PatternCache(100, 10);

        cache.Add("p1", Entry(4));
        cache.Add("p2", Entry(4));
        Assert.Equal(8, cache.Weight);

        cache.Add("p3", Entry(4));
        Assert.Equal(2, cache.Count);
        Assert.Equal(8, cache.Weight);
        Assert.False(Has(cache, "p1"));

        cache.Add("p4", Entry(10));
        Assert.Equal(1, cache.Count);
        Assert.Equal(10, cache.Weight);
        Assert.False(Has(cache, "p2"));
        Assert.False(Has(cache, "p3"));
        Assert.True(Has(cache, "p4"));
    }

    // A sum that is exactly the capacity fits, and so does a number of entries that is exactly the limit.
    [Fact]
    public static void Add_UpToTheLimits_EvictsNothing()
    {
        var cache = new PatternCache(2, 8);

        cache.Add("p1", Entry(4));
        cache.Add("p2", Entry(4));

        Assert.Equal(2, cache.Count);
        Assert.Equal(8, cache.Weight);
        Assert.True(Has(cache, "p1"));
        Assert.True(Has(cache, "p2"));
    }

    // CLOCK: the hand clears the flag of an entry that was read after it last passed and goes on to the next, so that entry
    // outlives one that was not read.
    [Fact]
    public static void Add_EvictsTheEntryThatWasNotRead_AndKeepsOneThatWas()
    {
        var cache = new PatternCache(3, 1000);
        cache.Add("a", Entry(2));
        cache.Add("b", Entry(2));
        cache.Add("c", Entry(2));
        Assert.True(Has(cache, "a"));

        cache.Add("d", Entry(2));

        Assert.Equal(3, cache.Count);
        Assert.True(Has(cache, "a"));
        Assert.False(Has(cache, "b"));
        Assert.True(Has(cache, "c"));
        Assert.True(Has(cache, "d"));
    }

    // The hand stays where it stopped: after "b" went, the next eviction looks at "c" first, and "a", whose flag was cleared, is
    // the third to go.
    [Fact]
    public static void Add_ContinuesTheTurnOfTheHandWhereItStopped()
    {
        var cache = new PatternCache(3, 1000);
        cache.Add("a", Entry(2));
        cache.Add("b", Entry(2));
        cache.Add("c", Entry(2));
        Assert.True(Has(cache, "a"));
        cache.Add("d", Entry(2));
        Assert.False(Has(cache, "b"));

        cache.Add("e", Entry(2));
        cache.Add("f", Entry(2));

        Assert.Equal(3, cache.Count);
        Assert.False(Has(cache, "c"));
        Assert.False(Has(cache, "a"));
        Assert.True(Has(cache, "d"));
        Assert.True(Has(cache, "e"));
        Assert.True(Has(cache, "f"));
    }

    // When every entry was read, the hand clears all the flags in one turn and removes the first it met.
    [Fact]
    public static void Add_WhenEveryEntryWasRead_StillEvictsOne()
    {
        var cache = new PatternCache(2, 1000);
        cache.Add("a", Entry(2));
        cache.Add("b", Entry(2));
        Assert.True(Has(cache, "a"));
        Assert.True(Has(cache, "b"));

        cache.Add("c", Entry(2));

        Assert.Equal(2, cache.Count);
        Assert.Equal(4, cache.Weight);
        Assert.False(Has(cache, "a"));
        Assert.True(Has(cache, "b"));
        Assert.True(Has(cache, "c"));
    }

    // A pattern that failed is kept like a program, weighs 1, and is found with its error and a size of 0.
    [Fact]
    public static void ErrorEntry_IsKeptAndFound()
    {
        var cache = new PatternCache();
        PatternEntry entry = ErrorEntry();

        cache.Add("(", entry);

        Assert.Equal(1, cache.Count);
        Assert.Equal(1, cache.Weight);
        Assert.True(cache.TryGet("(", out PatternEntry? found));
        Assert.Same(entry, found);
        Assert.Null(found.Program);
        Assert.NotNull(found.Error);
        Assert.Equal(0, found.Size);
        Assert.Equal(1, found.Weight);
    }

    // An entry of a program weighs its size, and keeps the program that was built.
    [Fact]
    public static void ProgramEntry_WeighsItsSize()
    {
        var cache = new PatternCache();
        PatternEntry entry = Entry(7);

        cache.Add("aaaaaa", entry);

        Assert.Equal(7, cache.Weight);
        Assert.True(cache.TryGet("aaaaaa", out PatternEntry? found));
        Assert.Same(entry, found);
        Assert.NotNull(found.Program);
        Assert.Null(found.Error);
        Assert.Equal(7, found.Size);
        Assert.Equal(7, found.Weight);
    }

    // A pattern longer than 4 096 code units is used and not kept; one of 4 096 is kept.
    [Fact]
    public static void Add_OfALongPattern_KeepsNothingBeyondTheLimitOfTheKey()
    {
        var cache = new PatternCache();
        string kept = new('a', PatternCache.MaxKeyLength);
        string refused = new('a', PatternCache.MaxKeyLength + 1);

        cache.Add(refused, ErrorEntry());
        Assert.Equal(0, cache.Count);
        Assert.False(Has(cache, refused));

        cache.Add(kept, ErrorEntry());
        Assert.Equal(1, cache.Count);
        Assert.True(Has(cache, kept));
    }

    // An entry heavier than the whole capacity is not kept, and takes no other entry out.
    [Fact]
    public static void Add_OfAnEntryHeavierThanTheCapacity_KeepsNothing()
    {
        var cache = new PatternCache(10, 5);
        cache.Add("small", Entry(5));

        cache.Add("heavy", Entry(6));

        Assert.Equal(1, cache.Count);
        Assert.Equal(5, cache.Weight);
        Assert.False(Has(cache, "heavy"));
        Assert.True(Has(cache, "small"));
    }

    // An entry exactly as heavy as the capacity is kept and takes everything else out.
    [Fact]
    public static void Add_OfAnEntryAsHeavyAsTheCapacity_TakesTheWholeCache()
    {
        var cache = new PatternCache(10, 5);
        cache.Add("one", ErrorEntry());
        cache.Add("two", ErrorEntry());

        cache.Add("whole", Entry(5));

        Assert.Equal(1, cache.Count);
        Assert.Equal(5, cache.Weight);
        Assert.True(Has(cache, "whole"));
    }

    // Of two insertions of one key the first wins, and the second changes nothing.
    [Fact]
    public static void Add_OfTheSameKeyTwice_KeepsTheFirst()
    {
        var cache = new PatternCache();
        PatternEntry first = Entry(3);
        PatternEntry second = Entry(9);

        cache.Add("p", first);
        cache.Add("p", second);

        Assert.Equal(1, cache.Count);
        Assert.Equal(3, cache.Weight);
        Assert.True(cache.TryGet("p", out PatternEntry? found));
        Assert.Same(first, found);
    }

    // Keys are compared by ordinal value: a case and a normalization form are different keys.
    [Fact]
    public static void Keys_AreComparedByOrdinalValue()
    {
        var cache = new PatternCache();
        cache.Add("a", Entry(2));

        Assert.False(Has(cache, "A"));
        Assert.False(Has(cache, "a "));
        Assert.True(Has(cache, "a"));
    }

    // A cache is empty when it is created, and a miss gives no entry.
    [Fact]
    public static void NewCache_IsEmpty()
    {
        var cache = new PatternCache();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Weight);
        Assert.False(cache.TryGet("a", out PatternEntry? entry));
        Assert.Null(entry);
    }

    // Two caches share nothing: the entry of one is not in the other.
    [Fact]
    public static void TwoCaches_ShareNothing()
    {
        var first = new PatternCache();
        var second = new PatternCache();

        first.Add("a", Entry(2));

        Assert.True(Has(first, "a"));
        Assert.False(Has(second, "a"));
        Assert.Equal(0, second.Count);
    }

    // The limits of a cache are at least 1.
    [Fact]
    public static void Constructor_WithALimitBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PatternCache(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PatternCache(10, 0));
    }

    // The ring of a cache that holds more entries than its first room is grown, and a cache of one entry never grows.
    [Fact]
    public static void Ring_GrowsToTheNumberOfEntries()
    {
        var cache = new PatternCache(40, 100_000);
        for (int i = 0; i < 60; i++)
        {
            cache.Add("p" + i, Entry(2));
        }

        Assert.Equal(40, cache.Count);
        Assert.Equal(80, cache.Weight);
        Assert.False(Has(cache, "p19"));
        Assert.True(Has(cache, "p20"));
        Assert.True(Has(cache, "p59"));

        var one = new PatternCache(1, 100);
        one.Add("x", Entry(2));
        one.Add("y", Entry(3));
        Assert.Equal(1, one.Count);
        Assert.Equal(3, one.Weight);
        Assert.True(Has(one, "y"));
    }

    // What the cache holds changes no result: the answers of patterns through a cache of one entry, through a cache that keeps all,
    // and without a cache are the same.
    [Fact]
    public static void Results_DoNotDependOnWhatTheCacheHolds()
    {
        string[] patterns = ["a+b", "(?i)k", "^a$", "(", "[a-c]{2}", "a+b", "\\bfoo\\b", "(", "[a-c]{2}", "x*"];
        string[] subjects = ["aab", "K", "a", "(", "bc", "foo bar", ""];
        var one = new PatternCache(1, 1000);
        var all = new PatternCache();

        foreach (string pattern in patterns)
        {
            foreach (string subject in subjects)
            {
                bool direct = Through(null, pattern, subject);
                Assert.Equal(direct, Through(one, pattern, subject));
                Assert.Equal(direct, Through(all, pattern, subject));
            }
        }

        Assert.Equal(1, one.Count);
        Assert.InRange(all.Count, 1, patterns.Length);
    }

    // Parses and builds a pattern as the caller of the cache does it, and keeps the outcome; false for a pattern that is refused.
    private static bool Through(PatternCache? cache, string pattern, string subject)
    {
        if (cache is null || !cache.TryGet(pattern, out PatternEntry? entry))
        {
            entry = Compiled(pattern);
            cache?.Add(pattern, entry);
        }

        if (entry.Program is null)
        {
            return false;
        }

        return ProgramHelper.IsMatch(entry.Program, subject, out _);
    }

    private static PatternEntry Compiled(string pattern)
    {
        if (!PatternParser.TryParse(pattern, out PatternTree? tree, out RegexError? error))
        {
            return new PatternEntry(error!);
        }

        using (tree)
        {
            return new PatternEntry(ProgramBuilder.Build(tree!));
        }
    }

    // Many threads look up and insert the same few hundred patterns in a cache of 16 entries. No thread fails, a hit is the entry
    // of its own pattern, and when they are done the count and the sum of the weights are those of the entries that can be found.
    [Fact]
    public static void ParallelLookupsAndInsertions_KeepTheCacheConsistent()
    {
        const int Threads = 8;
        const int Rounds = 4000;
        const int Patterns = 300;
        var cache = new PatternCache(16, 100);
        var failures = new List<string>();
        using var start = new Barrier(Threads);
        var threads = new Thread[Threads];
        for (int i = 0; i < Threads; i++)
        {
            int seed = i;
            threads[i] = new Thread(() =>
            {
                start.SignalAndWait();
                Work(cache, new Sequence((uint)seed), Rounds, Patterns, failures);
            });
            threads[i].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.Empty(failures);
        int found = 0;
        int weight = 0;
        for (int number = 0; number < Patterns; number++)
        {
            if (cache.TryGet(Pattern(number), out PatternEntry? entry))
            {
                found++;
                weight += entry.Weight;
            }
        }

        Assert.Equal(cache.Count, found);
        Assert.Equal(cache.Weight, weight);
        Assert.InRange(found, 1, 16);
    }

    // One thread: a lookup of a random pattern, and the insertion of its entry on a miss.
    private static void Work(PatternCache cache, Sequence random, int rounds, int patterns, List<string> failures)
    {
        for (int round = 0; round < rounds; round++)
        {
            int number = random.Next(patterns);
            string pattern = Pattern(number);
            if (cache.TryGet(pattern, out PatternEntry? hit))
            {
                CheckHit(pattern, number, hit, failures);
            }
            else
            {
                cache.Add(pattern, number % 7 == 0 ? ErrorEntry() : Entry((number % 9) + 2));
            }

            if (cache.Count > 16 || cache.Weight > 100)
            {
                lock (failures)
                {
                    failures.Add($"limits passed: {cache.Count} entries, weight {cache.Weight}");
                }
            }
        }
    }

    // A deterministic sequence of numbers for a thread: xorshift, from the number of the thread.
    private sealed class Sequence(uint seed)
    {
        private uint state = (seed * 2654435761u) + 1;

        internal int Next(int bound)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)bound);
        }
    }

    private static string Pattern(int number) => "pattern-" + number;

    // The entry that a pattern was given: an error for a multiple of 7, a program of size (number mod 9) + 2 otherwise.
    private static void CheckHit(string pattern, int number, PatternEntry hit, List<string> failures)
    {
        bool isError = number % 7 == 0;
        int weight = isError ? 1 : (number % 9) + 2;
        if ((hit.Error is not null) != isError || hit.Weight != weight)
        {
            lock (failures)
            {
                failures.Add($"{pattern}: the entry has weight {hit.Weight}, expected {weight}");
            }
        }
    }
}

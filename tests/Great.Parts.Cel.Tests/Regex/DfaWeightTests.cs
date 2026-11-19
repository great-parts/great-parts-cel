using System.Linq;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The weight of an entry of the cache of patterns with an automaton: m + ⌈D/16⌉, m the size of the program and D the bytes of the
// tables of the automaton — the transitions, the answers for the end of the text, the table of the classes, the starts of their
// intervals and their numbers. A program without an automaton weighs m, an error 1. The capacity of the cache, 100 000, is counted in
// these units.
public sealed class DfaWeightTests
{
    // 'abc': the size is 4 (three characters and the match). The classes are four — the letters a, b, c and the rest of the code
    // points — in five intervals: 128 bytes of the table of the code points below 128, 5 · 4 of starts, 5 of numbers, 153 in all. The
    // states are three; with the two rows of the answers there are five rows of four classes, 20 · 2 bytes, and five answers for the
    // end of the text: 40 + 5 = 45. D = 198, ⌈198/16⌉ = 13, the weight 17.
    [Fact]
    public static void Entry_OfALiteral_WeighsItsSizeAndItsTablesIn16Bytes()
    {
        CompiledPattern program = ProgramHelper.Compile("abc");
        Dfa automaton = DfaHelper.Build("abc");
        program.Automaton = automaton;

        var entry = new PatternEntry(program);

        Assert.Equal((3, 4, 198), (automaton.StateCount, program.Size, automaton.TableBytes));
        Assert.Equal((17, 4), (entry.Weight, entry.Size));
    }

    // The same program without its automaton weighs its size alone.
    [Fact]
    public static void Entry_OfAProgramWithoutAnAutomaton_WeighsItsSize()
    {
        CompiledPattern program = ProgramHelper.Compile("abc");

        var entry = new PatternEntry(program);

        Assert.Equal((4, 4), (entry.Weight, entry.Size));
    }

    // The weight is the size plus the tables rounded up to a whole unit of 16 bytes.
    [Theory]
    [InlineData("abc")]
    [InlineData("ORD-[0-9]+")]
    [InlineData("^[^@]+@[^@]+[.][a-z]+$")]
    [InlineData("(a|b)*a(a|b){7}")]
    [InlineData("^[\\p{L} -]+$")]
    [InlineData("")]
    public static void Entry_WeighsTheSizePlusTheTablesRoundedUpTo16Bytes(string pattern)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);
        program.Automaton = DfaHelper.Build(pattern);

        var entry = new PatternEntry(program);

        int bytes = program.Automaton.TableBytes;
        Assert.True(entry.Weight * 16 >= (program.Size * 16) + bytes, pattern);
        Assert.True(entry.Weight * 16 < (program.Size * 16) + bytes + 16, pattern);
    }

    // An automaton of 64 classes and 64 states takes some 8 KB: some five hundred units, which the cache counts as it counts a program
    // of that size.
    [Fact]
    public static void Entry_OfAnAutomatonOf64Classes_WeighsSomeHundredsOfUnits()
    {
        string letters = Letters(0x4E00, 63);
        CompiledPattern program = ProgramHelper.Compile(letters);
        program.Automaton = DfaHelper.Build(letters);

        var entry = new PatternEntry(program);

        Assert.Equal(64, program.Automaton.ClassCount);
        Assert.InRange(entry.Weight - program.Size, 500, 600);
    }

    // The preparation of a literal builds the automaton and keeps the entry with its weight: 17 for 'abc'. A pattern without an
    // automaton, because of a condition of a word, weighs its size.
    [Fact]
    public static void Preparer_KeepsTheEntryWithTheWeightOfTheAutomaton()
    {
        var cache = new PatternCache();

        var literal = Matches.Preparer("abc", cache);
        var word = Matches.Preparer("\\bab", cache);

        var program = (CompiledPattern)literal.Prepared!;
        var wordProgram = (CompiledPattern)word.Prepared!;
        Assert.NotNull(program.Automaton);
        Assert.Null(wordProgram.Automaton);
        Assert.Equal((2, 17 + wordProgram.Size), (cache.Count, cache.Weight));
        Assert.True(cache.TryGet("abc", out PatternEntry? entry));
        Assert.Same(program, entry.Program);
        Assert.Equal(17, entry.Weight);
    }

    // A second preparation of the pattern finds the entry: the same program with the same automaton, built once.
    [Fact]
    public static void Preparer_FindsTheEntryOfAKnownLiteral()
    {
        var cache = new PatternCache();

        var first = Matches.Preparer("abc", cache);
        var second = Matches.Preparer("abc", cache);

        Assert.Same(first.Prepared, second.Prepared);
        Assert.Equal((1, 17), (cache.Count, cache.Weight));
    }

    // The weight counts against the capacity: with room for 20 units 'abc' (17) is kept, and another literal of 17 takes its place; a
    // capacity of 16 does not keep it at all, though the literal is prepared and has its automaton.
    [Fact]
    public static void Cache_CountsTheWeightOfTheAutomaton_AgainstItsCapacity()
    {
        var cache = new PatternCache(10, 20);

        _ = Matches.Preparer("abc", cache);
        Assert.Equal((1, 17), (cache.Count, cache.Weight));

        _ = Matches.Preparer("xyz", cache);
        Assert.Equal((1, 17), (cache.Count, cache.Weight));
        Assert.False(cache.TryGet("abc", out _));
        Assert.True(cache.TryGet("xyz", out _));

        var small = new PatternCache(10, 16);
        var preparation = Matches.Preparer("abc", small);

        Assert.Equal((0, 0), (small.Count, small.Weight));
        Assert.NotNull(((CompiledPattern)preparation.Prepared!).Automaton);
    }

    // Three literals of 40 different letters have each the weight of a program and of an automaton of 41 classes; a cache that has room
    // for two of them keeps the last two.
    [Fact]
    public static void Cache_EvictsByTheWeightOfLargeAutomata()
    {
        string first = Letters(0x4E00, 40);
        string second = Letters(0x4F00, 40);
        string third = Letters(0x5000, 40);
        var measure = new PatternCache();
        _ = Matches.Preparer(first, measure);
        Assert.True(measure.TryGet(first, out PatternEntry? entry));
        int one = entry.Weight;
        var cache = new PatternCache(100, (2 * one) + 1);

        _ = Matches.Preparer(first, cache);
        _ = Matches.Preparer(second, cache);
        Assert.Equal((2, 2 * one), (cache.Count, cache.Weight));

        _ = Matches.Preparer(third, cache);

        Assert.Equal((2, 2 * one), (cache.Count, cache.Weight));
        Assert.False(cache.TryGet(first, out _));
        Assert.True(cache.TryGet(third, out _));
        Assert.True(one > 41 * 4, "the weight is more than the size of the program");
    }

    // A pattern from data builds no automaton: the program is run by the engine alone and the entry weighs its size.
    [Fact]
    public static void PatternFromData_HasNoAutomaton_AndWeighsItsSize()
    {
        var cache = new PatternCache();

        Assert.True(DfaHelper.Call("xabcx", "abc", null, cache));

        Assert.True(cache.TryGet("abc", out PatternEntry? entry));
        Assert.Null(entry.Program!.Automaton);
        Assert.Equal(entry.Program.Size, entry.Weight);
    }

    // A literal of the text of a pattern from data that is already in the cache gets an automaton, and the entry stays as it is: the
    // program of the entry may be running on other threads. The prepared program is a copy over the same instructions, with the same
    // size, so the answers and the charges are the same.
    [Fact]
    public static void Literal_AfterAPatternFromData_GetsAnAutomatonOnACopy_AndTheEntryStays()
    {
        var cache = new PatternCache();
        Assert.True(DfaHelper.Call("xabcx", "abc", null, cache));
        Assert.True(cache.TryGet("abc", out PatternEntry? entry));
        CompiledPattern cached = entry.Program!;
        int weight = entry.Weight;

        var preparation = Matches.Preparer("abc", cache);
        var prepared = (CompiledPattern)preparation.Prepared!;

        Assert.NotNull(prepared.Automaton);
        Assert.NotSame(cached, prepared);
        Assert.Null(cached.Automaton);
        Assert.False(cached.AutomatonTried);
        Assert.Equal(ProgramHelper.Listing(cached), ProgramHelper.Listing(prepared));
        Assert.Equal(cached.Size, prepared.Size);
        Assert.True(cache.TryGet("abc", out PatternEntry? kept));
        Assert.Same(entry, kept);
        Assert.Same(cached, kept.Program);
        Assert.Equal(weight, kept.Weight);
        Assert.True(DfaHelper.Call("xabcx", "abc", prepared, cache));
        Assert.False(DfaHelper.Call("xabx", "abc", prepared, cache));

        // A second literal of the text gets another copy, and a literal that built its own program has the automaton in the entry.
        Assert.NotSame(prepared, Matches.Preparer("abc", cache).Prepared);
        var fresh = new PatternCache();
        var built = (CompiledPattern)Matches.Preparer("abc", fresh).Prepared!;
        Assert.Same(built, Matches.Preparer("abc", fresh).Prepared);
        Assert.NotNull(built.Automaton);
    }

    // A run of the given number of different letters of the CJK block from a code point: each is a class of its own.
    private static string Letters(int first, int count) => string.Concat(Enumerable.Range(first, count).Select(char.ConvertFromUtf32));
}

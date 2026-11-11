using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The parse, and the height, product and size that go with it, use no recursion: each pattern here is parsed on a thread whose
// stack is 128 KB, which a recursion of a thousand levels would overflow. Go itself recurses in calcHeight, repeatIsValid,
// Simplify and Compile and in the factoring of alternatives, and the tree of a pattern of the greatest depth is as deep as Go's.
public sealed class NoRecursionTests
{
    private const int SmallStack = 128 * 1024;

    private static void OnSmallStack(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    failure = e;
                }
            },
            SmallStack);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    private static void ParsesToHeightAndSize(string pattern, int height, int size)
    {
        using PatternTree tree = ParseHelper.Parse(pattern);
        Assert.Equal(height, tree.Height(tree.Root));
        Assert.Equal(size, tree.Size);
    }

    // 999 groups, which is height 1000, and 4000 groups that are no node.
    [Fact]
    public static void NestedGroups_AreParsedWithoutRecursion()
    {
        OnSmallStack(() => ParsesToHeightAndSize(Repeat("(", 999) + "a" + Repeat(")", 999), 1000, 2));
        OnSmallStack(() => ParsesToHeightAndSize(Repeat("(?:", 4000) + "a" + Repeat(")", 4000), 1, 2));
        OnSmallStack(() => ParseHelper.AssertRefused(Repeat("(", 1000) + "a" + Repeat(")", 1000), 0, RegexError.NestingDepth));
        OnSmallStack(() => ParseHelper.AssertRefused(Repeat("(", 5000), 0, RegexError.MissingParen));
    }

    // 999 stars one inside the other, height 1000; each collapses in the size, as Simplify does it.
    [Fact]
    public static void NestedRepetitions_AreParsedWithoutRecursion()
    {
        OnSmallStack(() => ParsesToHeightAndSize(Repeat("(?:", 999) + "a" + Repeat(")*", 999), 1000, 3));
        OnSmallStack(() => ParsesToHeightAndSize(Repeat("(?:", 900) + "a" + Repeat("){1}", 900), 901, 2));
        OnSmallStack(() => ParseHelper.AssertRefused(Repeat("(?:", 1000) + "a" + Repeat(")+", 1000), 0, RegexError.NestingDepth));
    }

    // The product of counts is kept in the nodes: nine nested counts of 2 is 512, ten is 1024.
    [Fact]
    public static void NestedCounts_AreCheckedWithoutWalkingTheTree()
    {
        OnSmallStack(() => ParsesToHeightAndSize(NestedTwos(9), 10, 513));
        OnSmallStack(() => ParseHelper.AssertRefusedAtTheLastBrace(NestedTwos(10), RegexError.InvalidRepeatCount));
        OnSmallStack(() => ParsesToHeightAndSize("(?:" + Repeat("(?:", 500) + "a{1000}" + Repeat("){1}", 500) + ")", 502, 1001));
    }

    private static string NestedTwos(int levels) => Repeat("(?:", levels - 1) + "a{2}" + Repeat("){2}", levels - 1);

    // The counts and the size at their greatest: a{1000} 99 times and 999, which is 100 000.
    [Fact]
    public static void CountAtTheLimit_AndTheGreatestSize_AreComputedOnTheTree()
    {
        OnSmallStack(() => ParsesToHeightAndSize(Repeat("a{1000}", 99) + "a{999}", 3, 100_000));
        OnSmallStack(() => ParsesToHeightAndSize("a{1000}", 2, 1001));
        OnSmallStack(() => ParseHelper.AssertRefused(Repeat("a{1000}", 100), 0, RegexError.TooLarge));
    }

    // A chain of alternatives that each extend the one before is factored one level at a time: 500 levels, which is the deepest
    // that is accepted. The frames of the factoring are on the heap.
    [Fact]
    public static void NestedFactoring_IsAnExplicitStack()
    {
        string chain = string.Join('|', Enumerable.Range(1, 500).Select(i => new string('a', i)));
        string tooDeep = string.Join('|', Enumerable.Range(1, 501).Select(i => new string('a', i)));

        OnSmallStack(() => ParsesToHeightAndSize(chain, 999, 1000));
        OnSmallStack(() => ParseHelper.AssertRefused(tooDeep, 0, RegexError.NestingDepth));
    }

    // Long patterns of one construct and the trees printed from them.
    [Fact]
    public static void LongPatterns_AreParsedWithoutRecursion()
    {
        OnSmallStack(() => ParsesToHeightAndSize(new string('a', 90_000), 1, 90_001));
        OnSmallStack(() => ParsesToHeightAndSize(Repeat("a*", 30_000), 3, 60_001));
    }

    // The printing of a tree in the tests is a loop too, so the tree of the deepest pattern can be shown.
    [Fact]
    public static void DeepTree_CanBeShown()
    {
        OnSmallStack(() => Assert.Equal(Repeat("Capture(", 999) + "lit:a" + Repeat(")", 999), ParseHelper.Tree(Repeat("(", 999) + "a" + Repeat(")", 999))));
    }

    // The build of the program from the tree is a loop over the tasks of the walk, so a tree of the greatest height and a count at its
    // place in a program of 100 000 instructions are built on a stack of 128 KB, and the engine runs the program on it.
    [Fact]
    public static void Build_OfTheGreatestTreesAndProgram_UsesNoRecursion()
    {
        OnSmallStack(() => BuildsAndRuns(Repeat("(", 999) + "a" + Repeat(")", 999), 2, "xa", true));
        OnSmallStack(() => BuildsAndRuns(Repeat("(?:", 999) + "a" + Repeat(")*", 999), 3, "b", true));
        OnSmallStack(() => BuildsAndRuns(NestedTwos(9), 513, "aaa", false));
        OnSmallStack(() => BuildsAndRuns("(?:" + Repeat("(?:", 500) + "a{1000}" + Repeat("){1}", 500) + ")", 1001, "aaa", false));
        OnSmallStack(() => BuildsAndRuns(Repeat("a{1000}", 99) + "a{999}", 100_000, "aaaaaaaaaa", false));
        OnSmallStack(() => BuildsAndRuns(new string('a', 90_000), 90_001, "aaa", false));
        OnSmallStack(() => BuildsAndRuns(Repeat("a*", 30_000), 60_001, "b", true));
    }

    // The frames of the factoring are on the heap, and so is the build of the factored tree: the chain of 500 levels.
    [Fact]
    public static void Build_OfTheFactoredChain_UsesNoRecursion()
    {
        string chain = string.Join('|', Enumerable.Range(1, 500).Select(i => new string('a', i)));

        OnSmallStack(() => BuildsAndRuns(chain, 1000, "a", true));
    }

    private static void BuildsAndRuns(string pattern, int instructions, string subject, bool matches)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);
        Assert.Equal(instructions, program.Instructions.Length);
        Assert.Equal(matches, ProgramHelper.IsMatch(program, subject, out _));
    }
}

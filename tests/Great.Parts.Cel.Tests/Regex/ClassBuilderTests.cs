using Great.Parts.Cel.Library.Regex;
using Great.Parts.Cel.Library.Unicode;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The list of ranges of a class: adding, merging, negating and folding, in a class node of a tree of its own. Ranges are written
// as lo-hi in hexadecimal.
public sealed class ClassBuilderTests
{
    private static string Cleaned(PatternTree tree, int node)
    {
        ClassBuilder.Clean(tree, node);
        return ParseHelper.Ranges(tree.ClassRanges(node));
    }

    [Fact]
    public static void AppendRange_JoinsTheRangesThatOverlapOrTouch()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendRange(tree, node, 1, 3);
        ClassBuilder.AppendRange(tree, node, 4, 6);
        ClassBuilder.AppendRange(tree, node, 5, 5);

        Assert.Equal("1-6", ParseHelper.Ranges(tree.ClassRanges(node)));
    }

    // The new range is compared with the last two, so that a folded alphabet comes in as two growing runs.
    [Fact]
    public static void AppendRange_LooksAtTheLastTwoRanges()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendRange(tree, node, 1, 6);
        ClassBuilder.AppendRange(tree, node, 10, 12);
        ClassBuilder.AppendRange(tree, node, 5, 7);
        ClassBuilder.AppendRange(tree, node, 20, 20);
        ClassBuilder.AppendRange(tree, node, 8, 8);

        Assert.Equal("1-7,A-C,14,8", ParseHelper.Ranges(tree.ClassRanges(node)));
    }

    [Fact]
    public static void Clean_SortsAndMerges()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        foreach ((int lo, int hi) in new[] { (0x20, 0x30), (1, 5), (3, 8), (9, 9), (0x40, 0x40), (0x25, 0x50), (0x60, 0x60) })
        {
            ClassBuilder.AppendRange(tree, node, lo, hi);
        }

        Assert.Equal("1-9,20-50,60", Cleaned(tree, node));
        Assert.Equal(3, tree.OwnedRanges);
    }

    // Ranges with the same start sort with the longest first, so the merge keeps the longest.
    [Fact]
    public static void Clean_KeepsTheLongestOfTheSameStart()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendRange(tree, node, 50, 60);
        ClassBuilder.AppendRange(tree, node, 5, 6);
        ClassBuilder.AppendRange(tree, node, 1, 1);
        ClassBuilder.AppendRange(tree, node, 5, 9);
        ClassBuilder.AppendRange(tree, node, 5, 7);

        Assert.Equal("1,5-9,32-3C", Cleaned(tree, node));
    }

    [Fact]
    public static void Clean_OfASortedListIsTheSameList()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendClass(tree, node, UnicodeTables.Ranges(0));
        string before = ParseHelper.Ranges(tree.ClassRanges(node));

        Assert.Equal(before, Cleaned(tree, node));
        Assert.Equal(UnicodeTables.Ranges(0).Length / 2, tree.OwnedRanges);
    }

    [Theory]
    [InlineData("10-20,30-40", "0-F,21-2F,41-10FFFF")]
    [InlineData("0-5", "6-10FFFF")]
    [InlineData("5-10FFFF", "0-4")]
    [InlineData("0-10FFFF", "")]
    [InlineData("", "0-10FFFF")]
    [InlineData("0-5,7-10FFFF", "6")]
    [InlineData("61", "0-60,62-10FFFF")]
    public static void Negate_IsTheComplementOverTheCodePoints(string ranges, string expected)
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        ParseHelper.AppendRanges(tree, node, ranges);

        ClassBuilder.Clean(tree, node);
        ClassBuilder.Negate(tree, node);

        Assert.Equal(expected, ParseHelper.Ranges(tree.ClassRanges(node)));
    }

    [Fact]
    public static void NegateTwice_IsTheList()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendClass(tree, node, UnicodeTables.Ranges(5));
        string before = ParseHelper.Ranges(tree.ClassRanges(node));

        ClassBuilder.Negate(tree, node);
        ClassBuilder.Negate(tree, node);

        Assert.Equal(before, ParseHelper.Ranges(tree.ClassRanges(node)));
    }

    // The fold of a range is the range and every code point of an orbit that the range meets, from the generated orbits.
    [Theory]
    [InlineData(0x4B, 0x4B, "4B,6B,212A")]
    [InlineData(0x6B, 0x6B, "4B,6B,212A")]
    [InlineData(0x212A, 0x212A, "4B,6B,212A")]
    [InlineData(0xDF, 0xDF, "DF,1E9E")]
    [InlineData(0x345, 0x345, "345,399,3B9,1FBE")]
    [InlineData(0x3A3, 0x3A3, "3A3,3C2-3C3")]
    [InlineData(0x30, 0x39, "30-39")]
    [InlineData(0x41, 0x4A, "41-4A,61-6A")]
    [InlineData(0x61, 0x7A, "41-5A,61-7A,17F,212A")]
    [InlineData(0x130, 0x131, "130-131")]
    [InlineData(0x49, 0x49, "49,69")]
    public static void AppendFoldedRange_AddsTheOrbits(int lo, int hi, string expected)
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendFoldedRange(tree, node, lo, hi);

        Assert.Equal(Sorted(expected), Cleaned(tree, node));
    }

    private static string Sorted(string ranges)
    {
        // The expected lists are written in orbit order; the cleaned list is by code point.
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        ParseHelper.AppendRanges(tree, node, ranges);
        return Cleaned(tree, node);
    }

    // A range that holds a whole class of orbits is itself after the fold, whatever it holds.
    [Fact]
    public static void AppendFoldedRange_OfEverything_IsEverything()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendFoldedRange(tree, node, 0, StaticTables.MaxCodePoint);

        Assert.Equal("0-10FFFF", Cleaned(tree, node));
    }

    [Fact]
    public static void AppendFoldedClass_OfAnAlphabet_IsTheAlphabetAndItsFold()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendFoldedClass(tree, node, StaticTables.Ranges(StaticTables.Word));

        Assert.Equal("30-39,41-5A,5F,61-7A,17F,212A", Cleaned(tree, node));
    }

    [Fact]
    public static void AppendNegatedClass_IsTheComplementOfATable()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendNegatedClass(tree, node, StaticTables.Ranges(StaticTables.Digit));

        Assert.Equal("0-2F,3A-10FFFF", ParseHelper.Ranges(tree.ClassRanges(node)));
    }

    [Fact]
    public static void AppendNodeClass_AppendsAnotherListOrItsComplement()
    {
        using var tree = new PatternTree();
        int source = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendRange(tree, source, 0x61, 0x63);
        ClassBuilder.AppendRange(tree, source, 0x70, 0x70);
        int plain = tree.NewNode(NodeKind.Class, 0);
        int negated = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendNodeClass(tree, plain, source, false);
        ClassBuilder.AppendNodeClass(tree, negated, source, true);

        Assert.Equal("61-63,70", ParseHelper.Ranges(tree.ClassRanges(plain)));
        Assert.Equal("0-60,64-6F,71-10FFFF", ParseHelper.Ranges(tree.ClassRanges(negated)));
    }

    [Fact]
    public static void MakeOwned_GivesAClassOfATableItsRanges()
    {
        using var tree = new PatternTree();
        int plain = tree.NewNode(NodeKind.Class, 0);
        int negated = tree.NewNode(NodeKind.Class, 0);
        tree.SetStaticClass(plain, StaticTables.Digit, false);
        tree.SetStaticClass(negated, StaticTables.Digit, true);
        Assert.Equal(0, tree.OwnedRanges);

        ClassBuilder.MakeOwned(tree, plain);
        ClassBuilder.MakeOwned(tree, negated);

        Assert.False(tree.IsStaticClass(plain));
        Assert.Equal("30-39", ParseHelper.Ranges(tree.ClassRanges(plain)));
        Assert.Equal("0-2F,3A-10FFFF", ParseHelper.Ranges(tree.ClassRanges(negated)));
        Assert.Equal(3, tree.OwnedRanges);
    }

    [Fact]
    public static void Contains_FindsTheCodePointsOfTheList()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendRange(tree, node, 0x10, 0x20);
        ClassBuilder.AppendRange(tree, node, 0x30, 0x30);
        ClassBuilder.AppendRange(tree, node, 0x50, 0x60);

        Assert.True(ClassBuilder.Contains(tree, node, 0x10));
        Assert.True(ClassBuilder.Contains(tree, node, 0x20));
        Assert.True(ClassBuilder.Contains(tree, node, 0x30));
        Assert.True(ClassBuilder.Contains(tree, node, 0x58));
        Assert.False(ClassBuilder.Contains(tree, node, 0x0F));
        Assert.False(ClassBuilder.Contains(tree, node, 0x21));
        Assert.False(ClassBuilder.Contains(tree, node, 0x31));
        Assert.False(ClassBuilder.Contains(tree, node, 0x61));
    }

    [Theory]
    [InlineData(0x4B, 0x4B)]
    [InlineData(0x6B, 0x4B)]
    [InlineData(0x212A, 0x4B)]
    [InlineData(0x31, 0x31)]
    [InlineData(0x1FBE, 0x345)]
    [InlineData(0x399, 0x345)]
    [InlineData(0x1E9E, 0xDF)]
    [InlineData(0x130, 0x130)]
    [InlineData(0x131, 0x131)]
    [InlineData(0x10FFFF, 0x10FFFF)]
    public static void MinFold_IsTheSmallestOfTheOrbit(int codePoint, int expected)
    {
        Assert.Equal(expected, ClassBuilder.MinFold(codePoint));
    }

    // A class of one code point is a literal, and so is a class of the two code points of one orbit of two.
    [Fact]
    public static void SinglePointAndFoldPair_AreRecognized()
    {
        Assert.True(ClassBuilder.IsSinglePoint([0x61, 0x61]));
        Assert.False(ClassBuilder.IsSinglePoint([0x61, 0x62]));
        Assert.False(ClassBuilder.IsSinglePoint([0x61, 0x61, 0x63, 0x63]));
        Assert.True(ClassBuilder.IsFoldPair([0x41, 0x41, 0x61, 0x61]));
        Assert.True(ClassBuilder.IsFoldPair([0xDF, 0xDF, 0x1E9E, 0x1E9E]));
        Assert.False(ClassBuilder.IsFoldPair([0x4B, 0x4B, 0x6B, 0x6B]));
        Assert.False(ClassBuilder.IsFoldPair([0x41, 0x42]));
        Assert.False(ClassBuilder.IsFoldPair([0x41, 0x41, 0x62, 0x62]));
    }

    // The ranges that classes own are counted for the tree while they are held, and not after the class is freed.
    [Fact]
    public static void OwnedRanges_AreCountedWhileTheyAreHeld()
    {
        using var tree = new PatternTree();
        int first = tree.NewNode(NodeKind.Class, 0);
        int second = tree.NewNode(NodeKind.Class, 0);
        ClassBuilder.AppendRange(tree, first, 1, 1);
        ClassBuilder.AppendRange(tree, first, 5, 5);
        ClassBuilder.AppendRange(tree, second, 9, 9);
        Assert.Equal(3, tree.OwnedRanges);

        tree.Free(first);

        Assert.Equal(1, tree.OwnedRanges);
    }

    // Past the ranges that a program may have, the tree stops storing them.
    [Fact]
    public static void OwnedRanges_PastTheSizeOfAProgram_StopBeingStored()
    {
        using var tree = new PatternTree();
        int node = tree.NewNode(NodeKind.Class, 0);
        for (int i = 0; i < PatternSize.MaxProgramSize; i++)
        {
            ClassBuilder.AppendRange(tree, node, 2 * i, 2 * i);
        }

        Assert.False(tree.RangesExceeded);
        Assert.Equal(PatternSize.MaxProgramSize, tree.OwnedRanges);

        ClassBuilder.AppendRange(tree, node, 1_000_001, 1_000_001);
        Assert.True(tree.RangesExceeded);

        int held = tree.DataCount(node);
        ClassBuilder.AppendRange(tree, node, 1_000_003, 1_000_003);
        Assert.Equal(held, tree.DataCount(node));
    }

    // A literal under (?i) and a class added to a class: the fold is of the literal's flag.
    [Fact]
    public static void AppendLiteral_FoldsWhenTheFlagIsSet()
    {
        using var tree = new PatternTree();
        int folded = tree.NewNode(NodeKind.Class, 0);
        int plain = tree.NewNode(NodeKind.Class, 0);

        ClassBuilder.AppendLiteral(tree, folded, 0x6B, PatternFlags.FoldCase);
        ClassBuilder.AppendLiteral(tree, plain, 0x6B, 0);

        Assert.Equal("4B,6B,212A", Cleaned(tree, folded));
        Assert.Equal("6B", Cleaned(tree, plain));
    }
}

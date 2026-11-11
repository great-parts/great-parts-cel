using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Library.Regex;
using Great.Parts.Cel.Library.Unicode;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The size m of the program of a pattern: the instructions of the program that Compile(Simplify(tree)) of Go gives, without
// captures, no-operations and jumps, and with the one that ends the program, plus the ranges that the classes of the pattern own.
// It is counted on the tree, before any repetition is expanded. The numbers of the first table were taken from the instructions
// of that program in Go 1.27.3, with the ranges of the classes added; the second table is where the size is by rule one less than
// the program of Go.
public sealed class PatternSizeTests
{
    [Theory]
    [InlineData("abc", 4)]
    [InlineData("^.*$", 5)]
    [InlineData("^$", 3)]
    [InlineData("\\b\\B", 3)]
    [InlineData("a{2,5}", 9)]
    [InlineData("a{0,5}", 11)]
    [InlineData("a{3,}", 5)]
    [InlineData("a{2,}", 4)]
    [InlineData("a{1,}", 3)]
    [InlineData("a{0,}", 3)]
    [InlineData("a{1}", 2)]
    [InlineData("x{0}", 1)]
    [InlineData("a*", 3)]
    [InlineData("a+", 3)]
    [InlineData("a?", 3)]
    [InlineData("a*?", 3)]
    [InlineData("a+?", 3)]
    [InlineData("(?U)a*", 3)]
    [InlineData("x*y*z*", 7)]
    [InlineData("(a)(b)", 3)]
    [InlineData("((a))*", 3)]
    [InlineData(".{3}", 4)]
    [InlineData("(?s).{3}", 4)]
    public static void Size_OfLiteralsAndRepetitions_IsTheProgramOfGo(string pattern, int size)
    {
        Assert.Equal(size, ParseHelper.SizeOf(pattern));
    }

    // Simplify: the repetition of a repetition of the same kind and the same greediness is one, a repetition of the empty match is
    // nothing, and x{0} does not look at x.
    [Theory]
    [InlineData("(?:a*)*", 3)]
    [InlineData("(?:a+)+", 3)]
    [InlineData("(?:a?)?", 3)]
    [InlineData("(?:a*?)*?", 3)]
    [InlineData("(?:a+?)+?", 3)]
    [InlineData("(?U)(?:a*)*", 3)]
    [InlineData("(?:a*)+", 4)]
    [InlineData("(?:a+)*", 4)]
    [InlineData("(?:a??)?", 4)]
    [InlineData("(?:a+?)+", 4)]
    [InlineData("(?:){5}", 1)]
    [InlineData("(?:)*", 1)]
    [InlineData("(?:a{0}){4}", 1)]
    [InlineData("(?:a{0})*", 1)]
    [InlineData("(?:a{2}){0}", 1)]
    public static void Size_CollapsesWhatSimplifyCollapses(string pattern, int size)
    {
        Assert.Equal(size, ParseHelper.SizeOf(pattern));
    }

    // Counted repeats: n copies of x and the nested (x(x(x)?)?)? of the m - n that may be absent, and x{n,} as n - 1 copies and x+.
    [Theory]
    [InlineData("(?:ab){2,3}", 8)]
    [InlineData("(?:a?){2}", 5)]
    [InlineData("(?:a?){0,3}", 9)]
    [InlineData("(?:a{0,2}){2}", 9)]
    [InlineData("(?:a{1,}){3,}", 7)]
    [InlineData("(?:a*){3}", 7)]
    [InlineData("(?:a*){0,1}", 4)]
    [InlineData("(?:a*){0,2}", 7)]
    [InlineData("(?:a{2}){3}", 7)]
    [InlineData("(?:|a){2}", 5)]
    [InlineData("(?:|a){0,2}", 7)]
    [InlineData("(a*){2}", 5)]
    [InlineData("x{2,3}y{0,1}", 7)]
    public static void Size_OfCountedRepeats_IsTheExpansionOfGo(string pattern, int size)
    {
        Assert.Equal(size, ParseHelper.SizeOf(pattern));
    }

    // An alternation adds a fork for each alternative but the first, and the tree is the factored one.
    [Theory]
    [InlineData("a|bc", 5)]
    [InlineData("ab|cd", 6)]
    [InlineData("ab|ac", 4)]
    [InlineData("a|b|c", 3)]
    [InlineData("abc|abd|aef", 8)]
    [InlineData("(a|b)*", 4)]
    [InlineData("(?:a|b){2}", 4)]
    public static void Size_OfAlternations_IsOfTheFactoredTree(string pattern, int size)
    {
        Assert.Equal(size, ParseHelper.SizeOf(pattern));
    }

    // The star of an expression that can match the empty string is one fork here and two in Go, so the size is one less than the
    // program of Go for each such star; no limit of Go is near enough for that to matter.
    [Theory]
    [InlineData("\\b*", 3)]
    [InlineData("(?:^)*", 3)]
    [InlineData("(?:|a)*", 4)]
    [InlineData("(?:a?)*", 4)]
    [InlineData("(a*)*", 4)]
    [InlineData("()*", 2)]
    [InlineData("(?:a*?)*", 4)]
    [InlineData("a\\b*b", 5)]
    public static void Size_OfAStarOverAnExpressionThatCanBeEmpty_IsOneFork(string pattern, int size)
    {
        Assert.Equal(size, ParseHelper.SizeOf(pattern));
    }

    // A class that is written in brackets owns its ranges, counted once for the node however often a repetition copies it; a
    // class that is written as one escape refers to a table, and is an instruction and no range; under (?i) it owns the ranges
    // after the fold.
    [Theory]
    [InlineData("[a-z]", 3)]
    [InlineData("[a-z]{3}", 5)]
    [InlineData("[a-z]{1000}", 1002)]
    [InlineData("[a-cx]", 4)]
    [InlineData("[^a]", 4)]
    [InlineData("[\\d_]", 4)]
    [InlineData("[\\w]", 6)]
    [InlineData("[a]", 2)]
    [InlineData("\\d{3}", 4)]
    [InlineData("\\d{1000}", 1001)]
    [InlineData("\\pL", 2)]
    [InlineData("\\pL{50}", 51)]
    [InlineData("\\w", 2)]
    [InlineData("\\D", 2)]
    [InlineData("(?i)\\w", 8)]
    [InlineData("(?i)\\d", 3)]
    [InlineData("(?i)a", 2)]
    [InlineData("(?i)[a-c]", 4)]
    [InlineData("[a-c]x[m-p]", 6)]
    [InlineData("[a-c]|[e-g]|x", 5)]
    [InlineData("a|[b-d]", 3)]
    public static void Size_OfClasses_CountsTheRangesTheyOwn(string pattern, int size)
    {
        Assert.Equal(size, ParseHelper.SizeOf(pattern));
    }

    // Under (?i) a class of a table owns the ranges of the table merged with its fold table, a complement taken after the merge; a
    // table without a fold table stays a reference, as it is outside (?i).
    [Theory]
    [InlineData("L", "(?i)\\pL", false)]
    [InlineData("Greek", "(?i)\\p{Greek}", false)]
    [InlineData("Lu", "(?i)\\p{Lu}", false)]
    [InlineData("Greek", "(?i)\\P{Greek}", true)]
    [InlineData("Greek", "(?i)\\p{^Greek}", true)]
    public static void Size_OfATableUnderFoldCase_IsTheMergeOfTheTableAndItsFoldTable(string name, string pattern, bool complement)
    {
        Assert.True(UnicodeTables.TryFindTable(name, out TableName table));
        Assert.True(table.FoldTable >= 0);
        int merged = MergedRanges(UnicodeTables.Ranges(table.Table), UnicodeTables.Ranges(table.FoldTable), complement);

        using PatternTree tree = ParseHelper.Parse(pattern);

        Assert.True(merged > 1);
        Assert.Equal(merged, tree.OwnedRanges);
        Assert.Equal(merged + 2, tree.Size);
    }

    [Theory]
    [InlineData("(?i)\\p{Lc}")]
    [InlineData("(?i)\\P{Lc}")]
    [InlineData("\\p{Greek}")]
    public static void Size_OfATableWithoutFoldTableOrFoldCase_IsAReference(string pattern)
    {
        Assert.Equal(2, ParseHelper.SizeOf(pattern));
    }

    // The ranges of the union of two sorted range lists, touching ranges joined; a complement is taken after the union.
    private static int MergedRanges(ReadOnlySpan<int> table, ReadOnlySpan<int> fold, bool complement)
    {
        var all = new List<(int Lo, int Hi)>();
        for (int i = 0; i < table.Length; i += 2)
        {
            all.Add((table[i], table[i + 1]));
        }

        for (int i = 0; i < fold.Length; i += 2)
        {
            all.Add((fold[i], fold[i + 1]));
        }

        all.Sort();
        var union = new List<(int Lo, int Hi)>();
        foreach ((int lo, int hi) in all)
        {
            if (union.Count > 0 && lo <= union[^1].Hi + 1)
            {
                union[^1] = (union[^1].Lo, Math.Max(union[^1].Hi, hi));
            }
            else
            {
                union.Add((lo, hi));
            }
        }

        if (!complement)
        {
            return union.Count;
        }

        int count = 0;
        int next = 0;
        foreach ((int lo, int hi) in union)
        {
            if (lo > next)
            {
                count++;
            }

            next = hi + 1;
        }

        return next <= 0x10FFFF ? count + 1 : count;
    }

    // A class of a table inside brackets owns the ranges of the table: [\pL_] is the instruction, the match and the ranges.
    [Fact]
    public static void Size_OfBracketClassWithATable_IsItsRangesPlusTwo()
    {
        using PatternTree tree = ParseHelper.Parse("[\\pL_]");

        int ranges = tree.DataCount(tree.Root) / 2;
        Assert.True(ranges > 600);
        Assert.Equal(ranges, tree.OwnedRanges);
        Assert.Equal(ranges + 2, tree.Size);
    }

    // The ranges that the classes own are counted live: the classes that are merged into another, folded away or turned into a
    // character are not counted any more. The count is the ranges of the classes that are in the finished tree.
    [Theory]
    [InlineData("[a-c]|[e-g]|x")]
    [InlineData("[a-c]x|[a-c]y|[a-c]z")]
    [InlineData("(?i)[a-z]+[\\pL_]|k")]
    [InlineData("[^\\n]|\\n")]
    [InlineData("(?s:.)|[\\x00-\\x{10FFFF}]")]
    [InlineData("[ab]{2}[ab]{3}")]
    [InlineData("a|b|(?:c|d)|(?:e|f)g|\\pL")]
    [InlineData("[A-Za-z]+@[A-Za-z]+\\.[a-z]{2,4}|\\d{3}-\\d{4}")]
    [InlineData("(?i)ß|k|[Aa]|\\x{212A}|\\x{17F}x|[[:alpha:]]")]
    [InlineData("[k]|[Kk]|[a-z]|[A-Z]")]
    public static void OwnedRanges_AreTheRangesOfTheClassesOfTheFinishedTree(string pattern)
    {
        using PatternTree tree = ParseHelper.Parse(pattern);

        Assert.Equal(CountOwnedRanges(tree), tree.OwnedRanges);
        Assert.Equal(tree.Instructions(tree.Root) + 1 + tree.OwnedRanges, tree.Size);
    }

    private static int CountOwnedRanges(PatternTree tree)
    {
        int total = 0;
        var stack = new Stack<int>();
        stack.Push(tree.Root);
        while (stack.Count > 0)
        {
            int node = stack.Pop();
            if (tree.Kind(node) == NodeKind.Class && !tree.IsStaticClass(node))
            {
                total += tree.DataCount(node) / 2;
            }

            for (int child = tree.FirstChild(node); child != PatternTree.Null; child = tree.NextSibling(child))
            {
                stack.Push(child);
            }
        }

        return total;
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    // m is 100 000 in each of these and they are accepted: the instruction count of each is the number of units it gives.
    [Fact]
    public static void Size_100000_IsAccepted()
    {
        Assert.Equal(100_000, ParseHelper.SizeOf(Repeat("b{1000}", 99) + "b{999}"));
        Assert.Equal(100_000, ParseHelper.SizeOf(Repeat("\\w{1000}", 99) + "\\w{999}"));
        Assert.Equal(100_000, ParseHelper.SizeOf(Repeat("[b-y]{1000}", 99) + "b{900}"));
        Assert.Equal(100_000, ParseHelper.SizeOf(Repeat("b{0,1000}", 49) + "b{0,999}" + "b"));
    }

    // One more is refused, with the reason "expression too large" and the offset 0.
    [Fact]
    public static void Size_100001_IsRefused()
    {
        ParseHelper.AssertRefused(Repeat("b{1000}", 100), 0, RegexError.TooLarge);
        ParseHelper.AssertRefused(Repeat("\\w{1000}", 100), 0, RegexError.TooLarge);
        ParseHelper.AssertRefused(Repeat("[b-y]{1000}", 99) + "b{901}", 0, RegexError.TooLarge);
        ParseHelper.AssertRefused(Repeat("b{0,1000}", 50), 0, RegexError.TooLarge);
    }

    // Patterns of an enormous expansion are refused by the count of their repeats before they are expanded, and the work of the
    // refusal does not depend on the expansion: nothing here is expanded.
    [Fact]
    public static void Size_OfWhatWouldExpandToBillions_IsNeverExpanded()
    {
        ParseHelper.AssertRefused("(?:(?:a{1000}){1000}){1000}", 14, RegexError.InvalidRepeatCount);
        ParseHelper.AssertRefused(Repeat("(?:", 50) + "a{1000}" + Repeat("){1000}", 50), 158, RegexError.InvalidRepeatCount);
        ParseHelper.AssertRefused(Repeat("a{1000}", 100_000), 0, RegexError.TooLarge);
    }

    // The size is known when the text is read to its end, so an error of syntax anywhere in the text wins over the size.
    [Fact]
    public static void SyntaxErrorAfterAnOversizedPattern_IsTheSyntaxError()
    {
        string oversized = Repeat("b{1000}", 101);

        ParseHelper.AssertRefused(oversized + "(", 0, RegexError.MissingParen);
        ParseHelper.AssertRefused(oversized + "\\y", oversized.Length, RegexError.InvalidEscape);
        ParseHelper.AssertRefused(oversized + "a**", oversized.Length + 1, RegexError.InvalidRepeatOperator);
        ParseHelper.AssertRefused(oversized + "b{1001}", oversized.Length + 1, RegexError.InvalidRepeatCount);
    }

    // The classes of a program own at most the ranges that the size allows. The parser stops storing ranges past that point and
    // reads on only for an error of syntax, so a pattern of many big classes costs memory in proportion to its size.
    [Fact]
    public static void ClassesThatOwnTooManyRanges_AreTooLarge()
    {
        int ranges;
        using (PatternTree one = ParseHelper.Parse("[\\pL_]"))
        {
            ranges = one.OwnedRanges;
        }

        int fits = (PatternSize.MaxProgramSize - 1) / (ranges + 1);
        using (PatternTree tree = ParseHelper.Parse(Repeat("[\\pL_]", fits)))
        {
            Assert.Equal((fits * (ranges + 1)) + 1, tree.Size);
        }

        ParseHelper.AssertRefused(Repeat("[\\pL_]", fits + 1), 0, RegexError.TooLarge);
        ParseHelper.AssertRefused(Repeat("[\\pL_]", 2000), 0, RegexError.TooLarge);
        ParseHelper.AssertRefused(Repeat("[\\pL_]", 2000) + "(", 0, RegexError.MissingParen);
        ParseHelper.AssertRefused(Repeat("[\\pL_]", 2000) + "[a", 6 * 2000, RegexError.MissingBracket);
    }

    // Many items in one class are one list: the duplicates are merged and the list is the table once.
    [Fact]
    public static void ClassWithTheSameTableManyTimes_IsTheTableOnce()
    {
        using PatternTree one = ParseHelper.Parse("[\\pL]");
        using PatternTree many = ParseHelper.Parse("[" + Repeat("\\pL", 3000) + "]");

        Assert.Equal(one.OwnedRanges, many.OwnedRanges);
        Assert.Equal(one.Size, many.Size);
    }

    [Fact]
    public static void ClassWithManyFoldedRanges_IsTheFoldOnce()
    {
        using PatternTree one = ParseHelper.Parse("(?i)[\\x{100}-\\x{1E943}]");
        using PatternTree many = ParseHelper.Parse("(?i)[" + Repeat("\\x{100}-\\x{1E943}", 500) + "]");

        Assert.Equal(one.OwnedRanges, many.OwnedRanges);
    }

    // The values of the first and the second rule of the table of the size, directly.
    [Theory]
    [InlineData((int)NodeKind.Star, false, 1, (int)NodeKind.Literal, 2, (int)NodeKind.Star)]
    [InlineData((int)NodeKind.Star, false, 2, (int)NodeKind.Star, 2, (int)NodeKind.Star)]
    [InlineData((int)NodeKind.Star, false, 2, (int)NodeKind.Star | PatternSize.LazyBit, 3, (int)NodeKind.Star)]
    [InlineData((int)NodeKind.Plus, true, 1, (int)NodeKind.Literal, 2, (int)NodeKind.Plus | PatternSize.LazyBit)]
    [InlineData((int)NodeKind.Quest, false, 0, (int)NodeKind.EmptyMatch, 0, (int)NodeKind.EmptyMatch)]
    public static void Unary_IsTheRuleOfSimplify1(int op, bool lazy, int childInstructions, int childKind, int instructions, int kind)
    {
        PatternSize.Unary((NodeKind)op, lazy, childInstructions, childKind, out int actualInstructions, out int actualKind);

        Assert.Equal(instructions, actualInstructions);
        Assert.Equal(kind, actualKind);
    }

    [Theory]
    [InlineData(2, 5, 1, 8, (int)NodeKind.Concat)]
    [InlineData(0, 3, 1, 6, (int)NodeKind.Quest)]
    [InlineData(0, 1, 1, 2, (int)NodeKind.Quest)]
    [InlineData(0, 0, 7, 0, (int)NodeKind.EmptyMatch)]
    [InlineData(1, 1, 7, 7, (int)NodeKind.Literal)]
    [InlineData(3, -1, 1, 4, (int)NodeKind.Concat)]
    [InlineData(1, -1, 1, 2, (int)NodeKind.Plus)]
    [InlineData(0, -1, 1, 2, (int)NodeKind.Star)]
    [InlineData(1000, 1000, 1000, 1_000_000, (int)NodeKind.Concat)]
    public static void Repeat_IsTheRuleOfSimplify(int min, int max, int childInstructions, int instructions, int kind)
    {
        PatternSize.Repeat(min, max, false, childInstructions, (int)NodeKind.Literal, out int actualInstructions, out int actualKind);

        Assert.Equal(instructions, actualInstructions);
        Assert.Equal(kind, actualKind);
    }

    // The sizes saturate, so that nested repeats of 1000 cannot overflow.
    [Fact]
    public static void Sizes_Saturate()
    {
        PatternSize.Repeat(1000, 1000, false, PatternSize.Saturation, (int)NodeKind.Literal, out int instructions, out _);

        Assert.Equal(PatternSize.Saturation, instructions);
        Assert.Equal(PatternSize.Saturation, PatternSize.Add(PatternSize.Saturation, PatternSize.Saturation));
    }
}

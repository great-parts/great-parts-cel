using System.Linq;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The parser against regexp/syntax of Go 1.27.3 with the flags syntax.Perl. The trees of the first table, and the offsets and
// reasons of the second, were taken from that package (the tree as it comes out of the parse, before Simplify); a tree is written
// as in ParseHelper: a literal as lit:text, a class as class[lo-hi,...], the other nodes by name with their children in brackets,
// "~" after a literal or class that folds case and "?" after a node that is non-greedy. A class that refers to a table is
// written as table:name here, and is tested apart.
public sealed class PatternParserTests
{
    [Theory]
    [InlineData("abc", "lit:abc")]
    [InlineData("a\\.b", "lit:a.b")]
    [InlineData("\\x41\\x{1F600}\\101", "lit:A\\x{1F600}A")]
    [InlineData("\\Qa.b\\E", "lit:a.b")]
    [InlineData("\\t\\n\\r\\f\\a\\v", "lit:\\x{9}\\x{A}\\x{D}\\x{C}\\x{7}\\x{B}")]
    [InlineData("é😀", "lit:\\x{E9}\\x{1F600}")]
    [InlineData("a|b", "class[61-62]")]
    [InlineData("ab|cd", "Alternate(lit:ablit:cd)")]
    [InlineData("ab|ac", "Concat(lit:aclass[62-63])")]
    [InlineData("abc|abd|aef", "Concat(lit:aAlternate(Concat(lit:bclass[63-64])lit:ef))")]
    [InlineData("a|", "Alternate(lit:aEmptyMatch)")]
    [InlineData("", "EmptyMatch")]
    [InlineData("|a", "Alternate(EmptyMatchlit:a)")]
    [InlineData("a||b", "Alternate(lit:aEmptyMatchlit:b)")]
    [InlineData("a*", "Star(lit:a)")]
    [InlineData("a+?", "Plus?(lit:a)")]
    [InlineData("a??", "Quest?(lit:a)")]
    [InlineData("a{2}", "repeat{2,2}(lit:a)")]
    [InlineData("a{2,}", "repeat{2,-1}(lit:a)")]
    [InlineData("a{2,3}?", "repeat?{2,3}(lit:a)")]
    [InlineData("(?:ab)*", "Star(lit:ab)")]
    [InlineData("ab*", "Concat(lit:aStar(lit:b))")]
    [InlineData("a{0}", "repeat{0,0}(lit:a)")]
    [InlineData("(a)", "Capture(lit:a)")]
    [InlineData("(?:a)", "lit:a")]
    [InlineData("(?P<n>a)", "Capture(lit:a)")]
    [InlineData("(?<n>a)", "Capture(lit:a)")]
    [InlineData("((a))", "Capture(Capture(lit:a))")]
    [InlineData("()", "Capture(EmptyMatch)")]
    [InlineData("(?i)a", "lit~:A")]
    [InlineData("(?i:a)b", "Concat(lit~:Alit:b)")]
    [InlineData("(?i)a(?-i)b", "Concat(lit~:Alit:b)")]
    [InlineData("(?s).", "AnyChar")]
    [InlineData(".", "AnyCharNotNewline")]
    [InlineData("(?m)^$", "Concat(BeginLineEndLine)")]
    [InlineData("^$", "Concat(BeginTextEndText)")]
    [InlineData("(?U)a*", "Star?(lit:a)")]
    [InlineData("(?i-s)a", "lit~:A")]
    [InlineData("\\A\\z\\b\\B", "Concat(BeginTextEndTextWordBoundaryNoWordBoundary)")]
    [InlineData("[abc]", "class[61-63]")]
    [InlineData("[a-c]", "class[61-63]")]
    [InlineData("[^a]", "class[0-60,62-10FFFF]")]
    [InlineData("[a-]", "class[2D,61]")]
    [InlineData("[]a]", "class[5D,61]")]
    [InlineData("[^]a]", "class[0-5C,5E-60,62-10FFFF]")]
    [InlineData("[\\]]", "lit:]")]
    [InlineData("[[:alpha:]]", "class[41-5A,61-7A]")]
    [InlineData("[[:^alpha:]]", "class[0-40,5B-60,7B-10FFFF]")]
    [InlineData("[\\d_]", "class[30-39,5F]")]
    [InlineData("[a-c-e]", "class[2D,61-63,65]")]
    [InlineData("[a]", "lit:a")]
    [InlineData("[Aa]", "lit~:A")]
    [InlineData("(?i)[a]", "lit~:A")]
    [InlineData("(?i)[k]", "class~[4B,6B,212A]")]
    [InlineData("[^\\x00-\\x{10FFFF}]", "class[]")]
    [InlineData("a|b|c|d", "class[61-64]")]
    [InlineData("[a-c]|x|[m-p]", "class[61-63,6D-70,78]")]
    [InlineData(".|\\n", "AnyChar")]
    [InlineData("[^\\n]|\\n", "AnyChar")]
    [InlineData("(?s:.)|x", "AnyChar")]
    [InlineData("(?i)ß", "lit~:\\x{DF}")]
    [InlineData("(?i)s|k", "class~[4B,53,6B,73,17F,212A]")]
    [InlineData("a(?i)b", "Concat(lit:alit~:B)")]
    [InlineData("x{1}", "repeat{1,1}(lit:x)")]
    [InlineData("(?:a|b)c", "Concat(class[61-62]lit:c)")]
    [InlineData("(a|b)c", "Concat(Capture(class[61-62])lit:c)")]
    [InlineData("abx|aby|ab", "Concat(lit:abAlternate(class[78-79]EmptyMatch))")]
    [InlineData("[ab]c|[ab]d|e", "Alternate(Concat(class[61-62]class[63-64])lit:e)")]
    [InlineData("a{2}b|a{2}c", "Concat(repeat{2,2}(lit:a)class[62-63])")]
    [InlineData("(?i)ab|(?i)ac", "Concat(lit~:Aclass~[42-43,62-63])")]
    [InlineData("(?i)\\w", "class~[30-39,41-5A,5F,61-7A,17F,212A]")]
    [InlineData("\\Q\\E", "EmptyMatch")]
    [InlineData("(?i)[^k]", "class~[0-4A,4C-6A,6C-2129,212B-10FFFF]")]
    [InlineData("[[:word:]]", "class[30-39,41-5A,5F,61-7A]")]
    [InlineData("[\\w-z]", "class[2D,30-39,41-5A,5F,61-7A]")]
    [InlineData("[a-\\x{10FFFF}]", "class[61-10FFFF]")]
    [InlineData("a\\Q*\\Eb", "lit:a*b")]
    [InlineData("\\x{41}\\x4a", "lit:AJ")]
    [InlineData("\\0\\12\\012\\1234", "lit:\\x{0}\\x{A}\\x{A}S4")]
    public static void Pattern_BuildsTheTreeThatGoBuilds(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    // The offset is that of the fragment that Go names in its error, in UTF-16 code units; an error about the whole pattern is at 0.
    [Theory]
    [InlineData("(", 0, "missing closing )")]
    [InlineData("a(b", 0, "missing closing )")]
    [InlineData(")", 0, "unexpected )")]
    [InlineData("a)b", 0, "unexpected )")]
    [InlineData("x(a)b)", 0, "unexpected )")]
    [InlineData("[a", 0, "missing closing ]")]
    [InlineData("ab[a-", 2, "missing closing ]")]
    [InlineData("[^", 0, "missing closing ]")]
    [InlineData("a\\", 1, "trailing backslash at end of expression")]
    [InlineData("(?:a\\", 4, "trailing backslash at end of expression")]
    [InlineData("\\y", 0, "invalid escape sequence")]
    [InlineData("ab\\yc", 2, "invalid escape sequence")]
    [InlineData("\\x4", 0, "invalid escape sequence")]
    [InlineData("\\x{110000}", 0, "invalid escape sequence")]
    [InlineData("\\x{}", 0, "invalid escape sequence")]
    [InlineData("\\x{4", 0, "invalid escape sequence")]
    [InlineData("\\xg1", 0, "invalid escape sequence")]
    [InlineData("[\\y]", 1, "invalid escape sequence")]
    [InlineData("\\8", 0, "invalid escape sequence")]
    [InlineData("[a-\\d]", 3, "invalid escape sequence")]
    [InlineData("\\C", 0, "invalid escape sequence")]
    [InlineData("a\\Cb", 1, "invalid escape sequence")]
    [InlineData("\\Z", 0, "invalid escape sequence")]
    [InlineData("\\1", 0, "invalid escape sequence")]
    [InlineData("(a)\\1", 3, "invalid escape sequence")]
    [InlineData("[\\Q]", 1, "invalid escape sequence")]
    [InlineData("[z-a]", 1, "invalid character class range")]
    [InlineData("a[b-a]", 2, "invalid character class range")]
    [InlineData("[[:foo:]]", 1, "invalid character class range")]
    [InlineData("[a-[:alpha:]]", 1, "invalid character class range")]
    [InlineData("\\p{Foo}", 0, "invalid character class range")]
    [InlineData("a\\pX", 1, "invalid character class range")]
    [InlineData("\\p{L", 0, "invalid character class range")]
    [InlineData("x\\p{L", 1, "invalid character class range")]
    [InlineData("\\p", 0, "invalid character class range")]
    [InlineData("\\p{}", 0, "invalid character class range")]
    [InlineData("\\p{Grek}", 0, "invalid character class range")]
    [InlineData("(?P<n", 0, "invalid named capture")]
    [InlineData("(?P<>a)", 0, "invalid named capture")]
    [InlineData("a(?P<n!>b)", 1, "invalid named capture")]
    [InlineData("(?<n", 0, "invalid named capture")]
    [InlineData("(?<=a)", 0, "invalid named capture")]
    [InlineData("(?<!a)", 0, "invalid named capture")]
    [InlineData("(?=a)", 0, "unsupported Perl syntax: (?=")]
    [InlineData("(?!a)", 0, "unsupported Perl syntax: (?!")]
    [InlineData("(?>a)", 0, "unsupported Perl syntax: (?>")]
    [InlineData("a(?#c)", 1, "unsupported Perl syntax: (?#")]
    [InlineData("(?i", 0, "unsupported Perl syntax: (?i")]
    [InlineData("(?z)", 0, "unsupported Perl syntax: (?z")]
    [InlineData("(?-)", 0, "unsupported Perl syntax: (?-)")]
    [InlineData("(?i-)", 0, "unsupported Perl syntax: (?i-)")]
    [InlineData("(?", 0, "unsupported Perl syntax: (?")]
    [InlineData("(?P=n)", 0, "unsupported Perl syntax: (?P")]
    [InlineData("(?P>n)", 0, "unsupported Perl syntax: (?P")]
    [InlineData("(?|a|b)", 0, "unsupported Perl syntax: (?|")]
    [InlineData("*", 0, "missing argument to repetition operator")]
    [InlineData("a|*", 2, "missing argument to repetition operator")]
    [InlineData("(*)", 1, "missing argument to repetition operator")]
    [InlineData("+a", 0, "missing argument to repetition operator")]
    [InlineData("(?i)*", 4, "missing argument to repetition operator")]
    [InlineData("(?i)(*)", 5, "missing argument to repetition operator")]
    [InlineData("{1}", 0, "missing argument to repetition operator")]
    [InlineData("a|{1}", 2, "missing argument to repetition operator")]
    [InlineData("a**", 1, "invalid nested repetition operator")]
    [InlineData("a*+", 1, "invalid nested repetition operator")]
    [InlineData("a{2}{3}", 1, "invalid nested repetition operator")]
    [InlineData("a?*", 1, "invalid nested repetition operator")]
    [InlineData("a*??", 1, "invalid nested repetition operator")]
    [InlineData("a+?+", 1, "invalid nested repetition operator")]
    [InlineData("x{2}*", 1, "invalid nested repetition operator")]
    [InlineData("\\pN{2}*", 3, "invalid nested repetition operator")]
    [InlineData("a{1,2}{3}", 1, "invalid nested repetition operator")]
    [InlineData("a{1001}", 1, "invalid repeat count")]
    [InlineData("a{2,1}", 1, "invalid repeat count")]
    [InlineData("a{100000000000}", 1, "invalid repeat count")]
    [InlineData("(?:a{1000}){2}", 11, "invalid repeat count")]
    [InlineData("(?:a{10}){101}", 9, "invalid repeat count")]
    public static void Pattern_IsRefusedAtTheOffsetWithTheReason(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The constructs that the syntax page of RE2 lists as not supported and that Go reads as text.
    [Theory]
    [InlineData("x{}", "lit:x{}")]
    [InlineData("x{-}", "lit:x{-}")]
    [InlineData("x=", "lit:x=")]
    [InlineData("%(x)", "Concat(lit:%Capture(lit:x))")]
    [InlineData("\\zs", "Concat(EndTextlit:s)")]
    [InlineData("a{,2}", "lit:a{,2}")]
    [InlineData("a{1,2", "lit:a{1,2")]
    [InlineData("a{x}", "lit:a{x}")]
    [InlineData("a{01}", "lit:a{01}")]
    [InlineData("{", "lit:{")]
    [InlineData("}", "lit:}")]
    [InlineData("]", "lit:]")]
    [InlineData("a]", "lit:a]")]
    [InlineData("\\_", "lit:_")]
    [InlineData("(?i-i)", "EmptyMatch")]
    public static void FormsThatGoReadsAsText_AreNotErrors(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    // \d \s \w, \p and \P refer to a table when they stand alone, and the tree shows which, and whether it is the complement.
    [Theory]
    [InlineData("\\d", "table:digit")]
    [InlineData("\\D", "table!:digit")]
    [InlineData("\\s", "table:space")]
    [InlineData("\\S", "table!:space")]
    [InlineData("\\w", "table:word")]
    [InlineData("\\W", "table!:word")]
    [InlineData("\\P{Any}", "table!:any")]
    [InlineData("\\p{Any}", "AnyChar")]
    [InlineData("(?i)\\p{Any}", "AnyChar")]
    [InlineData("\\p{Any}x", "Concat(table:anylit:x)")]
    [InlineData("\\p{Ascii}", "table:ascii")]
    [InlineData("\\p{^Ascii}", "table!:ascii")]
    [InlineData("\\d+", "Plus(table:digit)")]
    public static void ClassWrittenAsOneEscape_RefersToItsTable(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    // Under (?i) a class that has more than its table, because the fold of its ranges adds code points, owns the folded ranges.
    [Theory]
    [InlineData("(?i)\\d", "class~[30-39]")]
    [InlineData("(?i)\\D", "class~[0-2F,3A-10FFFF]")]
    [InlineData("(?i)\\s", "class~[9-A,C-D,20]")]
    [InlineData("(?i)\\p{Ascii}", "class~[0-7F,17F,212A]")]
    [InlineData("(?i)\\W", "class~[0-2F,3A-40,5B-5E,60,7B-17E,180-2129,212B-10FFFF]")]
    public static void ClassUnderFoldCase_OwnsTheFoldedRanges(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    [Theory]
    [InlineData("\\pL", 'a', true)]
    [InlineData("\\pL", 0xE9, true)]
    [InlineData("\\pL", '1', false)]
    [InlineData("\\PL", 'a', false)]
    [InlineData("\\PL", '1', true)]
    [InlineData("\\p{^L}", 'a', false)]
    [InlineData("\\P{^L}", 'a', true)]
    [InlineData("\\pN", 0x663, true)]
    [InlineData("\\p{Greek}", 0x3B1, true)]
    [InlineData("\\p{Greek}", 'a', false)]
    [InlineData("\\p{Assigned}", 'a', true)]
    [InlineData("\\p{Assigned}", 0x378, false)]
    [InlineData("\\p{Cn}", 0x378, true)]
    [InlineData("\\p{Lc}", 'a', true)]
    [InlineData("\\p{Lc}", 0x1C5, true)]
    [InlineData("\\p{Lc}", '1', false)]
    [InlineData("\\d", 0x663, false)]
    [InlineData("\\d", '7', true)]
    [InlineData("\\w", 0xE9, false)]
    [InlineData("\\w", '_', true)]
    [InlineData("\\s", 0xA0, false)]
    [InlineData("\\s", 0x0B, false)]
    [InlineData("\\s", 0x0C, true)]
    [InlineData("[[:space:]]", 0x0B, true)]
    [InlineData("[[:alpha:]]", 0xE9, false)]
    [InlineData("[^a]", '\n', true)]
    public static void ClassHoldsTheCodePoints_AsInGo(string pattern, int codePoint, bool holds)
    {
        Assert.Equal(holds, ParseHelper.ClassHolds(pattern, codePoint));
    }

    // Under (?i) the table of a name and the code points that fold into it; the fold tables are those of Go, so L has U+0345
    // and the cased letters LC have no fold table.
    [Theory]
    [InlineData("(?i)\\p{L}", 0x345, true)]
    [InlineData("(?i)\\p{Lu}", 'a', true)]
    [InlineData("(?i)\\p{Lu}", 0x17F, true)]
    [InlineData("(?i)\\p{Lu}", '1', false)]
    [InlineData("(?i)\\P{Lu}", 'a', false)]
    [InlineData("(?i)\\P{Lu}", '1', true)]
    [InlineData("(?i)\\p{Lc}", 0x345, false)]
    [InlineData("(?i)\\p{Greek}", 0xB5, true)]
    [InlineData("(?i)\\w", 0x17F, true)]
    [InlineData("(?i)\\w", 0x212A, true)]
    [InlineData("(?i)\\W", 0x17F, false)]
    [InlineData("(?i)\\W", 0x212A, false)]
    [InlineData("(?i)\\W", '-', true)]
    [InlineData("(?i)[[:alpha:]]", 0x17F, true)]
    [InlineData("(?i)[[:^alpha:]]", 0x17F, false)]
    [InlineData("(?i)[a-z]", 0x212A, true)]
    [InlineData("(?i)[a-z]", 0x130, false)]
    [InlineData("(?i)[a-z]", 0x131, false)]
    [InlineData("(?i)[\\x{DF}x]", 0x1E9E, true)]
    [InlineData("(?i)[k-l]", 0x212A, true)]
    [InlineData("(?i)[\\x{345}]", 0x399, true)]
    [InlineData("(?i)[\\x{345}]", 0x1FBE, true)]
    public static void ClassUnderFoldCase_HoldsTheOrbitsOfTheFold(string pattern, int codePoint, bool holds)
    {
        Assert.Equal(holds, ParseHelper.ClassHolds(pattern, codePoint));
    }

    // A literal under (?i) is the smallest code point of its orbit; the literal keeps the flag, whether or not the orbit has more.
    [Theory]
    [InlineData("(?i)k", "lit~:K")]
    [InlineData("(?i)\\x{212A}", "lit~:K")]
    [InlineData("(?i)i", "lit~:I")]
    [InlineData("(?i)1", "lit~:1")]
    [InlineData("(?i)\\x{345}", "lit~:\\x{345}")]
    [InlineData("(?i)\\x{1FBE}", "lit~:\\x{345}")]
    [InlineData("(?i)\\x{1E9E}", "lit~:\\x{DF}")]
    [InlineData("(?i)\\x{131}", "lit~:\\x{131}")]
    [InlineData("(?i)\\x{130}", "lit~:\\x{130}")]
    public static void LiteralUnderFoldCase_IsTheSmallestOfItsOrbit(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    // The names of \p are those that unicodeTable of Go resolves, in any form that canonicalName makes the same.
    [Theory]
    [InlineData("\\p{greek}", "\\p{Greek}")]
    [InlineData("\\p{GREEK}", "\\p{Greek}")]
    [InlineData("\\p{Old_Italic}", "\\p{OldItalic}")]
    [InlineData("\\p{old italic}", "\\p{Old_Italic}")]
    [InlineData("\\p{old-italic}", "\\p{Old_Italic}")]
    [InlineData("\\p{L_u}", "\\p{Lu}")]
    [InlineData("\\p{lu}", "\\p{Lu}")]
    [InlineData("\\pl", "\\pL")]
    [InlineData("\\p{Letter}", "\\p{L}")]
    [InlineData("\\p{Lowercase_Letter}", "\\p{Ll}")]
    [InlineData("\\p{Cased_Letter}", "\\p{LC}")]
    [InlineData("\\p{Lc}", "\\p{LC}")]
    [InlineData("\\p{^Greek}", "\\P{Greek}")]
    [InlineData("\\P{^Greek}", "\\p{Greek}")]
    [InlineData("\\p{Assigned}", "\\P{Cn}")]
    [InlineData("\\P{Assigned}", "\\p{Cn}")]
    [InlineData("\\p{Combining_Mark}", "\\p{M}")]
    [InlineData("\\p{Punctuation}", "\\p{P}")]
    public static void NameOfAClass_IsResolvedAsGoResolvesIt(string spelled, string canonical)
    {
        Assert.Equal(ParseHelper.Tree(canonical), ParseHelper.Tree(spelled));
    }

    [Theory]
    [InlineData("\\p{Grek}", 0)]
    [InlineData("\\p{Foo}", 0)]
    [InlineData("\\p{}", 0)]
    [InlineData("\\p{^}", 0)]
    [InlineData("\\p{Script=Greek}", 0)]
    [InlineData("\\p{IsGreek}", 0)]
    [InlineData("\\p{L&}", 0)]
    [InlineData("\\pX", 0)]
    [InlineData("x\\p{Latn}", 1)]
    [InlineData("[\\p{Foo}]", 1)]
    [InlineData("[a\\pX]", 2)]
    public static void UnknownName_IsAnInvalidClassRangeAtTheEscape(string pattern, int offset)
    {
        ParseHelper.AssertRefused(pattern, offset, RegexError.InvalidCharRange);
    }

    // A name of more characters than any table has can not be one, so it is unknown and costs no more than its length.
    [Fact]
    public static void NameLongerThanAnyTable_IsUnknown()
    {
        ParseHelper.AssertRefused("\\p{" + new string('a', 100) + "}", 0, RegexError.InvalidCharRange);
        ParseHelper.AssertRefused("\\p{" + string.Concat(Enumerable.Repeat("_-", 5000)) + "Foo}", 0, RegexError.InvalidCharRange);
    }

    [Fact]
    public static void NameWithManySeparators_IsStillTheName()
    {
        Assert.Equal(ParseHelper.Tree("\\p{Greek}"), ParseHelper.Tree("\\p{" + string.Concat(Enumerable.Repeat("_-", 5000)) + "greek}"));
    }

    // The anchors without and with (?m), and the dot without and with (?s).
    [Theory]
    [InlineData("^$", "Concat(BeginTextEndText)")]
    [InlineData("(?m)^$", "Concat(BeginLineEndLine)")]
    [InlineData("(?m)(?-m)^", "BeginText")]
    [InlineData("(?m:^)^", "Concat(BeginLineBeginText)")]
    [InlineData(".", "AnyCharNotNewline")]
    [InlineData("(?s).", "AnyChar")]
    [InlineData("(?s)(?-s).", "AnyCharNotNewline")]
    [InlineData("(?s:.).", "Concat(AnyCharAnyCharNotNewline)")]
    [InlineData("(?U)^", "BeginText?")]
    [InlineData("(?U:a*)a*", "Concat(Star?(lit:a)Star(lit:a))")]
    [InlineData("(?U)a*?", "Star(lit:a)")]
    [InlineData("a*?", "Star?(lit:a)")]
    public static void FlagsOfTheParse_AreKeptInTheNodes(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    // A named group is checked for its name and stored as any group, the name is not kept.
    [Theory]
    [InlineData("(?P<n>a)")]
    [InlineData("(?<n>a)")]
    [InlineData("(?P<N_1>a)")]
    [InlineData("(?<0>a)")]
    public static void NamedGroup_IsAGroup(string pattern)
    {
        Assert.Equal("Capture(lit:a)", ParseHelper.Tree(pattern));
    }

    // The first error in the order of the text is the one that is given.
    [Theory]
    [InlineData("a**(", 1, "invalid nested repetition operator")]
    [InlineData("a(**", 2, "missing argument to repetition operator")]
    [InlineData("a\\y(", 1, "invalid escape sequence")]
    [InlineData("[a\\y", 2, "invalid escape sequence")]
    [InlineData("\\y(", 0, "invalid escape sequence")]
    [InlineData("(?:a{1000}){2}x(", 11, "invalid repeat count")]
    [InlineData("(?:a{1000}){2}\\y", 11, "invalid repeat count")]
    [InlineData("a{2,1}{1001}", 1, "invalid repeat count")]
    [InlineData("a{1001}**", 1, "invalid repeat count")]
    public static void FirstErrorOfTheText_IsGiven(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The height of the tree: a group that is a node adds one, a group of (?: ) adds none, and the leaf is one.
    [Fact]
    public static void Height_999GroupsAreAccepted_And1000AreRefusedAtZero()
    {
        using PatternTree tree = ParseHelper.Parse(new string('(', 999) + "a" + new string(')', 999));
        Assert.Equal(1000, tree.Height(tree.Root));

        ParseHelper.AssertRefused(new string('(', 1000) + "a" + new string(')', 1000), 0, RegexError.NestingDepth);
    }

    [Fact]
    public static void Height_NonCapturingGroupsAddNothing()
    {
        using PatternTree tree = ParseHelper.Parse(string.Concat(Enumerable.Repeat("(?:", 4000)) + "a" + new string(')', 4000));
        Assert.Equal(1, tree.Height(tree.Root));
    }

    // A repetition is a node, and is checked as it is built: 999 of them around a character are accepted and 1000 are not.
    [Fact]
    public static void Height_RepetitionsAreNodes()
    {
        string accepted = string.Concat(Enumerable.Repeat("(?:", 999)) + "a" + string.Concat(Enumerable.Repeat(")*", 999));
        string refused = string.Concat(Enumerable.Repeat("(?:", 1000)) + "a" + string.Concat(Enumerable.Repeat(")*", 1000));
        using PatternTree tree = ParseHelper.Parse(accepted);
        Assert.Equal(1000, tree.Height(tree.Root));

        ParseHelper.AssertRefused(refused, 0, RegexError.NestingDepth);
    }

    // An error of the text before the node that is too high wins; one after it does not.
    [Fact]
    public static void Height_ErrorsAreInTheOrderOfTheText()
    {
        string open = new('(', 1000);
        ParseHelper.AssertRefused(open + "a**", 1001, RegexError.InvalidRepeatOperator);
        ParseHelper.AssertRefused(open + "a" + new string(')', 1000) + "(", 0, RegexError.NestingDepth);
    }

    // The counts: the count of a repeat is its upper end, the lower one when it has none, and the product on a path of nested
    // repeats is kept in the node. A repeat of {0}, and of {0,1}, {1} when it holds nothing larger, is not checked.
    [Theory]
    [InlineData("a{1000}", 1000)]
    [InlineData("a{1000,}", 1000)]
    [InlineData("a{0,1000}", 1000)]
    [InlineData("(?:a{10}){100}", 1000)]
    [InlineData("(?:a{1000}){1}", 1000)]
    [InlineData("(?:a{1000}){0,1}", 1000)]
    [InlineData("(?:a{1000}){0}", 1)]
    [InlineData("(a{2}){500}", 1000)]
    [InlineData("(?:a*){1000}", 1000)]
    [InlineData("(?:(?:a{500}){2}){1}", 1000)]
    [InlineData("(?:(?:a{3}){3}){3}", 27)]
    [InlineData("(?:a{1,2}){500}", 1000)]
    [InlineData("a{2,}", 2)]
    [InlineData("a{0,}", 1)]
    [InlineData("a*", 1)]
    [InlineData("a{2}b{3}", 3)]
    public static void Product_OfNestedCounts_IsKeptInTheNode(string pattern, int product)
    {
        using PatternTree tree = ParseHelper.Parse(pattern);
        Assert.Equal(product, tree.Product(tree.Root));
    }

    // The outer repeat is the one that is refused, at its "{".
    [Theory]
    [InlineData("(?:a{4}){251}")]
    [InlineData("(?:a{10}){101}")]
    [InlineData("(?:(?:a{10}){10}){11}")]
    [InlineData("(?:a{1000}){2}")]
    [InlineData("(a{2}){501}")]
    [InlineData("(?:a{2,}){501}")]
    [InlineData("(?:a*){1001}")]
    [InlineData("(?:a{0,1000}){2}")]
    [InlineData("(?:a{1000,}){2}")]
    [InlineData("(?:a{1,2}){501}")]
    [InlineData("(?:a{0,2}){501}")]
    [InlineData("(?:(?:a{1000}){0,}){2}")]
    [InlineData("(?:(?:a{1000}){1}){2}")]
    [InlineData("(?:(?:a{1000}){0}){2000}")]
    [InlineData("(?:(?:a{500}){2}){2}")]
    public static void ProductOverTheLimit_IsRefusedAtTheOuterRepeat(string pattern)
    {
        ParseHelper.AssertRefusedAtTheLastBrace(pattern, RegexError.InvalidRepeatCount);
    }

    [Theory]
    [InlineData("a{1000}")]
    [InlineData("a{1000,}")]
    [InlineData("a{0,1000}")]
    [InlineData("a{1000,1000}")]
    [InlineData("a{1}")]
    [InlineData("a{0}")]
    public static void CountAtTheLimit_IsAccepted(string pattern)
    {
        using PatternTree tree = ParseHelper.Parse(pattern);
        Assert.Equal(NodeKind.Repeat, tree.Kind(tree.Root));
    }

    [Theory]
    [InlineData("a{1001}")]
    [InlineData("a{1001,}")]
    [InlineData("a{0,1001}")]
    [InlineData("a{1001,1001}")]
    [InlineData("a{99999999}")]
    [InlineData("a{100000000}")]
    [InlineData("a{123456789012345678901234567890}")]
    [InlineData("a{2,1}")]
    [InlineData("a{1000,999}")]
    [InlineData("a{0,123456789012345678901234567890}")]
    public static void CountOverTheLimit_IsRefusedAtTheBrace(string pattern)
    {
        ParseHelper.AssertRefused(pattern, 1, RegexError.InvalidRepeatCount);
    }

    // A lone surrogate is an error at its offset, "invalid UTF-16", wherever the parser reads it: as a literal, in a class, in a
    // name, in a \Q...\E quotation, and where Go looks ahead at the text to name it in an error.
    [Fact]
    public static void LoneSurrogate_IsAnErrorAtItsOffset()
    {
        string high = "\uD800";
        string low = "\uDC00";
        ParseHelper.AssertRefused(high, 0, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("a" + low, 1, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused(low + high, 0, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("ab" + high + "c", 2, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("[" + high + "]", 1, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("[a-" + high + "]", 3, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("\\Qa" + high + "\\E", 3, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("(?P<" + high + ">a)", 4, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("(?P<n" + high, 5, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("(?<n" + high, 4, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("\\p{" + high + "}", 3, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("\\p{L" + high, 4, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("\\p" + high, 2, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("\\" + high, 1, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("(?i)" + high, 4, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("(?" + high, 2, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("a{1," + high, 4, RegexError.InvalidUtf16);
        ParseHelper.AssertRefused("\\x" + high, 2, RegexError.InvalidUtf16);
    }

    // A surrogate pair is one code point, a character beyond the BMP.
    [Theory]
    [InlineData("\uD83D\uDE00", "lit:\\x{1F600}")]
    [InlineData("a\uD83D\uDE00b", "lit:a\\x{1F600}b")]
    [InlineData("[\uD83D\uDE00]", "lit:\\x{1F600}")]
    [InlineData("[\uD83D\uDE00-\uD83D\uDE02]", "class[1F600-1F602]")]
    [InlineData("\\Q\uD83D\uDE00\\E", "lit:\\x{1F600}")]
    [InlineData("\uD83D\uDE00*", "Star(lit:\\x{1F600})")]
    [InlineData("\\x{1F600}", "lit:\\x{1F600}")]
    public static void SurrogatePair_IsOneCodePoint(string pattern, string expected)
    {
        Assert.Equal(expected, ParseHelper.Tree(pattern));
    }

    // The offset of an error after a pair counts both of its UTF-16 code units.
    [Fact]
    public static void OffsetAfterASurrogatePair_CountsTwoCodeUnits()
    {
        ParseHelper.AssertRefused("\uD83D\uDE00a**", 3, RegexError.InvalidRepeatOperator);
        ParseHelper.AssertRefused("\uD83D\uDE00\\y", 2, RegexError.InvalidEscape);
    }

    // The parse of a long pattern of one construct is the work of its length.
    [Fact]
    public static void LongPatterns_AreParsed()
    {
        using PatternTree literals = ParseHelper.Parse(new string('a', 50_000));
        Assert.Equal(NodeKind.Literal, literals.Kind(literals.Root));
        Assert.Equal(50_000, literals.DataCount(literals.Root));

        using PatternTree groups = ParseHelper.Parse(string.Concat(Enumerable.Repeat("(a)", 20_000)));
        Assert.Equal(20_000, groups.ChildCount(groups.Root));

        using PatternTree stars = ParseHelper.Parse(string.Concat(Enumerable.Repeat("a*", 20_000)));
        Assert.Equal(20_000, stars.ChildCount(stars.Root));
    }

    // Alternatives that are single code points are one class, at a cost in proportion to their number.
    [Fact]
    public static void AlternativesOfSingleCodePoints_AreOneClass()
    {
        string pattern = string.Join('|', Enumerable.Range(0, 3000).Select(i => char.ConvertFromUtf32(0x4E00 + (2 * i))));
        using PatternTree tree = ParseHelper.Parse(pattern);

        Assert.Equal(NodeKind.Class, tree.Kind(tree.Root));
        Assert.Equal(3000, tree.DataCount(tree.Root) / 2);
        Assert.Equal(3000, tree.OwnedRanges);
    }

    // Alternatives with a common literal prefix are factored out once, as Go does: a(b|c) and not an alternation of the whole.
    [Fact]
    public static void AlternativesWithACommonPrefix_AreFactoredToOneLevel()
    {
        string pattern = string.Join('|', Enumerable.Range(0, 2000).Select(i => "ab" + char.ConvertFromUtf32(0x4E00 + (2 * i)) + "z"));
        using PatternTree tree = ParseHelper.Parse(pattern);

        Assert.Equal(NodeKind.Concat, tree.Kind(tree.Root));
        Assert.Equal(3, tree.Height(tree.Root));
        Assert.Equal(2000, tree.ChildCount(tree.LastChild(tree.Root)));
    }

    // a|aa|aaa|... is factored one prefix at a time, a level deeper for each alternative: the tree is as high as Go makes it, which
    // is the height that decides its limit, and the factoring is a loop that does not use the stack of the thread.
    [Fact]
    public static void NestedFactoring_FollowsGoToTheLimitOfTheHeight()
    {
        Assert.Equal(
            "Concat(lit:aAlternate(EmptyMatchConcat(lit:aAlternate(EmptyMatchConcat(lit:aAlternate(EmptyMatchlit:a))))))",
            ParseHelper.Tree(Chain(4)));
        using (PatternTree tree = ParseHelper.Parse(Chain(500)))
        {
            Assert.Equal(999, tree.Height(tree.Root));
        }

        ParseHelper.AssertRefused(Chain(501), 0, RegexError.NestingDepth);
    }

    private static string Chain(int count) => string.Join('|', Enumerable.Range(1, count).Select(i => new string('a', i)));

    [Fact]
    public static void ErrorMessageReason_OfAPerlConstruct_NamesTheFragmentGoStopsAt()
    {
        RegexError error = ParseHelper.Refuse("ab(?<=c)");

        Assert.Equal(2, error.Offset);
        Assert.Equal(RegexError.InvalidNamedCapture, error.Reason);

        error = ParseHelper.Refuse("ab(?=c)");
        Assert.Equal(2, error.Offset);
        Assert.Equal(RegexError.UnsupportedPerlSyntax + "(?=", error.Reason);
    }

    // The parse is of the text only: the same text, parsed again, is the same tree and the same error.
    [Fact]
    public static void Parse_IsRepeatable()
    {
        string pattern = "(?i)(a|b)*[\\pL_]x{2,3}|\\d+";
        Assert.Equal(ParseHelper.Tree(pattern), ParseHelper.Tree(pattern));

        RegexError first = ParseHelper.Refuse("a(b");
        RegexError second = ParseHelper.Refuse("a(b");
        Assert.Equal(first.Offset, second.Offset);
        Assert.Equal(first.Reason, second.Reason);
    }
}

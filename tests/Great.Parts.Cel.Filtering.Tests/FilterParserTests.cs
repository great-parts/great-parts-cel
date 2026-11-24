using System.Collections.Generic;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Filtering.Tests;

// The parser of filters: the tree of each rule of the grammar, the priority of the joins, the keywords, the minus, the numbers with
// dots, the calls, and the place of the errors that the table of the syntax examples does not have.
[Trait("Requirement", "F-32")]
public sealed class FilterParserTests
{
    [Theory]
    [InlineData("", "empty")]
    [InlineData("a", "a")]
    [InlineData("a = b", "(= a b)")]
    [InlineData("a:*", "(: a *)")]
    [InlineData("a.b.c >= 1", "(>= a.b.c 1)")]
    [InlineData("a b OR c AND d", "(and (seq a (or b c)) d)")]
    [InlineData("a AND b AND c", "(and a b c)")]
    [InlineData("a OR b OR c", "(or a b c)")]
    [InlineData("a b c", "(seq a b c)")]
    [InlineData("(a OR b) c", "(seq [(or a b)] c)")]
    [InlineData("NOT a", "(not a)")]
    [InlineData("-a", "(not a)")]
    [InlineData("NOT (a b)", "(not [(seq a b)])")]
    [InlineData("a = (b OR c)", "(= a [(or b c)])")]
    [InlineData("f(a, b.c, \"d\")", "f(a, b.c, \"d\")")]
    [InlineData("f()", "f()")]
    [InlineData("a.b.f(x) = 1", "(= a.b.f(x) 1)")]
    [InlineData("f(g(x), (a OR b))", "f(g(x), [(or a b)])")]
    [InlineData("f (x)", "(seq f [x])")]
    [InlineData("(((a)))", "[[[a]]]")]
    [InlineData("a=b", "(= a b)")]
    [InlineData("a =b OR c= d", "(or (= a b) (= c d))")]
    public static void Tree_IsTheOneOfTheGrammar(string filter, string expected) => Assert.Equal(expected, Shown(filter));

    [Theory]
    [InlineData("title = A and title = B", "(seq (= title A) and (= title B))")]
    [InlineData("a or b", "(seq a or b)")]
    [InlineData("not a", "(seq not a)")]
    [InlineData("ANDa", "ANDa")]
    [InlineData("x.AND = 1", "(= x.AND 1)")]
    [InlineData("x.OR.NOT = 1", "(= x.OR.NOT 1)")]
    [InlineData("AND(x)", "AND(x)")]
    [InlineData("NOT(x)", "NOT(x)")]
    [InlineData("a.NOT(x)", "a.NOT(x)")]
    public static void Keyword_IsAKeywordOnlyAsTheWholeTextInUpperCase(string filter, string expected) => Assert.Equal(expected, Shown(filter));

    [Theory]
    [InlineData("pages > -5", "(> pages -5)")]
    [InlineData("-5", "-5")]
    [InlineData("-available = true", "(not (= available true))")]
    [InlineData("a = -b", "(= a -b)")]
    [InlineData("pages > 1.5", "(> pages 1.5)")]
    [InlineData("read_time < 2.5s", "(< read_time 2.5s)")]
    [InlineData("rating = -1.5e3", "(= rating -1.5e3)")]
    [InlineData("f(-1, 2)", "f(-1, 2)")]
    [InlineData("a=-5", "(= a -5)")]
    public static void Minus_BeforeANumber_IsItsSign(string filter, string expected) => Assert.Equal(expected, Shown(filter));

    [Theory]
    [InlineData("a AND(b)", 5, 1)]
    [InlineData("a AND b OR", 10, 0)]
    [InlineData("a OR", 4, 0)]
    [InlineData("a OR(b)", 4, 1)]
    [InlineData("NOT", 3, 0)]
    [InlineData("NOT(", 4, 0)]
    [InlineData("NOT NOT a", 4, 3)]
    [InlineData("a,", 1, 1)]
    [InlineData("f(a,)", 4, 1)]
    [InlineData("f(a", 3, 0)]
    [InlineData("f(a b)", 4, 1)]
    [InlineData("a = (", 5, 0)]
    [InlineData("a = ( )", 6, 1)]
    [InlineData("pages > - 5", 10, 1)]
    [InlineData("a. b", 3, 1)]
    [InlineData("a.", 2, 0)]
    [InlineData("a.(b)", 2, 1)]
    [InlineData("(a)(b)", 3, 1)]
    [InlineData("(a)b", 3, 1)]
    [InlineData("a !b", 2, 1)]
    [InlineData("a = AND", 4, 3)]
    [InlineData("\"f\"(x)", 3, 1)]
    [InlineData("-5(x)", 2, 1)]
    [InlineData("a = 'x", 4, 2)]
    [InlineData("a>", 2, 0)]
    [InlineData("(a = b", 6, 0)]
    public static void SyntaxError_IsAtTheToken(string filter, int start, int length)
    {
        FilterError error = Assert.IsType<FilterError>(FilterParser.Parse(filter).Error);

        Assert.Equal(CelErrorCode.FilterSyntaxError, error.Code);
        Assert.Equal((start, start + length), (error.Start, error.End));
    }

    [Fact]
    public void Nesting_AtTheLimitParses_AndDeeperIsNestingTooDeep()
    {
        Assert.Null(FilterParser.Parse(Nest(250, "(", ")")).Error);
        Assert.Null(FilterParser.Parse(Nest(250, "f(", ")")).Error);
        Assert.Equal(CelErrorCode.NestingTooDeep, FilterParser.Parse(Nest(251, "(", ")")).Error!.Code);
        Assert.Equal(CelErrorCode.NestingTooDeep, FilterParser.Parse(Nest(251, "f(", ")")).Error!.Code);
        Assert.Equal(CelErrorCode.NestingTooDeep, FilterParser.Parse(Nest(300, "NOT (", ")")).Error!.Code);
    }

    [Fact]
    public void Nesting_OfParenthesesThatAreNotClosed_StopsAtTheLimitWithoutReadingTheRest()
    {
        FilterParseResult result = FilterParser.Parse(new string('(', 65_536));

        Assert.Equal(CelErrorCode.NestingTooDeep, result.Error!.Code);
        Assert.Equal((250, 251), (result.Error.Start, result.Error.End));
    }

    [Fact]
    public void Nesting_OfEachJoin_IsParsedWithTheBoundedStack()
    {
        // Each level adds an AND, a sequence, an OR and a negation to the tree of the filter.
        StringBuilder text = new();
        for (int i = 0; i < 250; i++)
        {
            text.Append("a AND b c OR NOT (");
        }

        text.Append('x').Append(')', 250);

        Assert.Null(FilterParser.Parse(text.ToString()).Error);
    }

    [Fact]
    public void Ranges_AreThoseOfTheTextOfTheNode()
    {
        FilterExpr root = FilterParser.Parse("title = A OR NOT (x)").Root!;

        FilterJoin or = Assert.IsType<FilterJoin>(root);
        FilterRestriction restriction = Assert.IsType<FilterRestriction>(or.Operands[0]);
        FilterNot not = Assert.IsType<FilterNot>(or.Operands[1]);

        Assert.Equal((0, 20), (or.Start, or.End));
        Assert.Equal((10, 12), (or.Keywords[0].Start, or.Keywords[0].End));
        Assert.Equal((6, 7), (restriction.ComparatorStart, restriction.ComparatorEnd));
        Assert.Equal((0, 9), (restriction.Start, restriction.End));
        Assert.Equal((13, 16), (not.MarkStart, not.MarkEnd));
        Assert.Equal((13, 20), (not.Start, not.End));
    }

    [Fact]
    public void Member_KeepsTheSourceOfTheNumberAndTheValueOfAString()
    {
        FilterRestriction number = Assert.IsType<FilterRestriction>(FilterParser.Parse("a > -1.5").Root);
        FilterRestriction text = Assert.IsType<FilterRestriction>(FilterParser.Parse("a = \"x\\*y\"").Root);

        FilterMember value = Assert.IsType<FilterMember>(number.Right);
        FilterMember quoted = Assert.IsType<FilterMember>(text.Right);

        Assert.Equal("-1.5", value.Source);
        Assert.Equal(2, value.Segments.Count);
        Assert.Equal("-1", value.Segments[0].Text);
        Assert.Equal(4, value.Segments[0].Start);
        Assert.Equal("x*y", Assert.Single(quoted.Segments).Quoted!.Value);
    }

    private static string Nest(int depth, string open, string close)
    {
        StringBuilder text = new();
        for (int i = 0; i < depth; i++)
        {
            text.Append(open);
        }

        text.Append('x');
        for (int i = 0; i < depth; i++)
        {
            text.Append(close);
        }

        return text.ToString();
    }

    // The tree as text: a join in parentheses, a group in square brackets, a comparison with its comparator.
    private static string Shown(string filter)
    {
        FilterParseResult result = FilterParser.Parse(filter);
        Assert.Null(result.Error);
        return Show(result.Root!);
    }

    private static string Show(FilterExpr expression) =>
        expression switch
        {
            FilterEmpty => "empty",
            FilterJoin join => "(" + JoinName(join.Kind) + " " + string.Join(' ', Shown(join.Operands)) + ")",
            FilterNot not => "(not " + Show(not.Operand) + ")",
            FilterGroup group => "[" + Show(group.Inner) + "]",
            FilterRestriction restriction => "(" + Symbol(restriction.Comparator) + " " + Show(restriction.Left) + " " + Show(restriction.Right) + ")",
            FilterMember member => member.Segments[0].Quoted is { } quoted && member.Segments.Count == 1
                ? "\"" + quoted.Value + "\""
                : member.Source,
            FilterCall call => string.Join('.', Names(call)) + "(" + string.Join(", ", Shown(call.Arguments)) + ")",
            _ => "?",
        };

    private static IEnumerable<string> Shown(IReadOnlyList<FilterExpr> operands)
    {
        foreach (FilterExpr operand in operands)
        {
            yield return Show(operand);
        }
    }

    private static IEnumerable<string> Names(FilterCall call)
    {
        foreach (FilterSegment part in call.Name)
        {
            yield return part.Text;
        }
    }

    private static string JoinName(FilterJoinKind kind) =>
        kind switch
        {
            FilterJoinKind.And => "and",
            FilterJoinKind.Or => "or",
            _ => "seq",
        };

    private static string Symbol(FilterComparator comparator) =>
        comparator switch
        {
            FilterComparator.Equal => "=",
            FilterComparator.NotEqual => "!=",
            FilterComparator.Less => "<",
            FilterComparator.LessOrEqual => "<=",
            FilterComparator.Greater => ">",
            FilterComparator.GreaterOrEqual => ">=",
            _ => ":",
        };
}

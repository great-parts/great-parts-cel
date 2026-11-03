using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The limits of the parser (docs/design/syntax.md, "Limits and the stack"): exactly the limit passes, one more is the
// error of its code at its place; and recovery after syntax errors ("Errors and recovery").
public sealed class ParserLimitsTests
{
    private const string Smile = "\U0001F600";

    // A pair of surrogates is 4 UTF-8 bytes, U+20AC 3 bytes. The lengths are just past and just at 65 536 / 3 code
    // units, where the parser starts to count bytes.
    [Theory]
    [InlineData("'", Smile, 16383, "ab'")]
    [InlineData("'", "€", 21844, "'")]
    [InlineData("'", "€", 21845, "")]
    public static void TextOfAtMost65536Bytes_IsParsedOrHasNoLengthError(string head, string unit, int count, string tail)
    {
        ParseResult result = Parser.Parse(head + Repeat(unit, count) + tail);

        Assert.DoesNotContain(result.Errors, error => error.Code == CelErrorCode.ExpressionTooLong);
    }

    // The error is at the code unit with which the bytes pass the limit; a pair of surrogates across the limit is
    // reported at its first unit. Past 65 536 code units only the first 65 537 are read, also for lines.
    [Theory]
    [InlineData("'", Smile, 16383, "abc'", 32770, 1, 32771)]
    [InlineData("'", Smile, 16383, "aa" + Smile + "'", 32769, 1, 32770)]
    [InlineData("'", "€", 21845, "'", 21846, 1, 21847)]
    [InlineData("", "\r\n", 40000, "", 65536, 32769, 1)]
    public static void TextOverTheLimit_IsExpressionTooLongAtTheUnitPastIt(
        string head, string unit, int count, string tail, int offset, int line, int column)
    {
        CelError error = Assert.Single(Parser.Parse(head + Repeat(unit, count) + tail).Errors);

        Assert.Equal(
            (CelErrorCode.ExpressionTooLong, CelErrorStage.Parse, "expression is longer than 65536 UTF-8 bytes", offset, line, column),
            (error.Code, error.Stage, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // Each index adds one bracket; each "a < (b + (" adds two brackets and one to the largest of the operand counts,
    // not two: 1 + 2 * 83 + 83 = 250.
    [Theory]
    [InlineData("x[", "0", "]", 249)]
    [InlineData("c ? a : ", "z", "", 249)]
    [InlineData("a < (b + (", "c", "))", 83)]
    public static void NestingOf250_IsParsed(string open, string inner, string close, int count)
    {
        ParseResult result = Parser.Parse(Repeat(open, count) + inner + Repeat(close, count));

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(Format)));
    }

    // The error is at the bracket, the "?" or the operator that opens level 251, and parsing stops there: nothing after
    // it is reported, not even the missing closing brackets.
    [Theory]
    [InlineData("x[", "0", "", 250, 499)]
    [InlineData("c ? a : ", "z", "", 250, 1994)]
    [InlineData("a < (b + (", "c", "", 84, 832)]
    public static void NestingOver250_IsNestingTooDeepAndStopsParsing(string open, string inner, string close, int count, int offset)
    {
        ImmutableArray<CelError> errors = Parser.Parse(Repeat(open, count) + inner + Repeat(close, count)).Errors;

        Assert.Equal(
            "NestingTooDeep@" + offset.ToString(CultureInfo.InvariantCulture) + " nesting depth exceeds the limit of 250",
            Format(Assert.Single(errors)));
    }

    // Selections and indexes count, a chain of && between comparisons does not.
    [Theory]
    [InlineData("x", ".f[0]", 125, "")]
    [InlineData("", "x == 1 && ", 300, "true")]
    public static void SyntaxTreeOfDepth250_IsParsed(string head, string unit, int count, string tail)
    {
        Assert.True(Parser.Parse(head + Repeat(unit, count) + tail).IsSuccess);
    }

    [Fact]
    public static void SyntaxTreeOfDepth251_IsNestingTooDeepAtTheFirstNodePastTheLimit()
    {
        CelError error = Assert.Single(Parser.Parse("x" + Repeat(".f[0]", 125) + ".g").Errors);

        Assert.Equal("NestingTooDeep@626 syntax tree depth exceeds the limit of 250", Format(error));
    }

    // The depth is checked when parsing reaches the end of the text, also past a syntax error, and the error takes its
    // place by offset before the syntax error found earlier.
    [Fact]
    public static void SyntaxTreeTooDeep_IsListedByOffsetAmongTheSyntaxErrors()
    {
        ImmutableArray<CelError> errors = Parser.Parse("x" + Repeat(".f", 251) + " 1").Errors;

        Assert.Equal(
            "NestingTooDeep@501 syntax tree depth exceeds the limit of 250 | SyntaxError@504 syntax error: expected the end of the text, found '1'",
            string.Join(" | ", errors.Select(Format)));
    }

    // Parsing that stops does not reach the end of the text, so the depth of the tree is not reported.
    [Fact]
    public static void SyntaxTreeTooDeep_IsNotReportedWhenParsingStops()
    {
        ImmutableArray<CelError> errors = Parser.Parse("x" + Repeat(".f", 251) + " + " + Repeat("(", 250) + "1").Errors;

        Assert.Equal("NestingTooDeep@754 nesting depth exceeds the limit of 250", Format(Assert.Single(errors)));
    }

    // "[1 2, f(3 4), {5: 6 7}]": after each error parsing goes on at the next "," or closing bracket of the construct
    // the error is in.
    [Theory]
    [InlineData("[1 2, f(3 4), {5: 6 7}]", "SyntaxError@3 ']' '2' | SyntaxError@10 ')' '4' | SyntaxError@20 '}' '7'")]
    [InlineData("[1 2 (3, 4), 5 6]", "SyntaxError@3 ']' '2' | SyntaxError@15 ']' '6'")]
    [InlineData("f([1 2), x", "SyntaxError@5 ']' '2' | SyntaxError@7 the end of the text ','")]
    [InlineData("(1 2, 3) + y z", "SyntaxError@3 ')' '2' | SyntaxError@13 the end of the text 'z'")]
    [InlineData("1 2 ) ] , 3 4", "SyntaxError@2 the end of the text '2'")]
    [InlineData("A{f: 1 g: 2, h 3}", "SyntaxError@7 '}' 'g' | SyntaxError@15 ':' '3'")]
    [InlineData("[,,1 2]", "SyntaxError@2 ']' ',' | SyntaxError@5 ']' '2'")]
    public static void SyntaxErrors_AreRecoveredFromAtTheNextCommaOrClosingBracket(string expression, string errors)
    {
        ImmutableArray<CelError> found = Parser.Parse(expression).Errors;

        Assert.Equal(errors, string.Join(" | ", found.Select(FormatSyntaxError)));
    }

    [Fact]
    public static void HundredErrors_AreAllReportedWithoutTooManyErrors()
    {
        ImmutableArray<CelError> errors = Parser.Parse(CallWithErrors(100)).Errors;

        Assert.Equal(100, errors.Length);
        Assert.All(errors, error => Assert.Equal(CelErrorCode.SyntaxError, error.Code));
        Assert.Equal(499, errors[^1].Location.Offset);
    }

    // "f(a b, a b, …)": the "b" of argument i is at the offset 5 i + 4.
    [Fact]
    public static void HundredAndFirstError_IsTooManyErrorsLastAndStopsParsing()
    {
        ImmutableArray<CelError> errors = Parser.Parse(CallWithErrors(150)).Errors;

        Assert.Equal(101, errors.Length);
        Assert.Equal(Enumerable.Range(0, 100).Select(i => 5 * i + 4), errors.Take(100).Select(error => error.Location.Offset));
        Assert.Equal("TooManyErrors@504 more than 100 syntax errors; parsing stopped", Format(errors[^1]));
    }

    // Errors of every code count towards the limit.
    [Fact]
    public static void ReservedIdentifiers_CountTowardsTheLimit()
    {
        ImmutableArray<CelError> errors = Parser.Parse("[" + string.Join(", ", Enumerable.Repeat("if", 100)) + ", 1 2]").Errors;

        Assert.Equal(101, errors.Length);
        Assert.All(errors.Take(100), error => Assert.Equal(CelErrorCode.ReservedIdentifier, error.Code));
        Assert.Equal("TooManyErrors@403 more than 100 syntax errors; parsing stopped", Format(errors[^1]));
    }

    // On a thread with a small stack, parsing called with the stack nearly used up reports InsufficientStack at the
    // current token instead of overflowing the stack, and stops.
    [Fact]
    public static void ParsingOnASmallStack_IsInsufficientStackInsteadOfAnOverflow()
    {
        string text = Repeat("(", 249) + "1" + Repeat(")", 249);
        ParseResult? result = null;
        var thread = new Thread(() => result = ParseOnExhaustedStack(text), 256 * 1024);

        thread.Start();
        thread.Join();

        Assert.NotNull(result);
        Assert.Equal(
            "InsufficientStack@0 insufficient execution stack to continue parsing",
            Format(Assert.Single(result.Errors)));
    }

    // Descends until the runtime reports that the stack is nearly used up, then parses there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ParseResult ParseOnExhaustedStack(string text)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return Parser.Parse(text);
        }

        Span<byte> frame = stackalloc byte[256];
        frame.Fill(1);
        ParseResult result = ParseOnExhaustedStack(text);
        Assert.Equal((byte)1, frame[^1]);
        return result;
    }

    private static string CallWithErrors(int count) => "f(" + string.Join(", ", Enumerable.Repeat("a b", count)) + ")";

    private static string Repeat(string unit, int count) => string.Concat(Enumerable.Repeat(unit, count));

    private static string Format(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{error.Code}@{error.Location.Offset} {error.Message}");

    // A syntax error as "<code>@<offset> <expected> <found>".
    private static string FormatSyntaxError(CelError error)
    {
        const string Prefix = "syntax error: expected ";
        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        return string.Create(CultureInfo.InvariantCulture, $"{error.Code}@{error.Location.Offset} {error.Message[Prefix.Length..].Replace(", found ", " ", StringComparison.Ordinal)}");
    }
}

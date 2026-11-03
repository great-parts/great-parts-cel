using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The limits of parsing: 65 536 UTF-8 bytes of text, nesting and syntax tree depth of 250, 100 syntax
// errors, and the execution stack. Exactly the limit is accepted; past it the error has the place where
// the limit was passed. Texts are built from a head, a repeated part and a tail, so the names of the cases
// stay short; a character is given by its code.
public sealed class ParseLimitTests
{
    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    // A code unit is at least one UTF-8 byte: é is 2, a lone surrogate 3, the length of the U+FFFD that replaces it.
    [Theory]
    [Trait("Requirement", "R-2")]
    [InlineData("'", 0x61, 65534, "'")]
    [InlineData("'", 0xE9, 32767, "'")]
    [InlineData("x", 0x20, 65535, "")]
    public void TextOfExactly65536Bytes_IsParsed(string head, int character, int count, string tail)
    {
        ParseResult result = environment.Parse(head + new string((char)character, count) + tail);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("'", 0x61, 65535, "'", 65536, 1, 65537)]
    [InlineData("", 0x61, 100000, "", 65536, 1, 65537)]
    [InlineData("1 2", 0x20, 65534, "", 65536, 1, 65537)]
    [InlineData("'", 0xE9, 32768, "'", 32768, 1, 32769)]
    [InlineData("", 0xD800, 21846, "", 21845, 1, 21846)]
    [InlineData("", 0x0A, 65536, "x", 65536, 65537, 1)]
    public void TextOverTheLimit_IsExpressionTooLongAtTheFirstCharacterPastIt(
        string head, int character, int count, string tail, int offset, int line, int column)
    {
        CelError error = Assert.Single(Errors(head + new string((char)character, count) + tail));

        AssertError(CelErrorCode.ExpressionTooLong, "expression is longer than 65536 UTF-8 bytes", error);
        AssertPlace(offset, line, column, error);
    }

    // 21 845 lone surrogates are 65 535 bytes: the text is parsed, and a lone surrogate is outside the lexis.
    [Fact]
    [Trait("Requirement", "R-2")]
    public void LoneSurrogatesWithinTheLimit_AreParsed()
    {
        CelError error = Errors(new string((char)0xD800, 21845))[0];

        Assert.Equal(CelErrorCode.SyntaxError, error.Code);
        AssertPlace(0, 1, 1, error);
    }

    // The nesting at a point of the text, as cel-go counts it: 1, plus the enclosing brackets of any kind, plus
    // the largest of three counts of enclosing operands — third operands of ?:, right operands of comparisons
    // and in, right operands of + - * / %. So "x + (" adds two levels.
    [Theory]
    [Trait("Requirement", "R-2")]
    [InlineData("(", ")", 249)]
    [InlineData("[", "]", 249)]
    [InlineData("f(", ")", 249)]
    [InlineData("{1: ", "}", 249)]
    [InlineData("x + (", ")", 124)]
    public void NestingOf250_IsParsed(string open, string close, int count)
    {
        Assert.True(environment.Parse(Repeat(open, count) + "x" + Repeat(close, count)).IsSuccess);
    }

    // The error is at the bracket that opens level 251, and parsing stops there.
    [Theory]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("(", ")", 250, 249)]
    [InlineData("[", "]", 250, 249)]
    [InlineData("f(", ")", 250, 499)]
    [InlineData("{1: ", "}", 250, 996)]
    [InlineData("x + (", ")", 125, 624)]
    public void NestingOver250_IsNestingTooDeepAtTheBracketThatPassesIt(string open, string close, int count, int offset)
    {
        CelError error = Assert.Single(Errors(Repeat(open, count) + "x" + Repeat(close, count)));

        AssertError(CelErrorCode.NestingTooDeep, "nesting depth exceeds the limit of 250", error);
        AssertPlace(offset, 1, offset + 1, error);
    }

    // Binary operators, selections, indexes and receiver calls count; chains of && and ||, which are
    // balanced trees, and runs of ! and - do not.
    [Theory]
    [Trait("Requirement", "R-2")]
    [InlineData("x", "\n+ x", 250, "")]
    [InlineData("x", ".f", 250, "")]
    [InlineData("x", "[0]", 250, "")]
    [InlineData("x", ".f()", 250, "")]
    [InlineData("x", " && x", 1000, "")]
    [InlineData("x", " || x", 1000, "")]
    [InlineData("", "!", 1001, "x")]
    [InlineData("", "-", 1000, "x")]
    public void SyntaxTreeOfDepth250_IsParsed(string head, string unit, int count, string tail)
    {
        Assert.True(environment.Parse(head + Repeat(unit, count) + tail).IsSuccess);
    }

    // The error is at the first node of depth 251, the 251st operator, selection, index or receiver call.
    [Theory]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    [InlineData("\n+ x", 1002, 252, 1)]
    [InlineData(".f", 501, 1, 502)]
    [InlineData("[0]", 751, 1, 752)]
    [InlineData(".f()", 1003, 1, 1004)]
    public void SyntaxTreeOfDepth251_IsNestingTooDeepAtTheFirstNodePastTheLimit(string unit, int offset, int line, int column)
    {
        CelError error = Assert.Single(Errors("x" + Repeat(unit, 251)));

        AssertError(CelErrorCode.NestingTooDeep, "syntax tree depth exceeds the limit of 250", error);
        AssertPlace(offset, line, column, error);
    }

    // One element per line, "1 2", gives a syntax error at its "2"; the list goes on at the next comma.
    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    public void HundredSyntaxErrors_AreAllReported()
    {
        ImmutableArray<CelError> errors = Errors(ListOfErrors(100));

        Assert.Equal(100, errors.Length);
        Assert.All(errors, error => Assert.Equal(CelErrorCode.SyntaxError, error.Code));
        AssertPlace(3, 1, 4, errors[0]);
        AssertPlace(498, 100, 3, errors[99]);
    }

    // The 101st error becomes TooManyErrors at its place, after the hundred before it, and parsing stops.
    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    public void HundredAndFirstError_IsTooManyErrorsAtItsPlace()
    {
        ImmutableArray<CelError> errors = Errors(ListOfErrors(101));

        Assert.Equal(101, errors.Length);
        Assert.Equal(Enumerable.Range(0, 100).Select(index => 5 * index + 3), errors.Take(100).Select(error => error.Location.Offset));
        Assert.All(errors.Take(100), error => Assert.Equal(CelErrorCode.SyntaxError, error.Code));
        AssertError(CelErrorCode.TooManyErrors, "more than 100 syntax errors; parsing stopped", errors[100]);
        AssertPlace(503, 101, 3, errors[100]);
    }

    // Parsing called when the stack is nearly used up reports InsufficientStack instead of overflowing it, at
    // the token where it could not go deeper, and stops. Each "(" of the text is on its own line.
    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void ParsingWithoutStack_IsInsufficientStackAtTheCurrentToken()
    {
        const string Text = "(\n(\n(\n1)))";

        ParseResult result = ParseOnExhaustedStack(Text);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        AssertError(CelErrorCode.InsufficientStack, "insufficient execution stack to continue parsing", error);
        Assert.Contains(Text[error.Location.Offset], "(1");
        AssertPlace(2 * (error.Location.Line - 1), error.Location.Line, 1, error);
    }

    // Descends until the runtime reports that the stack is nearly used up, then parses there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ParseResult ParseOnExhaustedStack(string text)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return environment.Parse(text);
        }

        Span<byte> frame = stackalloc byte[1024];
        frame.Fill(1);
        ParseResult result = ParseOnExhaustedStack(text);
        Assert.Equal((byte)1, frame[^1]);
        return result;
    }

    private ImmutableArray<CelError> Errors(string text)
    {
        ParseResult result = environment.Parse(text);

        Assert.False(result.IsSuccess);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
        return result.Errors;
    }

    // "[1 2,\n1 2,\n…1 2]": the "2" of element i is at the offset 5 i + 3, on the line i + 1.
    private static string ListOfErrors(int count) => "[" + string.Join(",\n", Enumerable.Repeat("1 2", count)) + "]";

    private static string Repeat(string unit, int count) => string.Concat(Enumerable.Repeat(unit, count));

    private static void AssertError(CelErrorCode code, string message, CelError error)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Equal(message, error.Message);
    }

    private static void AssertPlace(int offset, int line, int column, CelError error)
    {
        Assert.Equal((offset, line, column), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.Location.SourceName);
    }
}

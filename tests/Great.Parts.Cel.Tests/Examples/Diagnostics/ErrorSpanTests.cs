using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Diagnostics;

// The fragment of an error (CelError.Span) and the two texts for people. The fragment starts at the place of the error,
// Span.Start equals Location, and runs to the end of the node that gave the error: the identifier, the call from "(" to ")",
// the operator and the operand after it; the root has no rule of its own, so an error at a root that is one token covers the
// whole text; a parse error at the end of the text has length 0; a build error has no fragment. Offsets and lengths count
// UTF-16 code units.
[Trait("Requirement", "F-11")]
public sealed class ErrorSpanTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddVariable<long>("x")
        .AddVariable<bool>("ok")
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("totl > 100", CelErrorCode.UndeclaredReference, 0, 4)]
    [InlineData("ok && totl", CelErrorCode.UndeclaredReference, 6, 4)]
    [InlineData("size(1, 2) == 0", CelErrorCode.NoMatchingOverload, 4, 6)]
    [InlineData("x + 'a'", CelErrorCode.NoMatchingOverload, 2, 5)]
    [InlineData("x +", CelErrorCode.SyntaxError, 3, 0)]
    [InlineData("'é' + totl", CelErrorCode.UndeclaredReference, 6, 4)]
    [InlineData("'😀' + totl", CelErrorCode.UndeclaredReference, 7, 4)]
    public static void CompileError_HasTheFragmentFromItsPlaceToTheEndOfTheNode(string expression, CelErrorCode code, int offset, int length)
    {
        CelError error = Assert.Single(Environment.Compile<CelValue>(expression).Errors);

        Assert.Equal(code, error.Code);
        Assert.Equal(error.Location, error.Span.Start);
        Assert.Equal(offset, error.Span.Start.Offset);
        Assert.Equal(length, error.Span.Length);
    }

    // The root of the expression is the place of the expected type and of the result conversion; a root of one token is the
    // whole text.
    [Fact]
    public void ErrorAtTheRoot_CoversTheWholeExpression()
    {
        CelError expected = Assert.Single(Environment.Compile<bool>("'abc'").Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, expected.Code);
        Assert.Equal(expected.Location, expected.Span.Start);
        Assert.Equal(0, expected.Span.Start.Offset);
        Assert.Equal(5, expected.Span.Length);

        CelEvalResult<long> result = Environment.Compile<long>("d").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("d", "text"), cancellationToken: CancellationToken.None);
        CelError conversion = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ResultConversion, conversion.Code);
        Assert.Equal(0, conversion.Span.Start.Offset);
        Assert.Equal(1, conversion.Span.Length);
    }

    [Fact]
    public void EvaluationError_HasTheFragmentOfItsNode()
    {
        CelEvalResult<long> result = Environment.Compile<long>("x / 0").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.DivisionByZero, error.Code);
        Assert.Equal(2, error.Span.Start.Offset);
        Assert.Equal(3, error.Span.Length);
    }

    [Fact]
    public void BuildError_HasNoFragment()
    {
        CelError error = Assert.Single(new CelEnvironmentBuilder().AddVariable<long>("x").AddVariable<string>("x").Build().Errors);

        Assert.Equal(CelErrorCode.DuplicateVariable, error.Code);
        Assert.Equal(default, error.Span);
        Assert.Equal(0, error.Span.Length);
        Assert.Equal(error.Message, error.ToDisplayString("anything"));
        Assert.Equal("DuplicateVariable: " + error.Message, error.ToString());
    }

    [Fact]
    public void ToString_IsTheCodeThePositionAndTheMessage()
    {
        CelError error = Assert.Single(Environment.Compile<CelValue>("ok && totl").Errors);

        Assert.Equal("1:7", error.Location.ToString());
        Assert.Equal("UndeclaredReference at 1:7: " + error.Message, error.ToString());
    }

    [Fact]
    public void SourceLocation_ToString_HasTheSourceName()
    {
        Assert.Equal("rules.json:12:30", new CelSourceLocation("rules.json", 400, 12, 30).ToString());
        Assert.Equal(string.Empty, default(CelSourceLocation).ToString());
    }

    // The line where the fragment starts, and a mark under it: "^" at its first code unit, "~" for each further one.
    [Theory]
    [InlineData("ok && totl", "ok && totl", "      ^~~~")]
    [InlineData("ok &&\n  totl", "  totl", "  ^~~~")]
    [InlineData("ok &&\r\n  totl", "  totl", "  ^~~~")]
    [InlineData("size(1, 2) == 0", "size(1, 2) == 0", "    ^~~~~~")]
    public static void ToDisplayString_ShowsTheLineAndMarksTheFragment(string expression, string line, string mark)
    {
        CelError error = Assert.Single(Environment.Compile<CelValue>(expression).Errors);

        Assert.Equal(error.Message + "\n" + line + "\n" + mark, error.ToDisplayString(expression));
    }

    // A fragment that goes on to the next line is marked to the end of its first line; length 0 is one "^".
    [Fact]
    public void ToDisplayString_StopsTheMarkAtTheEndOfTheLine_AndMarksAnEmptyFragment()
    {
        const string Call = "size(1,\n 2) == 0";
        CelError call = Assert.Single(Environment.Compile<CelValue>(Call).Errors);
        Assert.Equal(call.Message + "\nsize(1,\n    ^~~", call.ToDisplayString(Call));

        const string Cut = "x +";
        CelError end = Assert.Single(Environment.Compile<CelValue>(Cut).Errors);
        Assert.Equal(end.Message + "\nx +\n   ^", end.ToDisplayString(Cut));
    }

    [Fact]
    public void ToDisplayString_WithATextTooShortForTheFragment_IsArgumentException()
    {
        CelError error = Assert.Single(Environment.Compile<CelValue>("ok && totl").Errors);

        Assert.Throws<ArgumentException>("expression", () => error.ToDisplayString("ok"));
        Assert.Throws<ArgumentNullException>("expression", () => error.ToDisplayString(null!));
    }
}

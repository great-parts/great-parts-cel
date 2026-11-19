using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

// The texts for people of an error in the cases that the examples of ErrorSpanTests do not reach: a tab, a lone carriage return, a
// position in a source document, and the fragment of an evaluation error after the error is placed in a document.
[Trait("Requirement", "F-11")]
public sealed class ErrorTextsTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable<bool>("ok")
        .Build()
        .GetEnvironmentOrThrow();

    // A tab is written as it is, and the mark counts code units, not display columns.
    [Fact]
    public void ToDisplayString_WithATab_WritesTheLineAsItIs_AndMarksByCodeUnits()
    {
        const string Text = "x\t+ 'a'";
        CelError error = Assert.Single(Environment.Compile<CelValue>(Text).Errors);

        Assert.Equal(error.Message + "\nx\t+ 'a'\n  ^~~~~", error.ToDisplayString(Text));
    }

    // A lone carriage return ends a line, as the parser counts lines.
    [Fact]
    public void ToDisplayString_WithALoneCarriageReturn_TakesTheLineAfterIt()
    {
        const string Text = "ok &&\r  totl";
        CelError error = Assert.Single(Environment.Compile<CelValue>(Text).Errors);

        Assert.Equal(2, error.Location.Line);
        Assert.Equal(error.Message + "\n  totl\n  ^~~~", error.ToDisplayString(Text));
    }

    // A text longer than the one compiled is accepted: the offsets of the fragment are what the mark follows.
    [Fact]
    public void ToDisplayString_WithALongerText_MarksTheSameOffsets()
    {
        CelError error = Assert.Single(Environment.Compile<CelValue>("ok && totl").Errors);

        Assert.Equal(error.Message + "\nok && totl and more\n      ^~~~", error.ToDisplayString("ok && totl and more"));
    }

    [Fact]
    public void ToString_WithTheStartOfTheExpressionInADocument_HasThePositionInTheDocument()
    {
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("rules.json", 400, 12, 30) };

        CelError error = Assert.Single(Environment.Compile<CelValue>("ok && totl", options).Errors);

        Assert.Equal("UndeclaredReference at rules.json:12:36: " + error.Message, error.ToString());
        Assert.Equal("1:7", error.Location.ToString());
        Assert.Equal(error.Location, error.Span.Start);
        Assert.Equal(4, error.Span.Length);
    }

    [Fact]
    public void EvaluationError_InADocument_KeepsItsFragmentInTheExpression()
    {
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("rules.json", 400, 12, 30) };
        CelEvalResult<long> result = Environment.Compile<long>("x / 0", options).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);

        Assert.Equal(CelErrorCode.DivisionByZero, error.Code);
        Assert.Equal((2, 3), (error.Span.Start.Offset, error.Span.Length));
        Assert.Equal("DivisionByZero at rules.json:12:32: division by zero", error.ToString());
        Assert.Equal("division by zero\nx / 0\n  ^~~", error.ToDisplayString("x / 0"));
    }

    [Fact]
    public void SourceLocation_WithoutALine_IsEmpty_AndWithoutASourceName_IsLineAndColumn()
    {
        Assert.Equal(string.Empty, default(CelSourceLocation).ToString());
        Assert.Equal("3:9", new CelSourceLocation(20, 3, 9).ToString());
    }
}

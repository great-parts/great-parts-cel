using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The members for a parser of another language: CelExprFactory.CreateError, CelParseResult.FromParsedExpr and FromErrors, and
// CelCompileResult<T>.FromErrors.
[Trait("Requirement", "F-32")]
public sealed class ForeignParserTests
{
    private const string Filter = "pages > 1e2";

    private static CelError SomeError(string message) => new CelExprFactory().CreateError(CelErrorCode.SyntaxError, message);

    [Fact]
    public void CreateError_AtThePosition_HasTheSpanAndTheParts()
    {
        var factory = new CelExprFactory(Filter)
        {
            Position = 8..11,
        };

        CelError error = factory.CreateError(CelErrorCode.InvalidFilterValue, "not an integer", "pages", CelType.Int);

        Assert.Equal(CelErrorCode.InvalidFilterValue, error.Code);
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal(8, error.Span.Start.Offset);
        Assert.Equal(3, error.Span.Length);
        Assert.Equal(1, error.Location.Line);
        Assert.Equal(9, error.Location.Column);
        Assert.Equal("pages", error.Subject);
        Assert.Equal(CelType.Int, error.ExpectedType);
        Assert.Null(error.ActualType);
        Assert.Null(error.Exception);
        Assert.Equal("not an integer\npages > 1e2\n        ^~~", error.ToDisplayString(Filter));
    }

    [Fact]
    public void CreateError_OnALaterLine_HasTheLineAndTheColumnOfTheText()
    {
        const string Text = "a\r\nbb cc";
        var factory = new CelExprFactory(Text)
        {
            Position = 6..8,
        };

        CelError error = factory.CreateError(CelErrorCode.FilterSyntaxError, "unexpected");

        Assert.Equal(2, error.Location.Line);
        Assert.Equal(4, error.Location.Column);
        Assert.Equal(2, error.Span.Length);
    }

    [Fact]
    public void CreateError_WithoutPosition_HasTheStageOfTheCodeAndNoPlace()
    {
        var factory = new CelExprFactory(Filter);

        CelError error = factory.CreateError(CelErrorCode.FilterSyntaxError, "unexpected");

        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Equal(default, error.Span);
        Assert.Equal(default, error.Location);
        Assert.Equal("unexpected", error.ToDisplayString(Filter));
    }

    [Fact]
    public void CreateError_OfAFactoryWithoutText_HasNoPlace()
    {
        CelError error = new CelExprFactory().CreateError(CelErrorCode.SyntaxError, "unexpected");

        Assert.Equal(default, error.Span);
        Assert.Equal(default, error.Location);
    }

    [Fact]
    public void CreateError_AtTheEndOfTheText_HasALengthOfZero()
    {
        var factory = new CelExprFactory(Filter)
        {
            Position = Filter.Length..Filter.Length,
        };

        CelError error = factory.CreateError(CelErrorCode.FilterSyntaxError, "the end");

        Assert.Equal(Filter.Length, error.Span.Start.Offset);
        Assert.Equal(0, error.Span.Length);
    }

    [Fact]
    public void CreateError_TakesTheStageFromTheRangeOfTheCode()
    {
        var factory = new CelExprFactory();

        Assert.Equal(CelErrorStage.Parse, factory.CreateError(CelErrorCode.ExpressionTooLong, "m").Stage);
        Assert.Equal(CelErrorStage.Check, factory.CreateError(CelErrorCode.UndeclaredReference, "m").Stage);
        Assert.Equal(CelErrorStage.Evaluate, factory.CreateError(CelErrorCode.EvalNoMatchingOverload, "m").Stage);
        Assert.Equal(CelErrorStage.Build, factory.CreateError(CelErrorCode.DuplicateVariable, "m").Stage);
    }

    [Fact]
    public void CreateError_OfACodeWithoutAStage_IsAnArgumentException()
    {
        var factory = new CelExprFactory();

        Assert.Throws<ArgumentException>(() => factory.CreateError(CelErrorCode.InternalError, "m"));
        Assert.Throws<ArgumentException>(() => factory.CreateError((CelErrorCode)12345, "m"));
        Assert.Throws<ArgumentException>(() => factory.CreateError(CelErrorCode.SyntaxError, string.Empty));
        Assert.Throws<ArgumentNullException>(() => factory.CreateError(CelErrorCode.SyntaxError, null!));
    }

    [Fact]
    public void FromParsedExpr_IsASuccessWithTheSameTree()
    {
        CelParsedExpr tree = TreeEnvironments.Env.Parse("x").GetParsedExprOrThrow();

        CelParseResult result = CelParseResult.FromParsedExpr(tree);

        Assert.True(result.IsSuccess);
        Assert.Same(tree, result.ParsedExpr);
        Assert.Empty(result.Errors);
        Assert.Throws<ArgumentNullException>(() => CelParseResult.FromParsedExpr(null!));
    }

    [Fact]
    public void FromErrors_KeepsTheErrorsInOrderAndThrowsThemAll()
    {
        CelError first = SomeError("first");
        CelError second = SomeError("second");

        CelParseResult result = CelParseResult.FromErrors([first, second]);

        Assert.False(result.IsSuccess);
        Assert.Equal<CelError>(new[] { first, second }, result.Errors);
        CelException exception = Assert.Throws<CelException>(result.GetParsedExprOrThrow);
        Assert.Equal<CelError>(new[] { first, second }, exception.Errors);
    }

    [Fact]
    public void FromErrors_WithoutErrorsOrWithANull_IsAnArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CelParseResult.FromErrors([]));
        Assert.Throws<ArgumentNullException>(() => CelParseResult.FromErrors([null!]));
        Assert.Throws<ArgumentNullException>(() => CelParseResult.FromErrors(null!));
        Assert.Throws<ArgumentException>(() => CelCompileResult<bool>.FromErrors([]));
        Assert.Throws<ArgumentNullException>(() => CelCompileResult<bool>.FromErrors([null!]));
        Assert.Throws<ArgumentNullException>(() => CelCompileResult<bool>.FromErrors(null!));
    }

    [Fact]
    public void CompileResultFromErrors_IsUnsuccessfulWithoutAProgram()
    {
        CelError error = SomeError("only");

        CelCompileResult<bool> result = CelCompileResult<bool>.FromErrors([error]);

        Assert.False(result.IsSuccess);
        Assert.Equal<CelError>(new[] { error }, result.Errors);
        CelException exception = Assert.Throws<CelException>(result.GetProgramOrThrow);
        Assert.Equal<CelError>(new[] { error }, exception.Errors);
        Assert.Throws<CelException>(() => result.Program);
    }
}

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// Type checking ends with a result for any parsed expression: when the stack is nearly used up it reports
// InsufficientStack, and an unexpected exception inside it becomes InternalError, both with the stage Check,
// after the errors found before. The text is parsed with the full stack; only checking runs on the short one.
public sealed class CheckLimitTests
{
    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    // Checking stops at the node it could not go into; the undeclared y inside is never reached.
    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void CheckingWithoutStack_IsInsufficientStackAtTheNodeNotChecked()
    {
        const string Text = "[[[y]]]";
        ParsedExpression parsed = Parse(Text);

        CheckResult result = CheckOnExhaustedStack(parsed);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InsufficientStack, error.Code);
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal("insufficient execution stack to continue type checking", error.Message);
        Assert.Contains(Text[error.Location.Offset], "[y");
        Assert.Equal((1, error.Location.Offset + 1), (error.Location.Line, error.Location.Column));
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(environment.Check(parsed).Errors).Code);
    }

    // A tree that the parser never builds — a call whose argument array is not initialized — makes the
    // checker throw. The exception becomes InternalError at the call, after the error found before it,
    // with the original exception kept.
    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void ExceptionInsideTheChecker_IsInternalErrorAtTheNodeBeingChecked()
    {
        const string Text = "[y, size()]";
        var factory = new NodeFactory();
        ExprNode y = factory.Ident("y", 1);
        ExprNode broken = factory.Call("size", null, default(ImmutableArray<ExprNode>), 8);
        ParsedExpression parsed = factory.Build(factory.List(ImmutableArray.Create(y, broken), ImmutableArray<int>.Empty, 0), new LineTable(Text));

        CheckResult result = environment.Check(parsed);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Expression);
        Assert.Equal(
            new[] { (CelErrorCode.UndeclaredReference, 1), (CelErrorCode.InternalError, 8) },
            result.Errors.Select(error => (error.Code, error.Location.Offset)));
        CelError error = result.Errors[1];
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal("internal error during type checking; the original exception is in CelError.Exception", error.Message);
        Assert.Equal((1, 9), (error.Location.Line, error.Location.Column));
        Assert.NotNull(error.Exception);
        Assert.IsNotType<OutOfMemoryException>(error.Exception);
    }

    private ParsedExpression Parse(string text)
    {
        ParseResult result = environment.Parse(text);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Expression);
        return result.Expression;
    }

    // Descends until the runtime reports that the stack is nearly used up, then checks there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private CheckResult CheckOnExhaustedStack(ParsedExpression parsed)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return environment.Check(parsed);
        }

        Span<byte> frame = stackalloc byte[1024];
        frame.Fill(1);
        CheckResult result = CheckOnExhaustedStack(parsed);
        Assert.Equal((byte)1, frame[^1]);
        return result;
    }
}

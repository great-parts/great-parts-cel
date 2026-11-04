using System;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Calls of application functions at evaluation and their errors. A typed delegate added with AddFunction
// has the overload identifier <name>_<parameter types>, such as boom_int. An exception of application code
// becomes ApplicationException with the original exception; an argument that the .NET parameter cannot
// take is ArgumentConversion; a result whose kind does not fit the declared result type is
// ImplementationResultMismatch. All three are error values at the call, the "(" after the name. Types of
// .NET in messages are written by Type.ToString().
public sealed class ApplicationFunctionTests
{
    private static readonly InvalidOperationException Thrown = new("the application failed");

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("i")
        .AddFunction<long, long>("boom", _ => throw Thrown)
        .AddFunction<int, long>("narrow", value => value)
        .AddFunction<string, string?>("blank", _ => null)
        .AddFunction(new CelFunctionDecl("explode", CelOverloadDecl.Global("explode_int", [CelType.Int], CelType.Int)))
        .AddFunctionImplementation("explode_int", _ => throw Thrown)
        .AddFunction(new CelFunctionDecl(
            "costly",
            CelOverloadDecl.Global("costly_int", [CelType.Int], CelType.Int).WithCost(_ => throw Thrown)))
        .AddFunctionImplementation("costly_int", arguments => arguments[0])
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("i", 1L << 40);

    [Theory]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    [InlineData("boom(i)", "overload 'boom_int' of function 'boom'", 4)]
    [InlineData("1 + explode(i)", "overload 'explode_int' of function 'explode'", 11)]
    public void ExceptionOfTheImplementation_IsApplicationExceptionWithTheException(string expression, string callee, int offset)
    {
        CelError error = SingleError(expression, null);

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.InvalidOperationException in " + callee, error.Message);
        Assert.Same(Thrown, error.Exception);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // The cost function runs only with a cost limit; its exception is ApplicationException as well.
    [Fact]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "R-3")]
    public void ExceptionOfTheCostFunction_IsApplicationExceptionUnderALimit()
    {
        CelError error = SingleError("costly(i)", 1000);

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.InvalidOperationException in the cost function of overload 'costly_int' of function 'costly'", error.Message);
        Assert.Same(Thrown, error.Exception);
        Assert.Equal(6, error.Location.Offset);
        Assert.True(Evaluate("costly(i)", null).IsSuccess);
    }

    // OperationCanceledException of a token other than the one of the evaluation is an exception of the
    // application like any other.
    [Fact]
    [Trait("Requirement", "+R-9")]
    public void CancellationOfAnotherToken_IsApplicationException()
    {
        using var other = new CancellationTokenSource();
        other.Cancel();
        CelEnvironment canceling = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<long, long>("wait", _ => throw new OperationCanceledException(other.Token))
            .Build()
            .GetEnvironmentOrThrow();

        CelError error = Assert.Single(canceling.Compile<long>("wait(1)").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.OperationCanceledException in overload 'wait_int' of function 'wait'", error.Message);
        Assert.IsType<OperationCanceledException>(error.Exception);
    }

    // The error is a value: || absorbs it.
    [Fact]
    [Trait("Requirement", "+R-9")]
    public void ApplicationException_IsAbsorbedByOr()
    {
        Assert.True(environment.Compile<bool>("boom(i) == 0 || true").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // narrow takes System.Int32: 2^40 does not fit it, 5 does.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void ArgumentThatTheParameterCannotTake_IsArgumentConversion()
    {
        CelError error = SingleError("narrow(i)", null);

        Assert.Equal(CelErrorCode.ArgumentConversion, error.Code);
        Assert.Equal("argument '1' of overload 'narrow_int' of function 'narrow' cannot be converted from 'int' to .NET type 'System.Int32'", error.Message);
        Assert.Equal((6, 1, 7), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Equal(5L, environment.Compile<long>("narrow(5)").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The kind of the result is checked against the declared result type: null — what a .NET null string
    // becomes — is not a string. A string for int and a result of the type dyn are stated in
    // Values/ValueTypeTests.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void NullResultOfAStringFunction_IsImplementationResultMismatch()
    {
        CelError error = SingleError("blank('a')", null);

        Assert.Equal(CelErrorCode.ImplementationResultMismatch, error.Code);
        Assert.Equal(
            "implementation of overload 'blank_string' of function 'blank' returned a value of type 'null_type', expected 'string'",
            error.Message);
        Assert.Equal((5, 1, 6), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private CelEvalResult<CelValue> Evaluate(string expression, ulong? limit) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);

    private CelError SingleError(string expression, ulong? limit)
    {
        CelError error = Assert.Single(Evaluate(expression, limit).Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Null(error.SourceLocation);
        return error;
    }
}

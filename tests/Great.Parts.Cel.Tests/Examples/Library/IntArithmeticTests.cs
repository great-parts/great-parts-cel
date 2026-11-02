using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The overloads add_int64, divide_int64 and negate_int64 of the standard library, version 1. The
// operands are variables, so the operations run at evaluation.
public sealed class IntArithmeticTests
{
    private readonly ExampleVariables programs = new();

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("x + 1", 42L)]
    [InlineData("x + -50", -9L)]
    [InlineData("max + min", -1L)]
    [InlineData("x / 2", 20L)]
    [InlineData("-x / 2", -20L)]
    [InlineData("x / -2", -20L)]
    [InlineData("min / 1", long.MinValue)]
    [InlineData("-x", -41L)]
    [InlineData("-(-max)", long.MaxValue)]
    public void Arithmetic_GivesTheIntResult(string expression, long expected)
    {
        Assert.Equal(expected, programs.Evaluate(expression).GetValueOrThrow());
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("max + 1", CelErrorCode.Overflow, "integer overflow", 4, 1, 5)]
    [InlineData("x\n+ max", CelErrorCode.Overflow, "integer overflow", 2, 2, 1)]
    [InlineData("min / -1", CelErrorCode.Overflow, "integer overflow", 4, 1, 5)]
    [InlineData("-min", CelErrorCode.Overflow, "integer overflow", 0, 1, 1)]
    [InlineData("x / zero", CelErrorCode.DivisionByZero, "division by zero", 2, 1, 3)]
    [InlineData("1 + x / zero", CelErrorCode.DivisionByZero, "division by zero", 6, 1, 7)]
    public void FailedOperation_IsAnErrorAtItsCall(string expression, CelErrorCode code, string message, int offset, int line, int column)
    {
        CelEvalResult<long> result = programs.Evaluate(expression);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((code, CelErrorStage.Evaluate, message), (error.Code, error.Stage, error.Message));
        Assert.Equal((offset, line, column), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // One environment with the variables of the examples and their values.
    private sealed class ExampleVariables
    {
        private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddVariable<long>("zero")
            .AddVariable<long>("max")
            .AddVariable<long>("min")
            .Build()
            .GetEnvironmentOrThrow();

        private readonly CelBindings bindings = new CelBindings()
            .Add("x", 41L)
            .Add("zero", 0L)
            .Add("max", long.MaxValue)
            .Add("min", long.MinValue);

        public CelEvalResult<long> Evaluate(string expression) =>
            environment.Compile<long>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
    }
}

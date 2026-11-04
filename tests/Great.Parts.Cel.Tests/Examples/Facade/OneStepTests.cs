using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The one-step call CelEnvironment.Evaluate<T>: the text and the bindings in, the value or the errors out.
// Its result and its errors are those of Compile<T> with the same compile options followed by
// CelProgram<T>.Evaluate with the same bindings, evaluation options and token; the stage of each error
// tells compilation from evaluation. Compilation is not charged: the cost limit applies to the evaluation.
[Trait("Requirement", "+R-13")]
public sealed class OneStepTests
{
    private static readonly CelCompileOptions InDocument = new() { SourceLocation = new CelSourceLocation("rules.yaml", 10, 2, 5) };

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("x", 41L).Add("d", "text");

    // A value; a parse error; a check error; an evaluation error; an expected type that the result does
    // not fit; a result that T cannot hold; an exceeded cost limit and the same text within the limit.
    // The value is read only from a successful result: reading it from a failed one throws.
    [Theory]
    [InlineData("x + 1", null)]
    [InlineData("x +", null)]
    [InlineData("y + 1", null)]
    [InlineData("x / 0", null)]
    [InlineData("'a'", null)]
    [InlineData("d", null)]
    [InlineData("x + 1 + 1", 2UL)]
    [InlineData("x + 1 + 1", 3UL)]
    public void OneStep_GivesTheResultOfCompileAndEvaluate(string expression, ulong? limit)
    {
        var evalOptions = new CelEvalOptions { CostLimit = limit };

        CelEvalResult<long> oneStep = environment.Evaluate<long>(expression, bindings, InDocument, evalOptions, CancellationToken.None);
        CelCompileResult<long> compiled = environment.Compile<long>(expression, InDocument);
        if (!compiled.IsSuccess)
        {
            Assert.False(oneStep.IsSuccess);
            Assert.Equal(compiled.Errors.Select(Describe), oneStep.Errors.Select(Describe));
            return;
        }

        CelEvalResult<long> twoSteps = compiled.GetProgramOrThrow().Evaluate(bindings, evalOptions, CancellationToken.None);

        Assert.Equal(twoSteps.IsSuccess, oneStep.IsSuccess);
        if (twoSteps.IsSuccess)
        {
            Assert.Equal(twoSteps.Value, oneStep.Value);
        }

        Assert.Equal(twoSteps.Errors.Select(Describe), oneStep.Errors.Select(Describe));
    }

    [Fact]
    public void ErrorStage_TellsCompilationFromEvaluation()
    {
        Assert.Equal(CelErrorStage.Parse, environment.Evaluate<long>("x +", bindings, cancellationToken: CancellationToken.None).Errors[0].Stage);
        Assert.Equal(CelErrorStage.Check, environment.Evaluate<long>("y", bindings, cancellationToken: CancellationToken.None).Errors[0].Stage);
        Assert.Equal(CelErrorStage.Evaluate, environment.Evaluate<long>("x / 0", bindings, cancellationToken: CancellationToken.None).Errors[0].Stage);
        Assert.Equal(42L, environment.Evaluate<long>("x + 1", bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // "1 + 1" costs 1, for the addition: the limit 1 is enough, whatever compiling the text takes.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void Compilation_IsNotCharged()
    {
        Assert.Equal(2L, environment.Evaluate<long>("1 + 1", bindings, evalOptions: new CelEvalOptions { CostLimit = 1 }, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(
            CelErrorCode.CostLimitExceeded,
            Assert.Single(environment.Evaluate<long>("1 + 1", bindings, evalOptions: new CelEvalOptions { CostLimit = 0 }, cancellationToken: CancellationToken.None).Errors).Code);
    }

    // The exceptions of Compile<T> pass through: T outside the type mapping table, and an expected type
    // that T cannot hold.
    [Fact]
    public void ExceptionsOfCompile_PassThrough()
    {
        var expectsString = new CelCompileOptions { ExpectedType = CelType.String };

        Assert.Throws<ArgumentException>(() => environment.Compile<short>("1"));
        Assert.Throws<ArgumentException>(() => environment.Evaluate<short>("1", bindings, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentException>(() => environment.Compile<long>("1", expectsString));
        Assert.Throws<ArgumentException>(() => environment.Evaluate<long>("1", bindings, expectsString, cancellationToken: CancellationToken.None));
    }

    // The token is checked before the compilation: a canceled token gives OperationCanceledException even
    // for a text that does not parse and for one without macros and application functions.
    [Theory]
    [Trait("Requirement", "+R-11")]
    [InlineData("x +")]
    [InlineData("x + 1")]
    public void CanceledToken_StopsTheCallBeforeTheCompilation(string expression)
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => environment.Evaluate<long>(expression, bindings, cancellationToken: source.Token));
    }

    private static (CelErrorCode, CelErrorStage, string, CelSourceLocation, CelSourceLocation?) Describe(CelError error) =>
        (error.Code, error.Stage, error.Message, error.Location, error.SourceLocation);
}

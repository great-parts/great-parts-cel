using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// CelEnvironment.Evaluate<T>: Compile<T> followed by CelProgram<T>.Evaluate, with the token checked before the
// compilation and between the two steps (docs/design/facade.md, "One step and the compilation cache").
public sealed class OneStepCallTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("n")
        .AddVariable("any", CelType.Dyn)
        .AddFunction("probe", (long value) => value)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings Bindings = new CelBindings().Add("n", 5L).Add("any", 2UL);

    private static readonly CelCompileOptions InSource = new() { SourceLocation = new CelSourceLocation("rules.json", 40, 3, 7) };

    // A value; errors of parsing, checking and evaluation; a result conversion; a cost limit passed and kept.
    [Theory]
    [InlineData("n * 2", null)]
    [InlineData("n *", null)]
    [InlineData("m + 1", null)]
    [InlineData("n % 0", null)]
    [InlineData("any", null)]
    [InlineData("n + n + n", 1UL)]
    [InlineData("n + n + n", 4UL)]
    public static void OneStep_IsCompileFollowedByEvaluate(string expression, ulong? limit)
    {
        var evalOptions = new CelEvalOptions { CostLimit = limit };

        CelEvalResult<long> oneStep = Environment.Evaluate<long>(expression, Bindings, InSource, evalOptions, CancellationToken.None);
        CelCompileResult<long> compiled = Environment.Compile<long>(expression, InSource);
        string twoSteps = compiled.IsSuccess
            ? Describe(compiled.Program.Evaluate(Bindings, evalOptions, CancellationToken.None))
            : Describe(compiled.Errors);

        Assert.Equal(twoSteps, Describe(oneStep));
    }

    [Fact]
    public void Value_IsTheResultOfTheExpression()
    {
        Assert.Equal(10L, Environment.Evaluate<long>("n * 2", Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void CanceledToken_ThrowsBeforeTheCompilation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        // The text has a parse error, which the compilation would return.
        Assert.ThrowsAny<OperationCanceledException>(() => Environment.Evaluate<long>("n *", Bindings, cancellationToken: source.Token));
    }

    // The token is canceled while the text compiles: the check between the steps throws before the evaluation,
    // which would call probe.
    [Fact]
    public void TokenCanceledDuringTheCompilation_ThrowsBeforeTheEvaluation()
    {
        using var source = new CancellationTokenSource();
        int calls = 0;
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("probe", (long value) =>
            {
                calls++;
                return value;
            })
            .Build()
            .GetEnvironmentOrThrow();
        CelCompileResult<long> compiled = environment.Compile<long>("probe(1)");
        source.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(
            () => CelEnvironment.EvaluateCompiled(compiled, new CelBindings(), null, source.Token));
        Assert.Equal(0, calls);
        Assert.Equal(1L, CelEnvironment.EvaluateCompiled(compiled, new CelBindings(), null, CancellationToken.None).GetValueOrThrow());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ExceptionsOfCompile_PassThrough()
    {
        Assert.Throws<ArgumentException>(() => Environment.Evaluate<Guid>("n", Bindings, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentException>(
            () => Environment.Evaluate<long>("n", Bindings, new CelCompileOptions { ExpectedType = CelType.Bool }, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void NullArguments_ThrowArgumentNullException()
    {
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Environment.Evaluate<long>(null!, Bindings, cancellationToken: TestContext.Current.CancellationToken)).ParamName);
        Assert.Equal("bindings", Assert.Throws<ArgumentNullException>(() => Environment.Evaluate<long>("n", null!, cancellationToken: TestContext.Current.CancellationToken)).ParamName);
    }

    private static string Describe(CelEvalResult<long> result) =>
        result.IsSuccess ? "value " + result.Value.ToString(CultureInfo.InvariantCulture) : Describe(result.Errors);

    private static string Describe(IEnumerable<CelError> errors) => string.Join("; ", errors.Select(Describe));

    private static string Describe(CelError error) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{error.Code} {error.Stage} {error.Message} {error.Location.Offset}:{error.Location.Line}:{error.Location.Column} {error.SourceLocation?.SourceName} {error.SourceLocation?.Offset}:{error.SourceLocation?.Line}:{error.SourceLocation?.Column}");
}

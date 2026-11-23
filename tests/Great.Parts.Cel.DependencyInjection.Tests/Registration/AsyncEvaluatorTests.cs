using System;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Great.Parts.Cel.DependencyInjection.Tests.Registration;

// The asynchronous one-step calls of the evaluator: they delegate to the compilation cache, wait for the asynchronous functions of the
// environment, and a replacement of the evaluator in a test implements both members.
[Trait("Requirement", "F-16")]
[Trait("Requirement", "F-23")]
public sealed class AsyncEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_WaitsForTheAsynchronousFunctionAndCompilesOnce()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder.AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1)))
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        CelEvalResult<long> first = await evaluator.EvaluateAsync<long>("inc(1)", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        CelEvalResult<long> second = await evaluator.EvaluateAsync<long>("inc(1)", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(2L, first.GetValueOrThrow());
        Assert.Equal(2L, second.GetValueOrThrow());
        Assert.Equal(1, provider.GetRequiredService<CelCompilationCache>().Count);
    }

    [Fact]
    public async Task EvaluateInputAsync_ReadsTheInputAndWaitsForTheFunction()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder
                .AddTypes(ShopContext.Default)
                .UseInput<Order>()
                .AddVariable<double>("limit")
                .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1)))
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        CelEvalResult<bool> result = await evaluator.EvaluateInputAsync<bool>(
            "total > limit && inc(1) == 2",
            new Order { Total = 10 },
            new CelBindings().Add("limit", 5.0),
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.True(result.GetValueOrThrow());
    }

    // A synchronous call of the same evaluator for an expression that calls the asynchronous function is the evaluation error.
    [Fact]
    public void Evaluate_OfAnExpressionThatCallsAnAsynchronousFunction_IsAsyncEvaluationRequired()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder.AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1)))
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        CelEvalResult<long> result = evaluator.Evaluate<long>("inc(1)", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CelErrorCode.AsyncEvaluationRequired, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ReplacementOfTheEvaluator_ImplementsBothAsynchronousMembers()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder.AddVariable<long>("x"))
            .AddSingleton<ICelEvaluator>(static services => new FixedEvaluator(services.GetRequiredService<CelEnvironment>()))
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        CelEvalResult<long> result = await evaluator.EvaluateAsync<long>("anything", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        CelEvalResult<bool> input = await evaluator.EvaluateInputAsync<bool>("anything", new object(), cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(7L, result.GetValueOrThrow());
        Assert.True(input.GetValueOrThrow());
    }

    // The evaluator of a test: the asynchronous calls give fixed results.
    private sealed class FixedEvaluator(CelEnvironment environment) : ICelEvaluator
    {
        public CelEnvironment Environment => environment;

        public CelEvalResult<T> Evaluate<T>(string expression, CelBindings bindings, CelCompileOptions? compileOptions = null, CelEvalOptions? evalOptions = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public CelEvalResult<T> EvaluateInput<T>(string expression, object input, CelBindings? bindings = null, CelCompileOptions? compileOptions = null, CelEvalOptions? evalOptions = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public CelValidationResult Validate<T>(string expression, CelCompileOptions? options = null) =>
            throw new NotSupportedException();

        public ValueTask<CelEvalResult<T>> EvaluateAsync<T>(string expression, CelBindings bindings, CelCompileOptions? compileOptions = null, CelEvalOptions? evalOptions = null, CancellationToken cancellationToken = default) =>
            environment.EvaluateAsync<T>("7", bindings, cancellationToken: cancellationToken);

        public ValueTask<CelEvalResult<T>> EvaluateInputAsync<T>(string expression, object input, CelBindings? bindings = null, CelCompileOptions? compileOptions = null, CelEvalOptions? evalOptions = null, CancellationToken cancellationToken = default) =>
            environment.EvaluateAsync<T>("true", new CelBindings(), cancellationToken: cancellationToken);
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// CelCompilationCache keeps the compile results of one environment, by the text, T and the compile options.
// A hit returns the same result, a failed compilation included; a text longer than the capacity of the
// total length is compiled and not kept; eviction never changes a result. Its Evaluate is the one-step call
// with the cache in place of Compile<T>.
public sealed class CompilationCacheTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("x", 41L);

    [Fact]
    [Trait("Requirement", "P-2")]
    public void SameTextAndOptions_GiveTheSameResult()
    {
        var cache = new CelCompilationCache(environment, 10, 1000);

        Assert.Same(cache.GetOrCompile<long>("x + 1"), cache.GetOrCompile<long>("x + 1"));
        Assert.Same(cache.GetOrCompile<long>("x + 1", null), cache.GetOrCompile<long>("x + 1", new CelCompileOptions()));
        Assert.NotSame(cache.GetOrCompile<long>("x + 1"), cache.GetOrCompile<long>("x + 1 "));
    }

    [Fact]
    [Trait("Requirement", "P-2")]
    public void FailedCompilation_IsKeptToo()
    {
        var cache = new CelCompilationCache(environment, 10, 1000);

        CelCompileResult<long> first = cache.GetOrCompile<long>("x +");

        Assert.False(first.IsSuccess);
        Assert.Same(first, cache.GetOrCompile<long>("x +"));
    }

    // The source location is part of the key: the errors of each result carry their own.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void OtherCompileOptions_GiveAnotherResult()
    {
        var cache = new CelCompilationCache(environment, 10, 1000);
        var inA = new CelCompileOptions { SourceLocation = new CelSourceLocation("a.yaml", 0, 1, 1) };
        var inB = new CelCompileOptions { SourceLocation = new CelSourceLocation("b.yaml", 0, 1, 1) };

        CelError errorInA = cache.GetOrCompile<long>("y", inA).Errors[0];
        CelError errorInB = cache.GetOrCompile<long>("y", inB).Errors[0];

        Assert.Equal("a.yaml", errorInA.SourceLocation?.SourceName);
        Assert.Equal("b.yaml", errorInB.SourceLocation?.SourceName);
    }

    // The capacity of the total length is 10: a text of 11 code units is compiled each time and not kept.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void TextLongerThanTheCapacity_IsCompiledAndNotKept()
    {
        var cache = new CelCompilationCache(environment, 10, 10);
        const string Long = "x + 1 + 100";

        CelCompileResult<long> first = cache.GetOrCompile<long>(Long);

        Assert.True(first.IsSuccess);
        Assert.NotSame(first, cache.GetOrCompile<long>(Long));
    }

    // Two entries at most: the third text evicts one of the first two, and every text keeps its result.
    [Fact]
    [Trait("Requirement", "R-4")]
    public void Eviction_DoesNotChangeResults()
    {
        var cache = new CelCompilationCache(environment, 2, 1000);
        string[] texts = ["x + 1", "x + 2", "x + 3", "x + 1", "x + 2", "x + 3"];
        long[] expected = [42, 43, 44, 42, 43, 44];

        for (int index = 0; index < texts.Length; index++)
        {
            Assert.Equal(expected[index], cache.Evaluate<long>(texts[index], bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        }
    }

    // The same as the one-step call of the environment: value, compile errors, evaluation errors, limit.
    [Theory]
    [Trait("Requirement", "+R-13")]
    [InlineData("x + 1", null)]
    [InlineData("x +", null)]
    [InlineData("x / 0", null)]
    [InlineData("x + 1", 1UL)]
    public void Evaluate_GivesTheResultOfTheOneStepCall(string expression, ulong? limit)
    {
        var cache = new CelCompilationCache(environment, 10, 1000);
        var options = new CelEvalOptions { CostLimit = limit };

        CelEvalResult<long> cached = cache.Evaluate<long>(expression, bindings, evalOptions: options, cancellationToken: CancellationToken.None);
        CelEvalResult<long> direct = environment.Evaluate<long>(expression, bindings, evalOptions: options, cancellationToken: CancellationToken.None);

        Assert.Equal(Describe(direct), Describe(cached));
    }

    // An exception of Compile<T> passes through and keeps nothing; the capacities are at least 1.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void ConditionsOfTheCall_AreChecked()
    {
        var cache = new CelCompilationCache(environment, 10, 1000);

        Assert.Throws<ArgumentException>(() => cache.GetOrCompile<long>("1", new CelCompileOptions { ExpectedType = CelType.String }));
        Assert.Throws<ArgumentNullException>(() => new CelCompilationCache(null!, 10, 1000));
        Assert.ThrowsAny<ArgumentException>(() => new CelCompilationCache(environment, 0, 1000));
        Assert.ThrowsAny<ArgumentException>(() => new CelCompilationCache(environment, 10, 0));
    }

    private static string Describe(CelEvalResult<long> result) =>
        result.IsSuccess
            ? "value " + result.Value.ToString(CultureInfo.InvariantCulture)
            : string.Join("; ", result.Errors.Select(error =>
                error.Code + " " + error.Stage + " " + error.Message + " at " + error.Location.Offset.ToString(CultureInfo.InvariantCulture)));
}

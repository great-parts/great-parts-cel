using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// CelCompilationCache (docs/design/facade.md, "One step and the compilation cache"): the key, the two capacities with
// eviction at insertion, failed compilations kept, and calls from many threads.
public sealed class CelCompilationCacheTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("n")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings Bindings = new CelBindings().Add("n", 5L);

    [Fact]
    public void Key_IsTheTextTheResultTypeAndTheOptionValues()
    {
        var cache = new CelCompilationCache(Environment, 100, 10_000);
        CelCompileResult<long> plain = cache.GetOrCompile<long>("n + 1");

        Assert.Same(plain, cache.GetOrCompile<long>("n + 1", new CelCompileOptions()));
        Assert.Same(
            cache.GetOrCompile<long>("n + 1", new CelCompileOptions { ExpectedType = CelType.Int }),
            cache.GetOrCompile<long>("n + 1", new CelCompileOptions { ExpectedType = CelType.Int }));
        Assert.Same(
            cache.GetOrCompile<long>("n", new CelCompileOptions { SourceLocation = new CelSourceLocation("a", 1, 1, 2) }),
            cache.GetOrCompile<long>("n", new CelCompileOptions { SourceLocation = new CelSourceLocation("a", 1, 1, 2) }));
        Assert.NotSame(plain, cache.GetOrCompile<long>("n + 1", new CelCompileOptions { ExpectedType = CelType.Int }));
        Assert.NotSame(plain, cache.GetOrCompile<long>("n + 1", new CelCompileOptions { SourceLocation = new CelSourceLocation("a", 0, 1, 1) }));
        Assert.Equal(6L, cache.GetOrCompile<object>("n + 1").Program?.Evaluate(Bindings, cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
        Assert.Equal((5, 21L), cache.Kept);
    }

    [Fact]
    public void FailedCompilation_IsKept()
    {
        var cache = new CelCompilationCache(Environment, 10, 100);
        CelCompileResult<long> failed = cache.GetOrCompile<long>("n +");

        Assert.Equal(CelErrorStage.Parse, failed.Errors[0].Stage);
        Assert.Same(failed, cache.GetOrCompile<long>("n +"));
    }

    // Three texts into two entries: the third evicts one, and the cache never holds more than two.
    [Fact]
    public void EntryCount_EvictsAtTheInsertion()
    {
        var cache = new CelCompilationCache(Environment, 2, 10_000);

        for (int index = 1; index <= 6; index++)
        {
            string text = "n + " + index.ToString(CultureInfo.InvariantCulture);
            Assert.Equal(5L + index, cache.Evaluate<long>(text, Bindings, cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
            Assert.Equal(Math.Min(index, 2), cache.Kept.Count);
        }
    }

    // Texts of 5 code units into a total of 12: two fit, a third evicts one of them.
    [Fact]
    public void TotalTextLength_EvictsAtTheInsertion()
    {
        var cache = new CelCompilationCache(Environment, 100, 12);

        _ = cache.GetOrCompile<long>("n + 1");
        _ = cache.GetOrCompile<long>("n + 2");
        Assert.Equal((2, 10L), cache.Kept);

        _ = cache.GetOrCompile<long>("n + 3");
        Assert.Equal((2, 10L), cache.Kept);
    }

    // A text read since the hand last passed is kept, so the entry evicted is the one not read.
    [Fact]
    public void EntryReadAgain_OutlivesAnEntryNotRead()
    {
        var cache = new CelCompilationCache(Environment, 2, 10_000);
        CelCompileResult<long> first = cache.GetOrCompile<long>("n + 1");
        CelCompileResult<long> second = cache.GetOrCompile<long>("n + 2");

        Assert.Same(first, cache.GetOrCompile<long>("n + 1"));
        _ = cache.GetOrCompile<long>("n + 3");

        Assert.Same(first, cache.GetOrCompile<long>("n + 1"));
        Assert.NotSame(second, cache.GetOrCompile<long>("n + 2"));
    }

    [Fact]
    public void TextLongerThanTheTotalLength_IsCompiledAndNotKept()
    {
        var cache = new CelCompilationCache(Environment, 10, 4);
        CelCompileResult<long> first = cache.GetOrCompile<long>("n + 1");

        Assert.True(first.IsSuccess);
        Assert.NotSame(first, cache.GetOrCompile<long>("n + 1"));
        Assert.Equal((0, 0L), cache.Kept);
    }

    [Fact]
    public void ExceptionOfCompile_PassesThroughAndKeepsNothing()
    {
        var cache = new CelCompilationCache(Environment, 10, 100);

        Assert.Throws<ArgumentException>(() => cache.GetOrCompile<short>("n"));
        Assert.Throws<ArgumentException>(() => cache.GetOrCompile<long>("n", new CelCompileOptions { ExpectedType = CelType.String }));
        Assert.Equal((0, 0L), cache.Kept);
    }

    [Fact]
    public void ConditionsOfTheCall_AreChecked()
    {
        var cache = new CelCompilationCache(Environment, 1, 1);

        Assert.Equal("environment", Assert.Throws<ArgumentNullException>(() => new CelCompilationCache(null!, 1, 1)).ParamName);
        Assert.Equal("maxEntryCount", Assert.Throws<ArgumentOutOfRangeException>(() => new CelCompilationCache(Environment, 0, 1)).ParamName);
        Assert.Equal("maxTotalTextLength", Assert.Throws<ArgumentOutOfRangeException>(() => new CelCompilationCache(Environment, 1, 0)).ParamName);
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => cache.GetOrCompile<long>(null!)).ParamName);
        Assert.Equal("bindings", Assert.Throws<ArgumentNullException>(() => cache.Evaluate<long>("n", null!, cancellationToken: TestContext.Current.CancellationToken)).ParamName);
    }

    [Fact]
    public void Evaluate_ChecksTheTokenBeforeTheCompilation()
    {
        var cache = new CelCompilationCache(Environment, 10, 100);
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => cache.Evaluate<long>("n", Bindings, cancellationToken: source.Token));
        Assert.Equal((0, 0L), cache.Kept);
    }

    // 64 threads over 16 texts, a capacity of 4 that evicts all the time: every call gives the value of its text.
    [Fact]
    public void ParallelCalls_GiveTheValuesOfTheirTexts()
    {
        var cache = new CelCompilationCache(Environment, 4, 10_000);
        long[] failures = new long[64];

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 64 }, thread =>
        {
            for (int call = 0; call < 200; call++)
            {
                int text = (thread + call) % 16;
                string expression = "n + " + text.ToString(CultureInfo.InvariantCulture);
                long viaCompile = cache.GetOrCompile<long>(expression).GetProgramOrThrow().Evaluate(Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
                long viaEvaluate = cache.Evaluate<long>(expression, Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
                if (viaCompile != 5 + text || viaEvaluate != 5 + text)
                {
                    failures[thread]++;
                }
            }
        });

        Assert.All(failures, count => Assert.Equal(0L, count));
        Assert.True(cache.Kept.Count <= 4);
    }
}

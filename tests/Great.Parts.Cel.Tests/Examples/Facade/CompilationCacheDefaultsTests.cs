using System.Threading;
using Great.Parts.Cel.Generator.Samples.Orders;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The compilation cache with default capacities and its control: 80 000 entries and 900 000 UTF-16 code units of text; Count,
// Clear, TryRemove by the text for every T and options; the one-step call with an input.
[Trait("Requirement", "+R-13")]
public sealed class CompilationCacheDefaultsTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .UseInput<Order>()
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings Bindings = new CelBindings().Add("x", 2L);

    [Fact]
    public void DefaultCapacities()
    {
        Assert.Equal(80_000, CelCompilationCache.DefaultMaxEntryCount);
        Assert.Equal(900_000, CelCompilationCache.DefaultMaxTotalTextLength);
        Assert.Equal(0, new CelCompilationCache(Environment).Count);
    }

    // An entry is the text, T and the options: one text compiled for two result types is two entries.
    [Fact]
    public void Count_ClearAndTryRemove()
    {
        var cache = new CelCompilationCache(Environment);
        cache.Evaluate<long>("x + 1", Bindings, cancellationToken: CancellationToken.None);
        cache.Evaluate<double>("x + 1", Bindings, cancellationToken: CancellationToken.None);
        cache.Evaluate<long>("x * 3", Bindings, cancellationToken: CancellationToken.None);

        Assert.Equal(3, cache.Count);
        Assert.True(cache.TryRemove("x + 1"));
        Assert.Equal(1, cache.Count);
        Assert.False(cache.TryRemove("x + 1"));
        Assert.False(cache.TryRemove("x  * 3"));

        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Equal(6L, cache.Evaluate<long>("x * 3", Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(1, cache.Count);
    }

    // A program returned before the removal stays valid.
    [Fact]
    public void RemovedProgram_StaysValid()
    {
        var cache = new CelCompilationCache(Environment);
        CelProgram<long> program = cache.GetOrCompile<long>("x + 1").GetProgramOrThrow();

        cache.Clear();

        Assert.Equal(3L, program.Evaluate(Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void EvaluateInput_ThroughTheCache()
    {
        var cache = new CelCompilationCache(Environment);
        var order = new Order { Total = 10.0 };

        Assert.Equal(20.0, cache.EvaluateInput<double>("total * double(x)", order, Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(20.0, cache.EvaluateInput<double>("total * double(x)", order, Bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(1, cache.Count);
    }
}

using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Great.Parts.Cel.DependencyInjection.Tests.Registration;

// AddCel: the three singletons, when the environment is built, the settings of CelOptions and the evaluator over the cache.
[Trait("Requirement", "F-16")]
public sealed class AddCelTests
{
    [Fact]
    public void AddCel_RegistersOneEnvironmentCacheAndEvaluator()
    {
        using ServiceProvider provider = new ServiceCollection().AddCel(builder => builder.AddVariable<long>("x")).BuildServiceProvider();

        CelEnvironment environment = provider.GetRequiredService<CelEnvironment>();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        Assert.Same(environment, provider.GetRequiredService<CelEnvironment>());
        Assert.Same(provider.GetRequiredService<CelCompilationCache>(), provider.GetRequiredService<CelCompilationCache>());
        Assert.Same(evaluator, provider.GetRequiredService<ICelEvaluator>());
        Assert.Same(environment, evaluator.Environment);
        Assert.Equal("x", Assert.Single(environment.Variables).Name);
    }

    // The first registration wins: the second call adds no services, and its delegate is never run.
    [Fact]
    public void AddCel_Twice_AddsNoServices()
    {
        ServiceCollection services = new();
        services.AddCel(builder => builder.AddVariable<long>("first"));
        int count = services.Count;

        services.AddCel(builder => builder.AddVariable<long>("second"));

        Assert.Equal(count, services.Count);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal("first", Assert.Single(provider.GetRequiredService<CelEnvironment>().Variables).Name);
    }

    [Fact]
    public void Environment_IsBuiltWhenFirstResolved()
    {
        int runs = 0;
        using ServiceProvider provider = new ServiceCollection().AddCel(_ => runs++).BuildServiceProvider();
        Assert.Equal(0, runs);

        _ = provider.GetRequiredService<ICelEvaluator>();
        _ = provider.GetRequiredService<CelEnvironment>();

        Assert.Equal(1, runs);
    }

    [Fact]
    public void BuildError_IsACelExceptionWhenResolved()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder.AddVariable<long>("x").AddVariable<string>("x"))
            .BuildServiceProvider();

        CelException exception = Assert.Throws<CelException>(() => provider.GetRequiredService<CelEnvironment>());

        Assert.Equal(CelErrorCode.DuplicateVariable, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void ExceptionOfTheDelegate_ReachesTheCallerUnchanged()
    {
        InvalidOperationException thrown = new("configure failed");
        using ServiceProvider provider = new ServiceCollection().AddCel(_ => throw thrown).BuildServiceProvider();

        Assert.Same(thrown, Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<CelEnvironment>()));
    }

    [Theory]
    [InlineData(null, 5UL)]
    [InlineData(7UL, 7UL)]
    public static void DefaultCostLimitOfTheOptions_ReplacesTheLimitOfTheDelegate(ulong? optionsLimit, ulong expected)
    {
        using ServiceProvider provider = new ServiceCollection()
            .Configure<CelOptions>(options => options.DefaultCostLimit = optionsLimit)
            .AddCel(builder => builder.SetDefaultCostLimit(5))
            .BuildServiceProvider();

        Assert.Equal<ulong?>(expected, provider.GetRequiredService<CelEnvironment>().DefaultCostLimit);
    }

    // The capacity of the cache comes from the options: with one entry, a second text replaces the first.
    [Fact]
    public void CacheCapacity_ComesFromTheOptions()
    {
        using ServiceProvider provider = new ServiceCollection()
            .Configure<CelOptions>(options => options.CacheMaxEntryCount = 1)
            .AddCel(_ => { })
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        Assert.Equal(2L, evaluator.Evaluate<long>("1 + 1", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
        Assert.Equal(4L, evaluator.Evaluate<long>("2 + 2", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());

        Assert.Equal(1, provider.GetRequiredService<CelCompilationCache>().Count);
    }

    [Theory]
    [InlineData(0, 10, "CacheMaxEntryCount")]
    [InlineData(10, 0, "CacheMaxTotalTextLength")]
    public static void CapacityLessThanOne_IsAnOptionsValidationExceptionWhenResolved(int entries, int textLength, string named)
    {
        using ServiceProvider provider = new ServiceCollection()
            .Configure<CelOptions>(options =>
            {
                options.CacheMaxEntryCount = entries;
                options.CacheMaxTotalTextLength = textLength;
            })
            .AddCel(_ => { })
            .BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<CelCompilationCache>());

        Assert.Contains(named, Assert.Single(exception.Failures), StringComparison.Ordinal);
    }

    // The evaluator compiles a text once through the cache, for bindings and for the input of the environment alike.
    [Fact]
    public void Evaluator_CompilesATextOnceThroughTheCache()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder.AddTypes(ShopContext.Default).UseInput<Order>().AddVariable<double>("limit"))
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        for (int i = 0; i < 2; i++)
        {
            Assert.True(evaluator.EvaluateInput<bool>("total > limit", new Order { Total = 10 }, new CelBindings().Add("limit", 5.0), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
        }

        Assert.Equal(1, provider.GetRequiredService<CelCompilationCache>().Count);
    }

    // Validate checks the text in the environment and creates nothing in the cache.
    [Fact]
    public void Evaluator_Validate_ChecksInTheEnvironment()
    {
        using ServiceProvider provider = new ServiceCollection().AddCel(builder => builder.AddVariable<long>("x")).BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredService<ICelEvaluator>();

        CelValidationResult valid = evaluator.Validate<bool>("x > 1");
        CelValidationResult invalid = evaluator.Validate<bool>("y > 1");

        Assert.True(valid.IsValid);
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(invalid.Errors).Code);
        Assert.Equal(0, provider.GetRequiredService<CelCompilationCache>().Count);
    }

    [Fact]
    public void AddCel_ChecksItsArguments()
    {
        Assert.Throws<ArgumentNullException>("services", () => CelServiceCollectionExtensions.AddCel(null!, _ => { }));
        Assert.Throws<ArgumentNullException>("configure", () => new ServiceCollection().AddCel(null!));
    }
}

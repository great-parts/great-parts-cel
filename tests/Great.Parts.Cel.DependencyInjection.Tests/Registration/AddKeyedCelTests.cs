using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Great.Parts.Cel.DependencyInjection.Tests.Registration;

// AddKeyedCel: keyed services by name, the named options of the same name, and names that are not names.
[Trait("Requirement", "F-16")]
public sealed class AddKeyedCelTests
{
    [Fact]
    public void AddKeyedCel_RegistersEachEnvironmentUnderItsNameWithItsOptions()
    {
        using ServiceProvider provider = new ServiceCollection()
            .Configure<CelOptions>("pricing", options => options.DefaultCostLimit = 9)
            .AddKeyedCel("pricing", builder => builder.AddVariable<long>("price"))
            .AddKeyedCel("access", builder => builder.AddVariable<string>("role"))
            .BuildServiceProvider();

        CelEnvironment pricing = provider.GetRequiredKeyedService<CelEnvironment>("pricing");
        CelEnvironment access = provider.GetRequiredKeyedService<CelEnvironment>("access");

        Assert.Equal("price", Assert.Single(pricing.Variables).Name);
        Assert.Equal("role", Assert.Single(access.Variables).Name);
        Assert.Equal<ulong?>(9, pricing.DefaultCostLimit);
        Assert.Null(access.DefaultCostLimit);
        Assert.Same(pricing, provider.GetRequiredKeyedService<ICelEvaluator>("pricing").Environment);
        Assert.Null(provider.GetService<CelEnvironment>());
    }

    // The keyed evaluator compiles through the keyed cache of its name.
    [Fact]
    public void KeyedEvaluator_CompilesThroughTheKeyedCache()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKeyedCel("pricing", builder => builder.AddVariable<long>("price"))
            .BuildServiceProvider();
        ICelEvaluator evaluator = provider.GetRequiredKeyedService<ICelEvaluator>("pricing");

        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(11L, evaluator.Evaluate<long>("price + 1", new CelBindings().Add("price", 10L), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
        }

        Assert.Equal(1, provider.GetRequiredKeyedService<CelCompilationCache>("pricing").Count);
    }

    // The capacity of the keyed cache comes from the options of its name, not from the unnamed options.
    [Fact]
    public void KeyedCacheCapacity_ComesFromTheOptionsOfItsName()
    {
        using ServiceProvider provider = new ServiceCollection()
            .Configure<CelOptions>("pricing", options => options.CacheMaxEntryCount = 1)
            .AddKeyedCel("pricing", _ => { })
            .AddKeyedCel("access", _ => { })
            .BuildServiceProvider();

        EvaluateTwoTexts(provider.GetRequiredKeyedService<ICelEvaluator>("pricing"));
        EvaluateTwoTexts(provider.GetRequiredKeyedService<ICelEvaluator>("access"));

        Assert.Equal(1, provider.GetRequiredKeyedService<CelCompilationCache>("pricing").Count);
        Assert.Equal(2, provider.GetRequiredKeyedService<CelCompilationCache>("access").Count);
    }

    private static void EvaluateTwoTexts(ICelEvaluator evaluator)
    {
        Assert.Equal(2L, evaluator.Evaluate<long>("1 + 1", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
        Assert.Equal(4L, evaluator.Evaluate<long>("2 + 2", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
    }

    // The capacities of the options of a name are checked when the keyed cache of that name is resolved.
    [Fact]
    public void KeyedCapacityLessThanOne_IsAnOptionsValidationExceptionOfItsNameOnly()
    {
        using ServiceProvider provider = new ServiceCollection()
            .Configure<CelOptions>("pricing", options => options.CacheMaxTotalTextLength = 0)
            .AddKeyedCel("pricing", _ => { })
            .AddKeyedCel("access", _ => { })
            .BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredKeyedService<CelCompilationCache>("pricing"));

        Assert.Equal("pricing", exception.OptionsName);
        Assert.Contains("CacheMaxTotalTextLength", Assert.Single(exception.Failures), StringComparison.Ordinal);
        Assert.Equal(0, provider.GetRequiredKeyedService<CelCompilationCache>("access").Count);
    }

    [Fact]
    public void AddKeyedCel_ChecksItsArguments()
    {
        ServiceCollection services = new();

        Assert.Throws<ArgumentNullException>("services", () => CelServiceCollectionExtensions.AddKeyedCel(null!, "a", _ => { }));
        Assert.Throws<ArgumentNullException>("name", () => services.AddKeyedCel(null!, _ => { }));
        Assert.Throws<ArgumentException>("name", () => services.AddKeyedCel(string.Empty, _ => { }));
        Assert.Throws<ArgumentNullException>("configure", () => services.AddKeyedCel("a", null!));
        Assert.Empty(services);
    }
}

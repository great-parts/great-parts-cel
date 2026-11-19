using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Great.Parts.Cel.DependencyInjection;

/// <summary>
/// Registers CEL in a dependency injection container: an environment, a compilation cache and an <see cref="ICelEvaluator"/>,
/// once for the application or by name for several environments.
/// </summary>
/// <remarks>
/// <para>
/// <c>services.AddCel(b =&gt; b.AddTypes(OrderContext.Default).UseInput&lt;Order&gt;())</c> registers three singletons:
/// the <see cref="CelEnvironment"/> built from the delegate, a <see cref="CelCompilationCache"/> over it and an
/// <see cref="ICelEvaluator"/> that evaluates through the cache. The environment is built when it is first resolved, with
/// <see cref="CelEnvironment.Create(Action{CelEnvironmentBuilder})"/>: a build error is a <see cref="Diagnostics.CelException"/>
/// at that moment, and an exception of the delegate reaches the caller unchanged.
/// </para>
/// <para>
/// The settings are <see cref="CelOptions"/> read through <c>IOptions&lt;CelOptions&gt;</c>, or through
/// <c>IOptionsMonitor&lt;CelOptions&gt;.Get(name)</c> for a named environment. The package does not read configuration:
/// the application binds the options itself, for example with <c>services.Configure&lt;CelOptions&gt;(section)</c> and the
/// configuration binding source generator, so that the package stays free of reflection and works under Native AOT. The
/// options are read once, when the environment and the cache are created.
/// </para>
/// <para>
/// The registrations are made once: a second call for the same name adds no services. The methods are called while the
/// container is configured, from one thread.
/// </para>
/// </remarks>
public static class CelServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CEL environment of the application, built from <paramref name="configure"/> with the standard library
    /// version 1, its compilation cache and an <see cref="ICelEvaluator"/>, as singletons.
    /// </summary>
    /// <remarks>
    /// <see cref="CelOptions.DefaultCostLimit"/>, when set, is applied after <paramref name="configure"/> and replaces a
    /// limit that the delegate set with <see cref="CelEnvironmentBuilder.SetDefaultCostLimit(ulong)"/>. The capacities of
    /// the cache come from <see cref="CelOptions.CacheMaxEntryCount"/> and <see cref="CelOptions.CacheMaxTotalTextLength"/>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Declares the contents of the environment on the builder.</param>
    /// <returns><paramref name="services"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCel(this IServiceCollection services, Action<CelEnvironmentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddOptionsCheck(services);
        services.TryAddSingleton(provider => CreateEnvironment(configure, provider.GetRequiredService<IOptions<CelOptions>>().Value));
        services.TryAddSingleton(provider => CreateCache(
            provider.GetRequiredService<CelEnvironment>(),
            provider.GetRequiredService<IOptions<CelOptions>>().Value));
        services.TryAddSingleton<ICelEvaluator>(provider => new CacheEvaluator(
            provider.GetRequiredService<CelEnvironment>(),
            provider.GetRequiredService<CelCompilationCache>()));
        return services;
    }

    /// <summary>
    /// Registers a named CEL environment, its compilation cache and an <see cref="ICelEvaluator"/> as keyed singletons with
    /// the key <paramref name="name"/>, for an application with several environments, such as one for pricing rules and one
    /// for access rules.
    /// </summary>
    /// <remarks>
    /// The services are resolved with the key, for example by a parameter with <c>[FromKeyedServices("orders")]</c>. The
    /// settings are the named <see cref="CelOptions"/> with the same name; otherwise the rules of
    /// <see cref="AddCel(IServiceCollection, Action{CelEnvironmentBuilder})"/> apply.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name of the environment: the key of its services and the name of its options.</param>
    /// <param name="configure">Declares the contents of the environment on the builder.</param>
    /// <returns><paramref name="services"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>, <paramref name="name"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public static IServiceCollection AddKeyedCel(this IServiceCollection services, string name, Action<CelEnvironmentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);

        AddOptionsCheck(services);
        services.TryAddKeyedSingleton(name, (provider, _) => CreateEnvironment(configure, NamedOptions(provider, name)));
        services.TryAddKeyedSingleton(name, (provider, _) => CreateCache(
            provider.GetRequiredKeyedService<CelEnvironment>(name),
            NamedOptions(provider, name)));
        services.TryAddKeyedSingleton<ICelEvaluator>(name, (provider, _) => new CacheEvaluator(
            provider.GetRequiredKeyedService<CelEnvironment>(name),
            provider.GetRequiredKeyedService<CelCompilationCache>(name)));
        return services;
    }

    // The options infrastructure and the check of the capacities, once for all environments: TryAddEnumerable adds the check once.
    private static void AddOptionsCheck(IServiceCollection services)
    {
        services.AddOptions();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CelOptions>, CelOptionsCheck>(static _ => new CelOptionsCheck()));
    }

    private static CelOptions NamedOptions(IServiceProvider provider, string name) =>
        provider.GetRequiredService<IOptionsMonitor<CelOptions>>().Get(name);

    // The limit of the options is applied after the delegate, so it replaces the limit the delegate set.
    private static CelEnvironment CreateEnvironment(Action<CelEnvironmentBuilder> configure, CelOptions options) =>
        CelEnvironment.Create(builder =>
        {
            configure(builder);
            if (options.DefaultCostLimit is ulong limit)
            {
                builder.SetDefaultCostLimit(limit);
            }
        });

    private static CelCompilationCache CreateCache(CelEnvironment environment, CelOptions options) =>
        new(environment, options.CacheMaxEntryCount, options.CacheMaxTotalTextLength);
}

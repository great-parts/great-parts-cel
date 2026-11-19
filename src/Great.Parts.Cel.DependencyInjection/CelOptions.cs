namespace Great.Parts.Cel.DependencyInjection;

/// <summary>
/// The settings of a CEL environment registered with <see cref="CelServiceCollectionExtensions.AddCel"/> or
/// <see cref="CelServiceCollectionExtensions.AddKeyedCel"/>: the default cost limit of the environment and the capacities
/// of its compilation cache.
/// </summary>
/// <remarks>
/// <para>
/// The application binds the settings itself, for example
/// <c>services.Configure&lt;CelOptions&gt;(configuration.GetSection("Cel"))</c> with the configuration binding source
/// generator; the package does not read configuration. Values are read once, when the environment and the cache are
/// created, so a change of the options after that has no effect.
/// </para>
/// <para>
/// Invalid values — a capacity less than 1 — are reported when the options are first read, as an
/// <c>OptionsValidationException</c> of <c>IOptions&lt;CelOptions&gt;.Value</c>, by the contract of
/// <c>IValidateOptions&lt;CelOptions&gt;</c>.
/// </para>
/// </remarks>
public sealed class CelOptions
{
    /// <summary>
    /// The cost limit of evaluations that pass no limit of their own, in cost units, or <see langword="null"/> to keep the
    /// limit that the delegate of the registration set, if any. When set, it replaces that limit, as
    /// <see cref="CelEnvironmentBuilder.SetDefaultCostLimit(ulong)"/> does.
    /// </summary>
    public ulong? DefaultCostLimit { get; set; }

    /// <summary>
    /// The largest number of entries of the compilation cache, 1 or more; by default
    /// <see cref="CelCompilationCache.DefaultMaxEntryCount"/>, 80,000.
    /// </summary>
    public int CacheMaxEntryCount { get; set; } = CelCompilationCache.DefaultMaxEntryCount;

    /// <summary>
    /// The largest total length of the expression texts that the compilation cache keeps, in UTF-16 code units, 1 or more;
    /// by default <see cref="CelCompilationCache.DefaultMaxTotalTextLength"/>, 900,000. With both defaults a full cache takes
    /// about 64 MiB.
    /// </summary>
    public int CacheMaxTotalTextLength { get; set; } = CelCompilationCache.DefaultMaxTotalTextLength;
}

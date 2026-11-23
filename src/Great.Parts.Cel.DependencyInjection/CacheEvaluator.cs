using System.Threading;

namespace Great.Parts.Cel.DependencyInjection;

// The registered ICelEvaluator: the one-step calls of the compilation cache of the environment, and the validation of the environment.
// The cache and the environment are safe for concurrent use, so this class is too.
internal sealed partial class CacheEvaluator(CelEnvironment environment, CelCompilationCache cache) : ICelEvaluator
{
    public CelEnvironment Environment => environment;

    public CelEvalResult<T> Evaluate<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default) =>
        cache.Evaluate<T>(expression, bindings, compileOptions, evalOptions, cancellationToken);

    public CelEvalResult<T> EvaluateInput<T>(
        string expression,
        object input,
        CelBindings? bindings = null,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default) =>
        cache.EvaluateInput<T>(expression, input, bindings, compileOptions, evalOptions, cancellationToken);

    public CelValidationResult Validate<T>(string expression, CelCompileOptions? options = null) =>
        environment.Validate<T>(expression, options);
}

using System.Threading;
using System.Threading.Tasks;

namespace Great.Parts.Cel.DependencyInjection;

// The asynchronous one-step calls of the registered evaluator delegate to the compilation cache, as the synchronous ones do.
internal sealed partial class CacheEvaluator
{
    public ValueTask<CelEvalResult<T>> EvaluateAsync<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default) =>
        cache.EvaluateAsync<T>(expression, bindings, compileOptions, evalOptions, cancellationToken);

    public ValueTask<CelEvalResult<T>> EvaluateInputAsync<T>(
        string expression,
        object input,
        CelBindings? bindings = null,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default) =>
        cache.EvaluateInputAsync<T>(expression, input, bindings, compileOptions, evalOptions, cancellationToken);
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Great.Parts.Cel;

public sealed partial class CelCompilationCache
{
    /// <summary>
    /// Takes the program of the expression from the cache, compiling it on a miss, and evaluates it on the bindings, awaiting
    /// the asynchronous functions it calls: the same result and errors as
    /// <see cref="CelEnvironment.EvaluateAsync{T}(string, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// The cache keeps programs, not evaluations: the state of an asynchronous evaluation lives only in this call.
    /// </remarks>
    /// <typeparam name="T">The .NET type of the result; it gives the expected CEL type unless the options set one.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The options of compilation, part of the key of the cache; <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The cost limit and the number of asynchronous calls in flight; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token of the evaluation, passed to every asynchronous call.</param>
    /// <returns>The value, or the errors of compilation, or the one error of the evaluation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    public ValueTask<CelEvalResult<T>> EvaluateAsync<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(bindings);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CelEvalResult<T>>(cancellationToken);
        }

        return CelEnvironment.EvaluateCompiledAsync(GetOrCompile<T>(expression, compileOptions), bindings, evalOptions, cancellationToken);
    }

    /// <summary>
    /// Takes the program of the expression from the cache, compiling it on a miss, and evaluates it on an instance of the input
    /// type of the environment, awaiting the asynchronous functions it calls: the same result and errors as
    /// <see cref="CelEnvironment.EvaluateInputAsync{T}(string, object, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>.
    /// </summary>
    /// <typeparam name="T">The .NET type of the result; it gives the expected CEL type unless the options set one.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="input">The input.</param>
    /// <param name="bindings">The values of other variables; <see langword="null"/> for none.</param>
    /// <param name="compileOptions">The options of compilation, part of the key of the cache; <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The cost limit and the number of asynchronous calls in flight; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token of the evaluation, passed to every asynchronous call.</param>
    /// <returns>The value, or the errors of compilation, or the one error of the evaluation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The environment has no input type, or <paramref name="input"/> is not of it.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    public ValueTask<CelEvalResult<T>> EvaluateInputAsync<T>(
        string expression,
        object input,
        CelBindings? bindings = null,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(input);
        CelEnvironment.CheckInput(_environment.InputType, input);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CelEvalResult<T>>(cancellationToken);
        }

        return CelEnvironment.EvaluateInputCompiledAsync(GetOrCompile<T>(expression, compileOptions), input, bindings, evalOptions, cancellationToken);
    }
}

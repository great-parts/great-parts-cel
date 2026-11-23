using System;
using System.Threading;
using System.Threading.Tasks;

namespace Great.Parts.Cel.DependencyInjection;

// The interface is new in 2.0, so the members have no default implementation.
public partial interface ICelEvaluator
{
    /// <summary>
    /// Compiles an expression through the cache and evaluates it on the bindings in one call, awaiting the asynchronous
    /// functions it calls, as
    /// <see cref="CelCompilationCache.EvaluateAsync{T}(string, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>
    /// does.
    /// </summary>
    /// <remarks>
    /// A program that needs no asynchronous result completes synchronously, so this member also serves expressions without
    /// asynchronous functions at no extra cost.
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the result is returned.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The compilation options, such as the expected type, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit and the number of asynchronous calls in flight, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call, passed to every asynchronous call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    ValueTask<CelEvalResult<T>> EvaluateAsync<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles an expression through the cache and evaluates it on an instance of the input type of the environment and on
    /// the bindings of other variables in one call, awaiting the asynchronous functions it calls, as
    /// <see cref="CelCompilationCache.EvaluateInputAsync{T}(string, object, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>
    /// does.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the result is returned.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="input">The input.</param>
    /// <param name="bindings">The values of other variables, or <see langword="null"/> for none.</param>
    /// <param name="compileOptions">The compilation options, such as the expected type, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit and the number of asynchronous calls in flight, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call, passed to every asynchronous call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The environment has no input type, or <paramref name="input"/> is not of it.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    ValueTask<CelEvalResult<T>> EvaluateInputAsync<T>(
        string expression,
        object input,
        CelBindings? bindings = null,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default);
}

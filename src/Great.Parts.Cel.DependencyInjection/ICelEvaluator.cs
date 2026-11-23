using System;
using System.Threading;

namespace Great.Parts.Cel.DependencyInjection;

/// <summary>
/// Evaluates and validates expression texts in one registered CEL environment through its compilation cache: the service
/// that application code takes from the container and that tests replace with their own implementation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CelServiceCollectionExtensions.AddCel"/> registers an implementation over the <see cref="CelCompilationCache"/>
/// of the environment, so a text is compiled once while the cache keeps it. The one-step calls take every parameter of
/// compilation and evaluation — the expected CEL type in <see cref="CelCompileOptions.ExpectedType"/>, the .NET type of the
/// result <c>T</c>, the cost limit in <see cref="CelEvalOptions.CostLimit"/> and the cancellation token — and give the same
/// results and errors as the compilation and the evaluation of its result with the same parameters.
/// <see cref="Diagnostics.CelError.Stage"/> tells whether compilation or evaluation failed.
/// </para>
/// <para>
/// The registered implementation can be called from any number of threads at once. Members added to the interface after
/// version 2.0 have a default implementation, so a replacement written for 2.0 keeps compiling.
/// </para>
/// </remarks>
public partial interface ICelEvaluator
{
    /// <summary>
    /// The environment in which the evaluator compiles expressions.
    /// </summary>
    CelEnvironment Environment { get; }

    /// <summary>
    /// Compiles an expression through the cache and evaluates it on the bindings in one call, as
    /// <see cref="CelCompilationCache.Evaluate{T}(string, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>
    /// does.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the result is returned.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The compilation options, such as the expected type, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping, or the expected type of
    /// <paramref name="compileOptions"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    CelEvalResult<T> Evaluate<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles an expression through the cache and evaluates it on an instance of the input type of the environment and
    /// on the bindings of other variables in one call, as
    /// <see cref="CelCompilationCache.EvaluateInput{T}(string, object, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>
    /// does.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the result is returned.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="input">The instance of the input type of the environment.</param>
    /// <param name="bindings">The values of the other variables, or <see langword="null"/> for none.</param>
    /// <param name="compileOptions">The compilation options, such as the expected type, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The environment has no input type, <paramref name="input"/> is not an instance of it, <typeparamref name="T"/> is not
    /// in the result column of the type mapping, or the expected type of <paramref name="compileOptions"/> cannot be
    /// converted to <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    CelEvalResult<T> EvaluateInput<T>(
        string expression,
        object input,
        CelBindings? bindings = null,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an expression in the environment without creating a program, as
    /// <see cref="CelEnvironment.Validate{T}(string, CelCompileOptions)"/> does.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the expression would return its result.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The result of the validation: the errors, the result type and the referenced names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping, or the expected type of
    /// <paramref name="options"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    CelValidationResult Validate<T>(string expression, CelCompileOptions? options = null);
}

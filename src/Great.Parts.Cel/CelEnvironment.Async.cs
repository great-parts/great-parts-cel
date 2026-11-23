using System;
using System.Threading;
using System.Threading.Tasks;

namespace Great.Parts.Cel;

public sealed partial class CelEnvironment
{
    /// <summary>
    /// Compiles the expression in this environment and evaluates it on the bindings, awaiting the asynchronous functions it
    /// calls: the same result and errors as <see cref="Compile{T}(string, CelCompileOptions)"/> followed by
    /// <see cref="CelProgram{T}.EvaluateAsync"/>.
    /// </summary>
    /// <remarks>
    /// Errors of compilation are the result, and the task is complete at once; <see cref="Diagnostics.CelError.Stage"/> tells
    /// compilation from evaluation. A program that needs no asynchronous result completes synchronously.
    /// </remarks>
    /// <typeparam name="T">The .NET type of the result; it gives the expected CEL type unless the options set one.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The options of compilation; <see langword="null"/> for none.</param>
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

        return EvaluateCompiledAsync(Compile<T>(expression, compileOptions), bindings, evalOptions, cancellationToken);
    }

    // The steps of the asynchronous one-step call after the compilation, as EvaluateCompiled: the errors of a failed compilation are the
    // result of a task that is complete; otherwise the token is checked again and the program evaluated.
    internal static ValueTask<CelEvalResult<T>> EvaluateCompiledAsync<T>(
        CelCompileResult<T> compiled,
        CelBindings bindings,
        CelEvalOptions? evalOptions,
        CancellationToken cancellationToken)
    {
        if (!compiled.IsSuccess)
        {
            return new ValueTask<CelEvalResult<T>>(new CelEvalResult<T>(compiled.Errors));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CelEvalResult<T>>(cancellationToken);
        }

        return compiled.Program.EvaluateAsync(bindings, evalOptions, cancellationToken);
    }

    // The same with an input, as EvaluateInputCompiled.
    internal static ValueTask<CelEvalResult<T>> EvaluateInputCompiledAsync<T>(
        CelCompileResult<T> compiled,
        object input,
        CelBindings? bindings,
        CelEvalOptions? evalOptions,
        CancellationToken cancellationToken)
    {
        if (!compiled.IsSuccess)
        {
            return new ValueTask<CelEvalResult<T>>(new CelEvalResult<T>(compiled.Errors));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CelEvalResult<T>>(cancellationToken);
        }

        return compiled.Program.EvaluateInputAsync(input, bindings, evalOptions, cancellationToken);
    }

    /// <summary>
    /// Compiles the expression in this environment and evaluates it on an instance of the input type of the environment,
    /// awaiting the asynchronous functions it calls: the same result and errors as compilation followed by
    /// <see cref="CelProgram{T}.EvaluateInputAsync{TInput}"/>.
    /// </summary>
    /// <remarks>
    /// The input is taken as <see cref="object"/>, as by the synchronous one-step call with an input: a structure is boxed once.
    /// </remarks>
    /// <typeparam name="T">The .NET type of the result; it gives the expected CEL type unless the options set one.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="input">The input.</param>
    /// <param name="bindings">The values of other variables; <see langword="null"/> for none.</param>
    /// <param name="compileOptions">The options of compilation; <see langword="null"/> for none.</param>
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
        CheckInput(InputType, input);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CelEvalResult<T>>(cancellationToken);
        }

        return EvaluateInputCompiledAsync(Compile<T>(expression, compileOptions), input, bindings, evalOptions, cancellationToken);
    }
}

using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// A compiled expression: parsed, type-checked in its environment and planned, ready to evaluate any
/// number of times on different bindings.
/// </summary>
/// <remarks>
/// <para>
/// The program is bound to the environment it was compiled in and to the function implementations of
/// that environment. It does not accept expression text: evaluation reads only the bindings.
/// </para>
/// <para>
/// The program is immutable. It can be evaluated from any number of threads at once without
/// synchronization, and each evaluation gives the same result as it would alone: the state of an
/// evaluation lives in that evaluation only.
/// </para>
/// </remarks>
/// <typeparam name="T">The .NET type in which <see cref="Evaluate"/> returns the result.</typeparam>
public sealed class CelProgram<T>
{
    private readonly Plan _plan;

    internal CelProgram(Plan plan, CelType resultType)
    {
        _plan = plan;
        ResultType = resultType;
    }

    /// <summary>
    /// The CEL type of the result that type checking found, for example <c>bool</c>, or <c>dyn</c> when
    /// the type is known only at evaluation.
    /// </summary>
    public CelType ResultType { get; }

    /// <summary>
    /// Evaluates the program on the bindings and returns the result converted to <typeparamref name="T"/>,
    /// or the evaluation error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluation errors are returned, not thrown, and there is one at most: when several parts of an
    /// expression fail, the CEL language definition does not specify which error is reported. A variable
    /// without a value in the bindings is the error <see cref="CelErrorCode.Unbound"/> when the expression
    /// reads it; a value that does not have the declared type of its variable is the error
    /// <see cref="CelErrorCode.BindingTypeMismatch"/>. An exception thrown by application code — a function
    /// implementation, a member getter of an application object — becomes the error
    /// <see cref="CelErrorCode.ApplicationException"/>, which keeps the original exception. A result that
    /// does not convert to <typeparamref name="T"/> by the type mapping table of the documentation is the
    /// error <see cref="CelErrorCode.ResultConversion"/>.
    /// </para>
    /// <para>
    /// When the token can be canceled, evaluation checks it before the first macro step or application
    /// call and then regularly between them; after cancellation it starts no more than 1,000 such steps and
    /// calls and throws <see cref="OperationCanceledException"/>. With <see cref="CancellationToken.None"/>
    /// nothing is counted.
    /// </para>
    /// <para>
    /// Application data is read lazily, without copying. If the application changes the data while an
    /// evaluation reads it, the result is undefined.
    /// </para>
    /// </remarks>
    /// <param name="bindings">
    /// The values of the variables. Names the environment does not declare are ignored, so one set of
    /// bindings can serve programs of different environments.
    /// </param>
    /// <param name="options">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the evaluation.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the evaluation error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelEvalResult<T> Evaluate(CelBindings bindings, CelEvalOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (options?.CostLimit is not null || cancellationToken.CanBeCanceled)
        {
            // The cost limit and cancellation are not implemented yet; they are not ignored.
            throw new NotImplementedException();
        }

        return Run(_plan, bindings);
    }

    // Evaluates a plan in a new frame and converts the value of the root to T: an error value of the root
    // is the one error of the result, and a value that no result row converts is ResultConversion at the
    // position of the root.
    internal static CelEvalResult<T> Run(Plan plan, CelBindings bindings)
    {
        CelValue value = plan.Evaluate(bindings.Values);
        if (value.TryGetError(out CelError? error))
        {
            return new CelEvalResult<T>([error]);
        }

        return ConversionRules.TryToClr(value, out T? result)
            ? new CelEvalResult<T>(result)
            : new CelEvalResult<T>([ConversionRules.ResultConversionError<T>(value, plan.Lines.GetLocation(plan.Root.Offset))]);
    }
}

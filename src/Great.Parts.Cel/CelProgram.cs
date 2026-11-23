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
/// that environment. It does not accept expression text: evaluation reads only the bindings and, in an environment with
/// an input type, the instance of the input.
/// </para>
/// <para>
/// The program is immutable. It can be evaluated from any number of threads at once without
/// synchronization, and each evaluation gives the same result as it would alone: the state of an
/// evaluation lives in that evaluation only.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// The .NET type in which <see cref="Evaluate(CelBindings, CelEvalOptions, CancellationToken)"/> and the evaluation on an
/// input return the result.
/// </typeparam>
public sealed partial class CelProgram<T>
{
    // The position at which the expression starts in its source document, from the compile options, or null.
    private readonly CelSourceLocation? _start;

    // The cost limit of the environment, which applies when a call passes none; null for none.
    private readonly ulong? _defaultCostLimit;

    // The description of the input type of the environment; null when it has none.
    private readonly CelObjectType? _inputType;

    internal CelProgram(Plan plan, CelType resultType, CelSourceLocation? start, ulong? defaultCostLimit = null, CelObjectType? inputType = null)
    {
        Plan = plan;
        ResultType = resultType;
        _start = start;
        _defaultCostLimit = defaultCostLimit;
        _inputType = inputType;
    }

    /// <summary>
    /// The CEL type of the result that type checking found, for example <c>bool</c>, or <c>dyn</c> when
    /// the type is known only at evaluation.
    /// </summary>
    public CelType ResultType { get; }

    // The plan of the program, for the internal steps of evaluation that the tests call.
    internal Plan Plan { get; }

    /// <summary>
    /// Evaluates the program on the bindings and returns the result converted to <typeparamref name="T"/>,
    /// or the evaluation error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluation errors are returned, not thrown, and there is one at most: when several parts of an expression fail,
    /// the CEL language definition does not specify which error is reported. A variable without a value in the bindings
    /// is the error
    /// <see cref="CelErrorCode.Unbound"/> when the expression reads it; a value that does not have the declared type of its variable is the error <see cref="CelErrorCode.BindingTypeMismatch"/>. An exception thrown by application code — a function implementation, a member getter of an application object — becomes the error <see cref="CelErrorCode.ApplicationException"/>, which keeps the original exception. A result that does not convert to <typeparamref name="T"/> by the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>
    /// is the error <see cref="CelErrorCode.ResultConversion"/>.
    /// </para>
    /// <para>
    /// When the token can be canceled, evaluation checks it before the first macro step or application
    /// call and then every 64 of them; after cancellation it starts no more than 64 such steps and calls
    /// and throws <see cref="OperationCanceledException"/>. With <see cref="CancellationToken.None"/>
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
        ulong? costLimit = options?.CostLimit ?? _defaultCostLimit;
        return Plan.HasAsyncCalls ? AsyncRequired(costLimit) : Run(Plan, ResultType, bindings, costLimit, _start, cancellationToken);
    }

    // The result of a synchronous evaluation of a program that calls an overload that waits: the error at the first such call, before
    // anything is evaluated, so with the cost 0 when there is a limit.
    private CelEvalResult<T> AsyncRequired(ulong? costLimit) =>
        new([InDocument(EvaluationErrors.AsyncEvaluationRequired(Plan.FirstAsyncFunction!, Plan.Lines, Plan.FirstAsyncOffset), _start)], costLimit is null ? null : 0UL);

    // The call of the simple entry: the limit of the call, whatever the default of the environment, and no options object.
    internal CelEvalResult<T> EvaluateWithLimit(CelBindings bindings, ulong costLimit, CancellationToken cancellationToken) =>
        Run(Plan, ResultType, bindings, costLimit, _start, cancellationToken);

    // Evaluates a plan in a frame that the thread rents and gives back when the evaluation ends, with a cost limit, null for none, and the token of the evaluation, whose
    // cancellation passes through as OperationCanceledException, and converts the value of the root to T by the
    // result rows and the static type of the root: an error value of the root is the one error of the result, and a
    // value that does not convert is ResultConversion at the position of the root. With the start of the expression in
    // a document the error has its position there too.
    internal static CelEvalResult<T> Run(
        Plan plan,
        CelType resultType,
        CelBindings bindings,
        ulong? costLimit,
        CelSourceLocation? start,
        CancellationToken cancellationToken,
        object? input = null)
    {
        Frame frame = plan.RentFrame(bindings.Values, costLimit, cancellationToken, input);
        try
        {
            return Run(plan, resultType, frame, start);
        }
        finally
        {
            frame.Release();
        }
    }

    // The evaluation of a plan in a frame that the caller rented, and the conversion of the value of the root: the steps of Run. The
    // result holds nothing of the frame, so the caller may give it back as soon as this returns.
    private static CelEvalResult<T> Run(Plan plan, CelType resultType, Frame frame, CelSourceLocation? start)
    {
        // The cost is the sum the frame charged, reported only when a limit applied: on an error of the evaluation the sum including
        // the operation that failed, and for CostLimitExceeded the sum before the refused charge.
        CelValue value = plan.Run(frame);
        ulong? cost = frame.Metered ? frame.ChargedUnits : null;
        return Result(plan, resultType, value, cost, start);
    }

    // The result of the value of the root of the last pass of an asynchronous evaluation.
    internal CelEvalResult<T> ResultOf(CelValue value, ulong? cost) => Result(Plan, ResultType, value, cost, _start);

    // The result of the value of the root and the cost: the one error, or the value converted to T.
    private static CelEvalResult<T> Result(Plan plan, CelType resultType, CelValue value, ulong? cost, CelSourceLocation? start)
    {
        if (value.TryGetError(out CelError? error))
        {
            return new CelEvalResult<T>([InDocument(error, start)], cost);
        }

        if (ConversionRules.TryToResult(value, resultType, out T? result, out string? actualType, out CelError? elementError, plan.Types))
        {
            return new CelEvalResult<T>(result, cost);
        }

        // An error value that an element of the result is, is the one error of the result, with the place of the root.
        if (elementError is not null && EvaluationErrors.Placed(CelValue.FromError(elementError), plan.Lines, plan.Root.Offset).TryGetError(out CelError? placed))
        {
            return new CelEvalResult<T>([InDocument(placed, start)], cost);
        }

        var root = new CelSourceSpan(plan.Lines.GetLocation(plan.Root.Offset), plan.Lines.GetEnd(plan.Root.Offset) - plan.Root.Offset);
        CelType actual = ConversionRules.ResultActualType<T>(value, resultType, plan.Types);
        return new CelEvalResult<T>([InDocument(ConversionRules.ResultConversionError<T>(actualType, actual, root), start)], cost);
    }

    private static CelError InDocument(CelError error, CelSourceLocation? start) =>
        start is { } documentStart ? error.InDocument(documentStart) : error;

    /// <summary>
    /// Evaluates the program on an instance of the input type of its environment and, optionally, on the bindings of the
    /// other variables, and returns the result converted to <typeparamref name="T"/>, or the evaluation error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fields of the input are the variables of the top level that the environment declared with
    /// <see cref="CelEnvironmentBuilder.UseInput{TInput}"/>; they are read from <paramref name="input"/> lazily, without
    /// copying, through the description of the type source. A structure is passed without boxing. The fields are read only
    /// from <paramref name="input"/>: a binding with the name of a field is not used. Other variables are read from
    /// <paramref name="bindings"/>; without bindings, reading one of them is the evaluation error
    /// <see cref="CelErrorCode.Unbound"/>.
    /// </para>
    /// <para>
    /// The second parameter is the bindings, so pass the options by name when there are no bindings:
    /// <c>program.EvaluateInput(order, options: new CelEvalOptions { CostLimit = 10_000 })</c>. To evaluate a program of an
    /// environment without an input type, use <see cref="Evaluate(CelBindings, CelEvalOptions, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Errors, cancellation and threads are as for <see cref="Evaluate(CelBindings, CelEvalOptions, CancellationToken)"/>:
    /// evaluation errors are returned, at most one; the token is checked every 64 macro steps and application calls; the
    /// program can be evaluated from any number of threads at once.
    /// </para>
    /// </remarks>
    /// <typeparam name="TInput">The .NET type of the input: the input type of the environment.</typeparam>
    /// <param name="input">The instance whose fields are the variables.</param>
    /// <param name="bindings">The values of the other variables, or <see langword="null"/> for none.</param>
    /// <param name="options">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the evaluation.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the evaluation error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The environment of the program has no input type, or <paramref name="input"/> is not an instance of it.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelEvalResult<T> EvaluateInput<TInput>(
        TInput input,
        CelBindings? bindings = null,
        CelEvalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // The checks do not box a structure: a reference is tested against null by the generic parameter, and a structure is
        // compared by its type, not by an instance test of its box.
        if (default(TInput) is null && input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        ulong? costLimit = options?.CostLimit ?? _defaultCostLimit;
        if (typeof(TInput).IsValueType)
        {
            // A structure stays in the frame of the evaluation and is read by reference through the typed description; it is never boxed
            // for the call.
            CelEnvironment.CheckInputType(_inputType, typeof(TInput));
            if (Plan.HasAsyncCalls)
            {
                return AsyncRequired(costLimit);
            }

            InputFrame<TInput> frame = Plan.RentInputFrame(
                (bindings ?? CelBindings.Empty).Values,
                costLimit,
                input,
                _inputType as CelObjectType<TInput>,
                cancellationToken);
            try
            {
                return Run(Plan, ResultType, frame, _start);
            }
            finally
            {
                frame.Release();
            }
        }

        CelEnvironment.CheckInput(_inputType, input!);
        return Plan.HasAsyncCalls
            ? AsyncRequired(costLimit)
            : Run(Plan, ResultType, bindings ?? CelBindings.Empty, costLimit, _start, cancellationToken, input);
    }
}

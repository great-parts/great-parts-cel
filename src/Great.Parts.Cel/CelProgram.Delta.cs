using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

public sealed partial class CelProgram<T>
{
    /// <summary>
    /// Whether the program calls an overload with an asynchronous implementation: such a program is evaluated by
    /// <see cref="EvaluateAsync"/>, <see cref="EvaluateInputAsync{TInput}"/> or the partial members.
    /// </summary>
    /// <remarks>
    /// A synchronous evaluation of such a program — <see cref="Evaluate"/>, <see cref="EvaluateInput{TInput}"/> and the
    /// synchronous one-step calls — returns the evaluation error <see cref="CelErrorCode.AsyncEvaluationRequired"/> at the first
    /// asynchronous call of the text, before it evaluates anything. Whether the program calls one is known from the expression
    /// and the environment, not from the data.
    /// </remarks>
    public bool HasAsyncCalls => Plan.HasAsyncCalls;

    /// <summary>
    /// Evaluates the program on the bindings and awaits the asynchronous functions it calls: the same result and errors as
    /// <see cref="Evaluate"/> would give if those functions were synchronous.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The evaluation runs synchronously, on the calling thread, until the expression needs the result of an asynchronous call
    /// whose arguments are known. Then it starts every such call of that pass, in the order the pass met them and at most
    /// <see cref="CelEvalOptions.MaxConcurrentAsyncCalls"/> at a time, awaits them all and evaluates again, until the result does
    /// not depend on a call. A call with the same arguments at the same place of the expression runs once per evaluation. The
    /// number of passes is the depth of the chain of dependent calls plus one; it depends on the expression and the data, not on
    /// the order or the time in which the calls complete, and neither do the result and the cost.
    /// </para>
    /// <para>
    /// A program that needs no asynchronous result completes synchronously: the returned task is complete and allocates
    /// nothing, so the member costs no more than <see cref="Evaluate"/> for a program without asynchronous functions.
    /// </para>
    /// <para>
    /// The cost limit bounds the sum of the costs of all passes, and <see cref="CelEvalResult{T}.Cost"/> reports that sum: a
    /// chain of k dependent calls evaluates the expression up to k + 1 times, so set the limit for the sum. An exception of an
    /// asynchronous function is the evaluation error <see cref="CelErrorCode.ApplicationException"/>, and the other calls of the
    /// pass are still awaited. After <paramref name="cancellationToken"/> is canceled, no new call starts; the calls already
    /// started receive the same token and are awaited, then awaiting the result throws <see cref="OperationCanceledException"/>:
    /// the member itself never throws it, even when the token was canceled before the call. The library creates no threads, tasks or timers: continuations run where the application functions complete.
    /// </para>
    /// </remarks>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="options">The cost limit and the number of asynchronous calls in flight; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token of the evaluation, passed to every asynchronous call.</param>
    /// <returns>The value converted to <typeparamref name="T"/>, or the one error of the evaluation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    public ValueTask<CelEvalResult<T>> EvaluateAsync(
        CelBindings bindings,
        CelEvalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ulong? costLimit = options?.CostLimit ?? _defaultCostLimit;
        try
        {
            Frame frame = Plan.RentFrame(bindings.Values, costLimit, cancellationToken);
            CelValue root = FirstPass(frame, costLimit, out AsyncCallTable? table, out ulong charged, out uint events);
            if (Waits(root, table))
            {
                return new AsyncEvaluation<T>(this, bindings, null, costLimit, WindowOf(options), table, charged, events, cancellationToken).RunAsync();
            }

            return new ValueTask<CelEvalResult<T>>(ResultOf(root, costLimit is null ? null : charged));
        }
        catch (OperationCanceledException exception)
        {
            return Canceled<CelEvalResult<T>>(exception, cancellationToken);
        }
    }

    // The task of an evaluation whose first pass was canceled: the first pass runs in the call, and a call that returns a task does not
    // throw the cancellation, as the later passes, which run in the task, do not.
    private static ValueTask<TResult> Canceled<TResult>(OperationCanceledException exception, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<TResult>(cancellationToken)
            : ValueTask.FromException<TResult>(exception);

    /// <summary>
    /// Evaluates the program on an instance of the input type of the environment, and on bindings of other variables, and awaits
    /// the asynchronous functions it calls, as <see cref="EvaluateAsync"/> does.
    /// </summary>
    /// <remarks>
    /// The input is read as by <see cref="EvaluateInput{TInput}"/>; a structure is kept in the state of the evaluation between
    /// passes and is not boxed for the call.
    /// </remarks>
    /// <typeparam name="TInput">The input type of the environment.</typeparam>
    /// <param name="input">The input.</param>
    /// <param name="bindings">The values of other variables; <see langword="null"/> for none.</param>
    /// <param name="options">The cost limit and the number of asynchronous calls in flight; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token of the evaluation, passed to every asynchronous call.</param>
    /// <returns>The value converted to <typeparamref name="T"/>, or the one error of the evaluation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The environment has no input type, or <typeparamref name="TInput"/> is not its input type.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    public ValueTask<CelEvalResult<T>> EvaluateInputAsync<TInput>(
        TInput input,
        CelBindings? bindings = null,
        CelEvalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // The checks do not box a structure, as those of EvaluateInput.
        if (default(TInput) is null && input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        ulong? costLimit = options?.CostLimit ?? _defaultCostLimit;
        CelBindings other = bindings ?? CelBindings.Empty;
        if (typeof(TInput).IsValueType)
        {
            CelEnvironment.CheckInputType(_inputType, typeof(TInput));
            var description = _inputType as CelObjectType<TInput>;
            try
            {
                InputFrame<TInput> frame = Plan.RentInputFrame(other.Values, costLimit, input, description, cancellationToken);
                CelValue root = FirstPass(frame, costLimit, out AsyncCallTable? table, out ulong charged, out uint events);
                if (Waits(root, table))
                {
                    return new AsyncInputEvaluation<T, TInput>(this, other, input, description, costLimit, WindowOf(options), table, charged, events, cancellationToken).RunAsync();
                }

                return new ValueTask<CelEvalResult<T>>(ResultOf(root, costLimit is null ? null : charged));
            }
            catch (OperationCanceledException exception)
            {
                return Canceled<CelEvalResult<T>>(exception, cancellationToken);
            }
        }

        CelEnvironment.CheckInput(_inputType, input!);
        try
        {
            Frame instanceFrame = Plan.RentFrame(other.Values, costLimit, cancellationToken, input);
            CelValue instanceRoot = FirstPass(instanceFrame, costLimit, out AsyncCallTable? instanceTable, out ulong instanceCharged, out uint instanceEvents);
            if (Waits(instanceRoot, instanceTable))
            {
                return new AsyncEvaluation<T>(this, other, input, costLimit, WindowOf(options), instanceTable, instanceCharged, instanceEvents, cancellationToken).RunAsync();
            }

            return new ValueTask<CelEvalResult<T>>(ResultOf(instanceRoot, costLimit is null ? null : instanceCharged));
        }
        catch (OperationCanceledException exception)
        {
            return Canceled<CelEvalResult<T>>(exception, cancellationToken);
        }
    }

    // The first pass of an asynchronous evaluation, in the frame that the caller rented; it is the pass of a synchronous evaluation, and
    // it gives the frame back. The table holds the calls the pass registered, null when it registered none.
    private CelValue FirstPass(Frame frame, ulong? costLimit, out AsyncCallTable? table, out ulong charged, out uint events)
    {
        frame.CollectsCalls = Plan.HasAsyncCalls;
        try
        {
            CelValue root = Plan.Run(frame);
            table = frame.Calls;
            charged = costLimit is null ? 0 : frame.ChargedUnits;
            events = frame.Events;
            return root;
        }
        finally
        {
            frame.Release();
        }
    }

    // Whether the result of the pass depends on a call that has to be started: it is an unknown value and the pass registered a call.
    private static bool Waits(CelValue root, [NotNullWhen(true)] AsyncCallTable? table) => root.IsUnknown && table is { HasPending: true };

    private static int WindowOf(CelEvalOptions? options) => options?.MaxConcurrentAsyncCalls ?? CelEvalOptions.DefaultMaxConcurrentAsyncCalls;

    /// <summary>
    /// Evaluates the program with parts of the data unknown: the value when the unknown parts do not affect it, the errors, or
    /// the unknown parts the result depends on, and the residual expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The program must be compiled with <see cref="CelCompileOptions.PartialEvaluation"/>. Each pattern of
    /// <paramref name="unknowns"/> names a variable by its full name, as the bindings do, and optionally a path of fields, list
    /// indexes and map keys under it, where a qualifier may match any; an access whose path agrees with a pattern as far as both
    /// go is unknown. Unknown values propagate as errors do in the CEL language definition: <c>false &amp;&amp; x</c> and
    /// <c>true || x</c> are known whatever <c>x</c> is, an error of an argument of a function takes precedence over an unknown
    /// argument, and in <c>&amp;&amp;</c> and <c>||</c> an unknown operand takes precedence over an error.
    /// </para>
    /// <para>
    /// An asynchronous call whose arguments are known is not made: it is an unknown that describes the call — function,
    /// overload and arguments. <see cref="EvaluatePartialAsync"/> makes such calls.
    /// </para>
    /// <para>
    /// To mark as unknown every declared variable that the bindings lack, build the patterns from
    /// <see cref="CelEnvironment.Variables"/>. Partial evaluation records the values of the nodes for the residual expression
    /// and allocates memory for them on every call.
    /// </para>
    /// </remarks>
    /// <param name="bindings">The values of the known variables.</param>
    /// <param name="unknowns">The patterns of the unknown parts of the data; empty for none.</param>
    /// <param name="options">The cost limit; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token of the evaluation.</param>
    /// <returns>The value, the errors, or the unknown parts the result depends on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> or <paramref name="unknowns"/> is <see langword="null"/>, or contains <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The program was not compiled with <see cref="CelCompileOptions.PartialEvaluation"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelPartialEvalResult<T> EvaluatePartial(
        CelBindings bindings,
        IReadOnlyList<CelAttributePattern> unknowns,
        CelEvalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        UnknownPattern[] patterns = PartialPatterns(bindings, unknowns, out CelParsedExpr tree);

        // The frame holds the state of the partial evaluation, so the nodes that serve it see it; the frame is given back, and with it
        // the state, however the evaluation ends.
        Frame frame = Plan.RentFrame(bindings.Values, options?.CostLimit ?? _defaultCostLimit, cancellationToken);
        var state = new PartialState(patterns, tree.IdCount, Plan.KeyCount);
        frame.Partial = state;
        try
        {
            CelValue value = Plan.Run(frame);
            ulong? cost = frame.Metered ? frame.ChargedUnits : null;
            return PartialResult(tree, value, cost, new ResidualSource(tree, state.Release()));
        }
        finally
        {
            state.Discard();
            frame.Release();
        }
    }

    // The patterns of the call as the evaluator keeps them, and the tree of the program, which only a program compiled for partial
    // evaluation has.
    private UnknownPattern[] PartialPatterns(CelBindings bindings, IReadOnlyList<CelAttributePattern> unknowns, out CelParsedExpr tree)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(unknowns);
        var patterns = new UnknownPattern[unknowns.Count];
        for (int i = 0; i < patterns.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(unknowns[i], nameof(unknowns));
            patterns[i] = new UnknownPattern(unknowns[i].Variable, unknowns[i].Qualifiers);
        }

        tree = Plan.Tree ?? throw new InvalidOperationException("The program was not compiled for partial evaluation: set CelCompileOptions.PartialEvaluation.");
        return patterns;
    }

    // The result of the value of the root: the unknown parts it depends on, or the one error, or the value converted to T.
    internal CelPartialEvalResult<T> PartialResult(CelParsedExpr tree, CelValue value, ulong? cost, ResidualSource residual)
    {
        if (value.TryGetUnknown(out UnknownSet? set))
        {
            ImmutableArray<CelUnknown>.Builder unknowns = ImmutableArray.CreateBuilder<CelUnknown>(set.Entries.Length);
            foreach (UnknownEntry entry in set.Entries)
            {
                unknowns.Add(new CelUnknown(entry, tree.GetSpan(entry.Id)));
            }

            return new CelPartialEvalResult<T>(unknowns.MoveToImmutable(), cost, residual);
        }

        CelEvalResult<T> result = Result(Plan, ResultType, value, cost, _start);
        return result.IsSuccess
            ? new CelPartialEvalResult<T>(result.Value, cost, residual)
            : new CelPartialEvalResult<T>(result.Errors, cost, residual);
    }

    /// <summary>
    /// Evaluates the program with parts of the data unknown, as <see cref="EvaluatePartial"/> does, and awaits the asynchronous
    /// functions it calls, as <see cref="EvaluateAsync"/> does.
    /// </summary>
    /// <remarks>
    /// The result is unknown only through the patterns: every asynchronous call the result depends on is made. The cost limit
    /// bounds the sum of all passes.
    /// </remarks>
    /// <param name="bindings">The values of the known variables.</param>
    /// <param name="unknowns">The patterns of the unknown parts of the data; empty for none.</param>
    /// <param name="options">The cost limit and the number of asynchronous calls in flight; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">The cancellation token of the evaluation, passed to every asynchronous call.</param>
    /// <returns>The value, the errors, or the unknown parts the result depends on.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bindings"/> or <paramref name="unknowns"/> is <see langword="null"/>, or contains <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The program was not compiled with <see cref="CelCompileOptions.PartialEvaluation"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled. The member does not throw it: the returned task is canceled and throws it
    /// when awaited.
    /// </exception>
    public ValueTask<CelPartialEvalResult<T>> EvaluatePartialAsync(
        CelBindings bindings,
        IReadOnlyList<CelAttributePattern> unknowns,
        CelEvalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        UnknownPattern[] patterns = PartialPatterns(bindings, unknowns, out CelParsedExpr tree);
        try
        {
            return StartPartialAsync(bindings, patterns, tree, options, cancellationToken);
        }
        catch (OperationCanceledException exception)
        {
            return Canceled<CelPartialEvalResult<T>>(exception, cancellationToken);
        }
    }

    // The first pass of an asynchronous partial evaluation, in the call, and the task of the passes after it when the result waits.
    private ValueTask<CelPartialEvalResult<T>> StartPartialAsync(
        CelBindings bindings,
        UnknownPattern[] patterns,
        CelParsedExpr tree,
        CelEvalOptions? options,
        CancellationToken cancellationToken)
    {
        ulong? costLimit = options?.CostLimit ?? _defaultCostLimit;
        Frame frame = Plan.RentFrame(bindings.Values, costLimit, cancellationToken);
        var state = new PartialState(patterns, tree.IdCount, Plan.KeyCount);
        frame.Partial = state;
        CelValue root;
        AsyncCallTable? table;
        ulong charged;
        uint events;
        ValueRecord record = ValueRecord.Empty;
        try
        {
            root = FirstPass(frame, costLimit, out table, out charged, out events);

            // The record is made only when this pass is the last one: otherwise the next pass writes it again.
            if (!Waits(root, table))
            {
                record = state.Release();
            }
        }
        finally
        {
            state.Discard();
        }

        if (Waits(root, table))
        {
            return new AsyncPartialEvaluation<T>(this, bindings, patterns, tree, costLimit, WindowOf(options), table, charged, events, cancellationToken).RunAsync();
        }

        return new ValueTask<CelPartialEvalResult<T>>(PartialResult(tree, root, costLimit is null ? null : charged, new ResidualSource(tree, record)));
    }
}

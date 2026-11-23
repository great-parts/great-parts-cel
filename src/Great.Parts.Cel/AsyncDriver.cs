using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

// The driver of an asynchronous evaluation after its first pass: it starts the calls that the passes registered, awaits them, and
// evaluates the plan again until the root is not an unknown value that waits for a call. It creates no thread, task or timer: it
// calls the delegates of the application and awaits their ValueTask. The passes are synchronous and each rents its frame and gives it
// back before the next await, because the continuation may run on another thread and a frame belongs to the thread that rented it; what
// crosses the awaits is in this object: the table of the calls, the units charged and the events counted.
internal abstract class AsyncDriver<TResult>
{
    private readonly Plan _plan;
    private readonly int _window;
    private readonly AsyncCallTable _table;
    private ulong _charged;
    private uint _events;

    private protected AsyncDriver(Plan plan, ulong? limit, int window, AsyncCallTable table, ulong charged, uint events, CancellationToken cancellationToken)
    {
        _plan = plan;
        Limit = limit;
        _window = window;
        Token = cancellationToken;
        _table = table;
        _charged = charged;
        _events = events;
    }

    // The cost limit of the evaluation, null for none: the limit of every pass, whose remaining part the driver carries.
    private protected ulong? Limit { get; }

    private protected CancellationToken Token { get; }

    // The units charged by all the passes so far, reported only when there is a limit.
    private protected ulong? Cost => Limit is null ? null : _charged;

    // Waits for the calls and evaluates again until the result does not depend on a call, and gives the result.
    internal async ValueTask<TResult> RunAsync()
    {
        while (true)
        {
            Token.ThrowIfCancellationRequested();
            int first = _table.Started;
            await RunWaveAsync().ConfigureAwait(false);
            if (Limit is { } limit && TryChargeWave(first, limit, out CelValue exceeded))
            {
                return Complete(exceeded);
            }

            Token.ThrowIfCancellationRequested();
            CelValue root = Pass();
            if (!root.IsUnknown || !_table.HasPending)
            {
                return Complete(root);
            }
        }
    }

    // The frame of a pass, rented for the evaluation: its bindings, its input and the token.
    private protected abstract Frame Rent();

    // What a pass adds to its frame before it runs, and what it does with it after: whether the root of the pass is the result of the
    // evaluation, or the passes go on.
    private protected virtual void Begin(Frame frame)
    {
    }

    private protected virtual void End(Frame frame, bool last)
    {
    }

    // The result of the evaluation from the root of its last pass.
    private protected abstract TResult Complete(CelValue root);

    // One synchronous pass over the plan, which continues the cost and the event counter of the passes before it.
    private CelValue Pass()
    {
        Frame frame = Rent();
        bool last = false;
        try
        {
            frame.CollectsCalls = true;
            frame.Calls = _table;
            frame.Resume(_charged, _events);
            Begin(frame);
            CelValue root = _plan.Run(frame);
            _charged = frame.ChargedUnits;
            _events = frame.Events;
            last = !root.IsUnknown || !_table.HasPending;
            return root;
        }
        finally
        {
            End(frame, last);
            frame.Release();
        }
    }

    // Starts the calls registered since the last wave, in the order of registration, at most the window of them at a time, and awaits
    // them all in the order of their start. Nothing starts after the token is canceled; the calls that started are awaited all the
    // same, because each received the token and an operation of the application must not outlive the evaluation.
    private async ValueTask RunWaveAsync()
    {
        List<AsyncCall> calls = _table.Calls;
        int end = calls.Count;
        int started = _table.Started;
        int awaited = started;
        for (; started < end; started++)
        {
            while (started - awaited >= _window)
            {
                await SettleAsync(calls[awaited++]).ConfigureAwait(false);
            }

            if (Token.IsCancellationRequested)
            {
                break;
            }

            Start(calls[started]);
        }

        while (awaited < started)
        {
            await SettleAsync(calls[awaited++]).ConfigureAwait(false);
        }

        _table.Started = started;
    }

    // Adds the units of the calls of a wave, which each counted for itself (the copies of the arguments and of the result that the
    // typed delegates make), to the units charged, in the order in which the calls started and not in which they completed, so that the
    // sum does not depend on the time. True with CostLimitExceeded at the first call after which the sum is over the limit, and the
    // units charged are then those before it.
    private bool TryChargeWave(int first, ulong limit, out CelValue error)
    {
        List<AsyncCall> calls = _table.Calls;
        for (int i = first; i < _table.Started; i++)
        {
            AsyncCall call = calls[i];
            if (call.Units > limit - _charged)
            {
                error = EvaluationErrors.CostLimitExceeded(limit, _plan.Lines, call.Offset);
                return true;
            }

            _charged += call.Units;
        }

        error = default;
        return false;
    }

    // Calls the delegate of the call, which may throw before it returns a task. With a limit the call is the meter of its own copies.
    private void Start(AsyncCall call)
    {
        var site = new CallSite(call.Overload.Function, call.Offset, _plan.Lines, Limit is null ? null : call, _plan.Types, null, _plan.TimeZones);
        try
        {
            call.Begin(call.Overload.AsyncImplementation!(call.Arguments, site, Token));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            call.Exception = exception;
            call.State = AsyncCallState.Completed;
        }
    }

    // Awaits a started call and stores its result or its exception; a call that threw when it started has its outcome already.
    private static async ValueTask SettleAsync(AsyncCall call)
    {
        if (call.State != AsyncCallState.Started)
        {
            return;
        }

        ValueTask<CelValue> pending = call.Pending;
        call.Pending = default;
        try
        {
            call.Result = await pending.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            call.Exception = exception;
        }

        call.State = AsyncCallState.Completed;
    }
}

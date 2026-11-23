using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

// The cost limit and the cancellation of an asynchronous evaluation (docs/design/tests.md, "Properties of evaluation"): a limit below
// the cost of the evaluation — the sum of its passes and of the copies of the arguments of the calls — ends it with CostLimitExceeded at
// the same node and the same cost, whatever order the calls complete in; after a cancellation during a wave no new call starts, the calls
// that started are awaited, and the exception is in the returned task.
public sealed class AsyncCostLimitTests
{
    private const ulong Seed = 20261124;
    private const int Cases = 150;
    private const ulong Unlimited = 1_000_000_000;

    [Fact]
    [Trait("Requirement", "R-11")]
    public void LimitBelowTheCost_IsExceededAtTheSameNodeWithTheSameCost() =>
        PropertyLoop.Check(
            nameof(LimitBelowTheCost_IsExceededAtTheSameNodeWithTheSameCost),
            AsyncExpressions.Of(AsyncMode.Values),
            Seed,
            Cases,
            Violation,
            testCase => testCase.ToString());

    private static string? Violation(AsyncCase testCase)
    {
        var calls = new AsyncCalls();
        CelProgram<long> program = calls.Async.Compile<long>(testCase.Text).GetProgramOrThrow();
        ulong? total = Run(program, Unlimited).Cost;
        if (total is not { } cost || cost < 2)
        {
            return null;
        }

        // A limit that depends on the text only, so that the case is the same in every run.
        ulong limit = 1 + ((ulong)testCase.Text.Sum(letter => letter) % (cost - 1));
        CelEvalResult<long> first = Run(program, limit);
        string? wrong = Wrong(first, limit);
        for (int repeat = 0; repeat < 3 && wrong is null; repeat++)
        {
            CelEvalResult<long> again = Run(program, limit);
            wrong = Wrong(again, limit)
                ?? (Where(first) == Where(again) && first.Cost == again.Cost ? null : $"{Where(first)} with cost {first.Cost}, then {Where(again)} with cost {again.Cost}");
        }

        return wrong is null ? null : $"limit {limit} of {cost}: {wrong}";
    }

    private static string? Wrong(CelEvalResult<long> result, ulong limit)
    {
        if (result.IsSuccess || result.Errors.Single().Code != CelErrorCode.CostLimitExceeded)
        {
            return "the limit was not exceeded";
        }

        return result.Cost <= limit ? null : $"cost {result.Cost} above the limit";
    }

    private static string Where(CelEvalResult<long> result) =>
        result.Errors.Single().Location.Offset.ToString(CultureInfo.InvariantCulture);

    private static CelEvalResult<long> Run(CelProgram<long> program, ulong limit) => Run(program, AsyncCalls.Bindings(), limit);

    private static CelEvalResult<long> Run(CelProgram<long> program, CelBindings bindings, ulong limit) =>
        program.EvaluateAsync(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None).AsTask().GetAwaiter().GetResult();

    // Three calls with typed list arguments, whose copies are most of the cost: a limit anywhere below the total stops the evaluation at
    // the same call whatever order the calls complete in.
    [Fact]
    [Trait("Requirement", "R-11")]
    public void LimitBelowTheCopiesOfArguments_IsExceededAtTheSameCall()
    {
        for (int index = 0; index < 8; index++)
        {
            var calls = new AsyncCalls();
            CelProgram<long> program = calls.Async.Compile<long>("takes(l1) + takes(l2) + takes(l3)").GetProgramOrThrow();
            CelBindings bindings = new CelBindings()
                .Add("l1", AsyncCalls.Ints(20 + (index * 13), 0))
                .Add("l2", AsyncCalls.Ints(30 + (index * 7), 0))
                .Add("l3", AsyncCalls.Ints(40 + (index * 5), 0));
            ulong cost = Run(program, bindings, Unlimited).Cost!.Value;

            foreach (ulong limit in new[] { 1UL, cost / 4, cost / 2, cost * 3 / 4, cost - 1 })
            {
                CelEvalResult<long> first = Run(program, bindings, limit);

                Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(first.Errors).Code);
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    CelEvalResult<long> again = Run(program, bindings, limit);

                    Assert.Equal(first.Cost, again.Cost);
                    Assert.Equal(Where(first), Where(again));
                }
            }
        }
    }

    // The signal comes at the k-th call that starts, whatever the window: no call starts after it, the calls that started are awaited,
    // and the exception is in the task, not thrown by the call.
    [Fact]
    [Trait("Requirement", "+R-11")]
    public async Task CancellationDuringAWave_StartsNoCallAndAwaitsTheStartedOnes()
    {
        for (int index = 0; index < 30; index++)
        {
            var calls = new AsyncCalls();
            CelProgram<long> program = calls.Async.Compile<long>("l.map(i, inc(i)).size()").GetProgramOrThrow();
            CelBindings bindings = new CelBindings().Add("l", AsyncCalls.Ints(20 + (index * 6), 0));
            var options = new CelEvalOptions { MaxConcurrentAsyncCalls = 1 + (index % 10) };
            int signalAt = 1 + (index % 7);
            using var source = new CancellationTokenSource();
            int seen = 0;
            calls.OnStart = _ =>
            {
                if (Interlocked.Increment(ref seen) == signalAt)
                {
                    source.Cancel();
                }
            };

            ValueTask<CelEvalResult<long>> task = program.EvaluateAsync(bindings, options, source.Token);

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task.ConfigureAwait(true)).ConfigureAwait(true);
            Assert.Equal(signalAt, calls.Started);
            Assert.Equal(0, calls.StartedAfterCancel);
            Assert.Equal(0, calls.Running);
        }
    }
}

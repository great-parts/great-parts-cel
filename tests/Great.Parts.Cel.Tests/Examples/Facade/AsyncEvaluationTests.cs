using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Kinds;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Tests.Examples.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The asynchronous driver: the synchronous completion of the first pass, the passes and the waves, the calls that run once, the window
// of calls in flight, the cost and the cancellation across the passes, and the asynchronous inputs, partial evaluation and one-step calls.
public sealed class AsyncEvaluationTests
{
    private readonly AsyncFunctions functions = new();

    private static CelProgram<T> Compile<T>(CelEnvironment environment, string expression) =>
        environment.Compile<T>(expression).GetProgramOrThrow();

    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "R-9")]
    public void ProgramWithoutAsyncCalls_CompletesSynchronously()
    {
        CelEvalResult<long> result = Completed(Compile<long>(functions.Async, "x + 1").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None));

        Assert.Equal(2L, result.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void CallThatIsNotNeeded_IsNotMadeAndTheTaskIsComplete()
    {
        CelEvalResult<bool> result = Completed(Compile<bool>(functions.Async, "false && inc(1) > 0").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None));

        Assert.False(result.GetValueOrThrow());
        Assert.Equal(0, functions.Started);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task ACallWithKnownArguments_IsMadeOnce()
    {
        CelEvalResult<long> result = await Compile<long>(functions.Async, "inc(1) + 1").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3L, result.GetValueOrThrow());
        Assert.Equal(1, functions.CallsOf(1));
    }

    // f(g(x)) is three passes: the depth of the chain plus one. probe is evaluated in every pass.
    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "R-9")]
    public async Task ChainOfDependentCalls_IsOnePassMoreThanItsDepth()
    {
        CelEvalResult<long> result = await Compile<long>(functions.Async, "probe(0) + inc(inc(1))").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3L, result.GetValueOrThrow());
        Assert.Equal(3, functions.Probes);
        Assert.Equal(1, functions.CallsOf(1));
        Assert.Equal(1, functions.CallsOf(2));
    }

    // Two nodes of the text are two calls, as in cel-go.
    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task SameCallAtTwoPlaces_IsTwoCalls()
    {
        CelEvalResult<long> result = await Compile<long>(functions.Async, "inc(1) + inc(1)").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(4L, result.GetValueOrThrow());
        Assert.Equal(2, functions.CallsOf(1));
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task SameCallInALoop_IsMadeOncePerDistinctArguments()
    {
        CelEvalResult<bool> result = await Compile<bool>(functions.Async, "[1, 1, 2].map(i, inc(i)) == [2, 2, 3]").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.GetValueOrThrow());
        Assert.Equal(1, functions.CallsOf(1));
        Assert.Equal(1, functions.CallsOf(2));
    }

    // 1.0 and 1 are equal by CEL equality: one call, as in cel-go.
    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task ArgumentsEqualByCel_AreOneCall()
    {
        CelEvalResult<bool> result = await Compile<bool>(functions.Async, "[1.0, 1].map(i, incd(i)) == [2, 2]").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.GetValueOrThrow());
        Assert.Equal(1, functions.IncdCalls);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task WindowOfTwo_HoldsAtMostTwoCallsInFlight()
    {
        var options = new CelEvalOptions { MaxConcurrentAsyncCalls = 2 };

        CelEvalResult<bool> result = await Compile<bool>(functions.Async, "[1, 2, 3].map(i, inc(i)) == [2, 3, 4]").EvaluateAsync(AsyncFunctions.Bindings(), options, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.GetValueOrThrow());
        Assert.Equal(2, functions.Highest);
        Assert.Equal(3, functions.Started);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task DefaultWindow_IsAHundred()
    {
        CelBindings bindings = new CelBindings().Add("l", PartialTestSupport.IntList([.. Enumerable.Range(1, 300).Select(index => (long)index)]));

        CelEvalResult<long> result = await Compile<long>(functions.Async, "l.map(i, inc(i)).size()").EvaluateAsync(bindings, null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(300L, result.GetValueOrThrow());
        Assert.Equal(300, functions.Started);
        Assert.InRange(functions.Highest, 2, CelEvalOptions.DefaultMaxConcurrentAsyncCalls);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void WindowBelowOne_IsRefused()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CelEvalOptions { MaxConcurrentAsyncCalls = 0 });
    }

    // An error of a call does not end the wave: the pass decides whether it matters.
    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task ErrorOfACall_DoesNotMatterWhenTheOtherOperandDecides()
    {
        CelEvalResult<bool> result = await Compile<bool>(functions.Async, "fail(1) > 0 || inc(1) > 0").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task ErrorOfACall_WaitsForTheOtherCallsOfTheWave()
    {
        CelEvalResult<long> result = await Compile<long>(functions.Async, "fail(1) + inc(1)").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(result.Errors).Code);
        Assert.Equal(1, functions.CallsOf(1));
    }

    // The same value and the same cost in every repeat, whatever order the calls complete in.
    [Fact]
    [Trait("Requirement", "R-9")]
    [Trait("Requirement", "R-11")]
    public async Task ValueAndCost_DoNotDependOnTheOrderOfCompletion()
    {
        CelProgram<long> program = Compile<long>(functions.Async, "inc(x) + inc(y) + inc(x + y)");
        var options = new CelEvalOptions { CostLimit = 10_000 };
        CelEvalResult<long> first = await program.EvaluateAsync(AsyncFunctions.Bindings(), options, CancellationToken.None).ConfigureAwait(true);

        for (int repeat = 0; repeat < 100; repeat++)
        {
            CelEvalResult<long> result = await program.EvaluateAsync(AsyncFunctions.Bindings(), options, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(first.GetValueOrThrow(), result.GetValueOrThrow());
            Assert.Equal(first.Cost, result.Cost);
        }

        Assert.Equal(9L, first.GetValueOrThrow());
    }

    // The limit is for the sum of the passes: the third pass of inc(inc(1)) is the one that exceeds it, at the same node every time.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task LimitBelowTheSumOfThePasses_IsExceededInALaterPassIdentically()
    {
        CelProgram<long> program = Compile<long>(functions.Async, "inc(inc(1))");
        ulong total = (await program.EvaluateAsync(AsyncFunctions.Bindings(), new CelEvalOptions { CostLimit = 10_000 }, CancellationToken.None).ConfigureAwait(true)).Cost!.Value;
        var options = new CelEvalOptions { CostLimit = total - 1 };
        CelEvalResult<long> first = await program.EvaluateAsync(AsyncFunctions.Bindings(), options, CancellationToken.None).ConfigureAwait(true);

        CelError error = Assert.Single(first.Errors);
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
        Assert.True(first.Cost <= total - 1);
        for (int repeat = 0; repeat < 20; repeat++)
        {
            CelEvalResult<long> result = await program.EvaluateAsync(AsyncFunctions.Bindings(), options, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(first.Cost, result.Cost);
            Assert.Equal(error.Location.Offset, Assert.Single(result.Errors).Location.Offset);
        }
    }

    // Each pass charges what it evaluates: the cost of the evaluation is the cost of the first pass plus that of the second.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task CostOfTheEvaluation_IsTheSumOfThePasses()
    {
        var options = new CelEvalOptions { CostLimit = 10_000 };
        CelEvalResult<long> result = await Compile<long>(functions.Async, "inc(1) + 1").EvaluateAsync(AsyncFunctions.Bindings(), options, CancellationToken.None).ConfigureAwait(true);
        ulong call = SynchronousResult(functions.Sync, "inc(1)", options).Cost!.Value;
        ulong literal = SynchronousResult(functions.Sync, "1", options).Cost!.Value;
        ulong whole = SynchronousResult(functions.Sync, "inc(1) + 1", options).Cost!.Value;

        // The first pass evaluates the call and the literal, the second the whole expression.
        Assert.Equal((ulong?)(call + literal + whole), result.Cost);
    }

    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task EvaluationWithoutALimit_CountsNothing()
    {
        CelEvalResult<long> result = await Compile<long>(functions.Async, "inc(inc(1))").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Null(result.Cost);
    }

    [Fact]
    [Trait("Requirement", "+R-11")]
    public async Task Cancellation_ReachesTheCallThatWaits()
    {
        using var source = new CancellationTokenSource();
        source.CancelAfter(TimeSpan.FromMilliseconds(10));
        CelProgram<long> program = Compile<long>(functions.Async, "slow(1)");

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await program.EvaluateAsync(AsyncFunctions.Bindings(), null, source.Token).ConfigureAwait(true)).ConfigureAwait(true);
    }

    // No call starts after the signal; the calls that started are awaited.
    [Fact]
    [Trait("Requirement", "+R-11")]
    public async Task Cancellation_StartsNoNewCallsAfterTheSignal()
    {
        using var source = new CancellationTokenSource();
        functions.OnStart = _ =>
        {
            if (functions.Started == 10)
            {
                source.Cancel();
            }
        };
        CelBindings bindings = new CelBindings().Add("l", PartialTestSupport.IntList([.. Enumerable.Range(1, 200).Select(index => (long)index)]));
        var options = new CelEvalOptions { MaxConcurrentAsyncCalls = 10 };
        CelProgram<long> program = Compile<long>(functions.Async, "l.map(i, inc(i)).size()");

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await program.EvaluateAsync(bindings, options, source.Token).ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Equal(10, functions.Started);
    }

    // A canceled token changes nothing for a program that needs no event, as for Evaluate: only a macro step or a call checks it.
    [Fact]
    [Trait("Requirement", "+R-11")]
    public async Task CanceledToken_ValueWithoutEvents_ExceptionWithAMacroOrACall()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync().ConfigureAwait(true);

        Assert.Equal(2L, (await Compile<long>(functions.Async, "x + 1").EvaluateAsync(AsyncFunctions.Bindings(), null, source.Token).ConfigureAwait(true)).GetValueOrThrow());
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Compile<bool>(functions.Async, "l.all(i, i > 0)").EvaluateAsync(AsyncFunctions.Bindings(), null, source.Token).ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Compile<long>(functions.Async, "inc(1)").EvaluateAsync(AsyncFunctions.Bindings(), null, source.Token).ConfigureAwait(true)).ConfigureAwait(true);
        Assert.Equal(0, functions.Started);
    }

    // The cancellation of the first pass is in the returned task, as that of every later pass: the call does not throw, the task is
    // canceled, and awaiting it throws. The one-step calls and the partial member do the same.
    [Fact]
    [Trait("Requirement", "+R-11")]
    public async Task CanceledToken_InTheFirstPass_IsInTheTaskAndNotThrownByTheCall()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync().ConfigureAwait(true);
        CelProgram<bool> program = Compile<bool>(functions.Async, "l.all(i, i > 0)");
        CelProgram<bool> partial = functions.Async.Compile<bool>("l.all(i, i > 0)", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();

        ValueTask<CelEvalResult<bool>> evaluate = program.EvaluateAsync(AsyncFunctions.Bindings(), null, source.Token);
        ValueTask<CelPartialEvalResult<bool>> evaluatePartial = partial.EvaluatePartialAsync(AsyncFunctions.Bindings(), [], null, source.Token);
        ValueTask<CelEvalResult<bool>> oneStep = functions.Async.EvaluateAsync<bool>("l.all(i, i > 0)", AsyncFunctions.Bindings(), cancellationToken: source.Token);
        ValueTask<CelEvalResult<bool>> cached = new CelCompilationCache(functions.Async).EvaluateAsync<bool>("l.all(i, i > 0)", AsyncFunctions.Bindings(), cancellationToken: source.Token);

        Assert.True(evaluate.IsCanceled);
        Assert.True(evaluatePartial.IsCanceled);
        Assert.True(oneStep.IsCanceled);
        Assert.True(cached.IsCanceled);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await evaluate.ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await evaluatePartial.ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await oneStep.ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cached.ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task EvaluateInputAsync_OnAClass_WaitsForTheCall()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddTypes(ShopContext.Default)
            .UseInput<Order>()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1))
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<bool> program = Compile<bool>(environment, "total > 1000.0 && inc(1) == 2");

        Assert.True((await program.EvaluateInputAsync(new Order { Total = 1500.0 }, null, null, CancellationToken.None).ConfigureAwait(true)).GetValueOrThrow());
        Assert.False((await program.EvaluateInputAsync(new Order { Total = 10.0 }, null, null, CancellationToken.None).ConfigureAwait(true)).GetValueOrThrow());
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await program.EvaluateInputAsync<Order>(null!, null, null, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAsync<ArgumentException>(async () => await program.EvaluateInputAsync(new StructKind(), null, null, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task EvaluateInputAsync_OnAStructure_WaitsForTheCall()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddTypes(KindsContext.Default)
            .UseInput<StructKind>()
            .AddAsyncFunction("inc", async (long value, CancellationToken token) =>
            {
                await Task.Yield();
                return value + 1;
            })
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<bool> program = Compile<bool>(environment, "id > 0 && inc(id) == name.size() + 1");

        CelEvalResult<bool> result = await program.EvaluateInputAsync(new StructKind { Id = 4, Name = "abcd" }, null, null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.GetValueOrThrow());
    }

    // A structure that completes in the first pass is not boxed for it: the asynchronous call allocates what the synchronous one does.
    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "P-4")]
    public void EvaluateInputAsync_OnAStructure_AllocatesAsTheSynchronousCallDoes()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<StructKind>());
        CelProgram<bool> program = Compile<bool>(environment, "id > 0 && name != ''");
        var structure = new StructKind { Id = 1, Name = "n" };

        long synchronous = Smallest(() => program.EvaluateInput(structure, cancellationToken: CancellationToken.None).GetValueOrThrow());
        long asynchronous = Smallest(() => Completed(program.EvaluateInputAsync(structure, null, null, CancellationToken.None)).GetValueOrThrow());

        Assert.True(asynchronous <= synchronous, $"{asynchronous} bytes for the asynchronous call, {synchronous} for the synchronous one");
    }

    // A program that completes in the first pass costs the asynchronous call no more memory than the synchronous one.
    [Fact]
    [Trait("Requirement", "P-4")]
    public void EvaluateAsync_WhenTheFirstPassCompletes_AllocatesAsEvaluateDoes()
    {
        CelProgram<long> sync = Compile<long>(functions.Sync, "x + 1");
        CelProgram<long> asyncProgram = Compile<long>(functions.Async, "x + 1");
        CelProgram<bool> notNeeded = Compile<bool>(functions.Async, "false && inc(1) > 0");
        CelBindings bindings = AsyncFunctions.Bindings();

        long evaluate = Smallest(() => sync.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow() == 2);
        long evaluateAsync = Smallest(() => Completed(asyncProgram.EvaluateAsync(bindings, null, CancellationToken.None)).GetValueOrThrow() == 2);
        long withACall = Smallest(() => !Completed(notNeeded.EvaluateAsync(bindings, null, CancellationToken.None)).GetValueOrThrow());

        Assert.True(evaluateAsync <= evaluate, $"{evaluateAsync} bytes for EvaluateAsync, {evaluate} for Evaluate");
        Assert.True(withACall <= evaluate, $"{withACall} bytes for a program with a call that is not needed, {evaluate} for Evaluate");
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    [Trait("Requirement", "F-23")]
    public async Task EvaluatePartialAsync_MakesTheCallsAndLeavesTheUnknownAttributes()
    {
        CelProgram<long> program = functions.Async.Compile<long>("inc(x) + y", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();

        CelPartialEvalResult<long> result = await program.EvaluatePartialAsync(AsyncFunctions.Bindings(), [PartialTestSupport.P("y")], null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.IsUnknown);
        CelUnknown unknown = Assert.Single(result.Unknowns);
        Assert.Equal("y", unknown.Attribute!.ToString());
        Assert.Equal(1, functions.CallsOf(1));
        Assert.Equal("2 + y", CelSyntax.Format(result.GetResidual()));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    [Trait("Requirement", "F-23")]
    public async Task EvaluatePartialAsync_DoesNotCallWithAnUnknownArgument()
    {
        CelProgram<long> program = functions.Async.Compile<long>("inc(x)", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();

        CelPartialEvalResult<long> result = await program.EvaluatePartialAsync(AsyncFunctions.Bindings(), [PartialTestSupport.P("x")], null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.IsUnknown);
        Assert.Equal("x", Assert.Single(result.Unknowns).Attribute!.ToString());
        Assert.Equal(0, functions.Started);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    [Trait("Requirement", "F-23")]
    public async Task EvaluatePartialAsync_GivesTheValueWhenNoUnknownAffectsIt()
    {
        CelProgram<long> program = functions.Async.Compile<long>("inc(x) + 1", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();

        CelPartialEvalResult<long> result = await program.EvaluatePartialAsync(AsyncFunctions.Bindings(), [PartialTestSupport.P("y")], null, CancellationToken.None).ConfigureAwait(true);

        Assert.True(result.IsSuccess);
        Assert.Equal(3L, result.Value);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    [Trait("Requirement", "F-23")]
    public void EvaluatePartial_OfACallThatWaits_IsAnUnknownThatDescribesIt()
    {
        CelProgram<bool> program = functions.Async.Compile<bool>("inc(x) > 0", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();

        CelPartialEvalResult<bool> result = program.EvaluatePartial(AsyncFunctions.Bindings(), [], null, CancellationToken.None);

        Assert.True(result.IsUnknown);
        CelUnknown unknown = Assert.Single(result.Unknowns);
        Assert.Equal("inc", unknown.Function);
        Assert.Equal("inc_int", unknown.OverloadId);
        Assert.Null(unknown.Attribute);
        Assert.True(unknown.Arguments[0].TryGetInt64(out long argument));
        Assert.Equal(1L, argument);
        Assert.Equal(0, functions.Started);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    [Trait("Requirement", "F-23")]
    public async Task EvaluatePartialAsync_Arguments_AreChecked()
    {
        CelProgram<long> program = functions.Async.Compile<long>("inc(x)", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();
        CelProgram<long> withoutFlag = Compile<long>(functions.Async, "inc(x)");
        CelBindings bindings = AsyncFunctions.Bindings();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await program.EvaluatePartialAsync(null!, [], null, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await program.EvaluatePartialAsync(bindings, null!, null, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await withoutFlag.EvaluatePartialAsync(bindings, [], null, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "+R-13")]
    public async Task OneStepCallOfTheEnvironment_EvaluatesAsynchronously()
    {
        CelEvalResult<long> value = await functions.Async.EvaluateAsync<long>("inc(1)", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).ConfigureAwait(true);
        CelEvalResult<long> failed = Completed(functions.Async.EvaluateAsync<long>("1 +", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None));

        Assert.Equal(2L, value.GetValueOrThrow());
        Assert.False(failed.IsSuccess);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await functions.Async.EvaluateAsync<long>(null!, AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await functions.Async.EvaluateAsync<long>("1", null!, cancellationToken: CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "+R-13")]
    public async Task OneStepCallWithAnInput_EvaluatesAsynchronously()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddTypes(KindsContext.Default)
            .UseInput<ClassKind>()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1))
            .Build()
            .GetEnvironmentOrThrow();

        CelEvalResult<long> result = await environment.EvaluateInputAsync<long>("inc(id)", new ClassKind { Id = 4 }, cancellationToken: CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(5L, result.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "+R-13")]
    public async Task CallThroughTheCache_CompilesOnce()
    {
        var cache = new CelCompilationCache(functions.Async);

        CelEvalResult<long> first = await cache.EvaluateAsync<long>("inc(1)", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).ConfigureAwait(true);
        CelEvalResult<long> second = await cache.EvaluateAsync<long>("inc(1)", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(2L, first.GetValueOrThrow());
        Assert.Equal(2L, second.GetValueOrThrow());
        Assert.Equal(1, cache.Kept.Count);
    }

    [Fact]
    [Trait("Requirement", "R-4")]
    public async Task ThousandParallelEvaluations_GiveTheResultsOfOne()
    {
        CelProgram<long> program = Compile<long>(functions.Async, "inc(x) + inc(inc(y))");

        long[] results = await Task.WhenAll(Enumerable.Range(0, 1000).Select(_ => Task.Run(
            async () => (await program.EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true)).GetValueOrThrow(),
            CancellationToken.None))).ConfigureAwait(true);

        Assert.All(results, result => Assert.Equal(6L, result));
    }

    // A NaN equals a NaN at any depth, so the call that holds one is found again in the next pass: incd runs once and the evaluation
    // ends, with a limit and without. A call that is not found again is made in every pass and the evaluation without a limit never
    // ends, so the evaluation has a time of its own: a regression ends the test with a cancellation, not a hang.
    [Theory]
    [Trait("Requirement", "R-11")]
    [InlineData("incd(0.0 / 0.0) == 2")]
    [InlineData("incd([0.0 / 0.0]) == 2")]
    [InlineData("incd([0.0 / 0.0, 1.0]) == 2")]
    [InlineData("incd({'a': 0.0 / 0.0}) == 2")]
    [InlineData("incd(optional.of(0.0 / 0.0)) == 2")]
    public async Task ACallWithANaNInItsArgument_IsFoundAgainInEveryPass(string expression)
    {
        CelProgram<bool> program = Compile<bool>(functions.Async, expression);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        CelEvalResult<bool> unlimited = await program.EvaluateAsync(AsyncFunctions.Bindings(), null, timeout.Token).ConfigureAwait(true);
        CelEvalResult<bool> limited = await program.EvaluateAsync(AsyncFunctions.Bindings(), new CelEvalOptions { CostLimit = 10_000 }, timeout.Token).ConfigureAwait(true);

        Assert.True(unlimited.GetValueOrThrow());
        Assert.True(limited.GetValueOrThrow());
        Assert.Equal(2, functions.IncdCalls);
    }

    // The lists come from the data: the rows with a NaN are one call, the other row is another.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task ACallWithANaNFromTheData_IsMadeOncePerDistinctValue()
    {
        CelEnvironment environment = functions.Async.ToBuilder()
            .AddVariable("rows", CelType.ListOf(CelType.ListOf(CelType.Double)))
            .Build()
            .GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("rows", CelValue.FromList(CelList.Create([RowOf(double.NaN), RowOf(double.NaN), RowOf(1.0)])));

        CelEvalResult<long> result = await Compile<long>(environment, "rows.map(r, incd(r)).size()").EvaluateAsync(bindings, null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(3L, result.GetValueOrThrow());
        Assert.Equal(2, functions.IncdCalls);
    }

    // One thousand identifiers above 2^53 are one thousand calls and two passes: the first registers them all, the second finds them all.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task ThousandCallsWithIntegersAbove2To53_AreMadeOnceInTwoPasses()
    {
        long[] identifiers = [.. Enumerable.Range(0, 1000).Select(index => (1L << 60) + index)];
        CelBindings bindings = new CelBindings().Add("l", PartialTestSupport.IntList(identifiers));

        CelEvalResult<long> result = await Compile<long>(functions.Async, "probe(0) + l.map(i, incd(i)).size()").EvaluateAsync(bindings, null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(1000L, result.GetValueOrThrow());
        Assert.Equal(1000, functions.IncdCalls);
        Assert.Equal(2, functions.Probes);
    }

    // Without a limit nothing is charged, so fifty objects of the application are fifty calls whatever the cost of finding them.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task CallsWithObjectsOfTheApplication_AreMadeOnceEachWithoutALimit()
    {
        CelEvalResult<long> result = await Compile<long>(OrdersEnvironment(), "orders.map(o, incd(o)).size()")
            .EvaluateAsync(OrdersBindings(OrdersOf(50)), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(50L, result.GetValueOrThrow());
        Assert.Equal(50, functions.IncdCalls);
    }

    // Finding a call whose argument is an object compares it with the objects of the calls before it, and that reads the fields of the
    // application: the comparisons are charged, and the unit that passes the limit is the error of the node of the call.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task ComparisonsOfObjectsInTheLookup_AreChargedAgainstTheLimit()
    {
        const string Expression = "orders.map(o, incd(o)).size()";
        CelEvalResult<long> result = await Compile<long>(OrdersEnvironment(), Expression)
            .EvaluateAsync(OrdersBindings(OrdersOf(50)), new CelEvalOptions { CostLimit = 6_000 }, CancellationToken.None).ConfigureAwait(true);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
        int call = Expression.IndexOf("incd(o)", StringComparison.Ordinal);
        Assert.InRange(error.Location.Offset, call, call + "incd(o)".Length);
        Assert.Equal(0, functions.IncdCalls);
    }

    // The advice of the page: a key instead of an object costs the calls only.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task CallsWithAKeyInsteadOfAnObject_FitTheSameLimit()
    {
        CelEvalResult<long> result = await Compile<long>(OrdersEnvironment(), "orders.map(o, incd(o.id)).size()")
            .EvaluateAsync(OrdersBindings(OrdersOf(50)), new CelEvalOptions { CostLimit = 6_000 }, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(50L, result.GetValueOrThrow());
        Assert.Equal(50, functions.IncdCalls);
    }

    // Two values that are one instance are equal without reading a field and without a charge, so a list of the same object under a limit
    // that fifty different objects pass is one call.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task TheSameInstanceInManyCalls_IsFoundWithoutAChargeAndIsOneCall()
    {
        var shared = new Order { Id = "shared" };
        Order[] same = [.. Enumerable.Repeat(shared, 50)];

        CelEvalResult<long> result = await Compile<long>(OrdersEnvironment(), "orders.map(o, incd(o)).size()")
            .EvaluateAsync(OrdersBindings(same), new CelEvalOptions { CostLimit = 6_000 }, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(50L, result.GetValueOrThrow());
        Assert.Equal(1, functions.IncdCalls);
    }

    // An object with a NaN in a field: the instance that is the same in every pass, and two instances that are equal in every field, as an
    // object that the input creates anew at each read is.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task ObjectsWithANaNInAField_AreOneCallWhenTheirFieldsAreEqual()
    {
        Order[] oneInstance = [new Order { Total = double.NaN }];
        Order[] twoInstances = [new Order { Total = double.NaN }, new Order { Total = double.NaN }];
        CelProgram<long> program = Compile<long>(OrdersEnvironment(), "orders.map(o, incd(o)).size()");

        CelEvalResult<long> first = await program.EvaluateAsync(OrdersBindings(oneInstance), null, CancellationToken.None).ConfigureAwait(true);
        CelEvalResult<long> second = await program.EvaluateAsync(OrdersBindings(twoInstances), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(1L, first.GetValueOrThrow());
        Assert.Equal(2L, second.GetValueOrThrow());
        Assert.Equal(2, functions.IncdCalls);
    }

    private CelEnvironment OrdersEnvironment() => functions.Async.ToBuilder()
        .AddTypes(ShopContext.Default)
        .AddVariable("orders", CelType.ListOf(CelType.ObjectNamed("shop.Order")))
        .Build()
        .GetEnvironmentOrThrow();

    private static CelBindings OrdersBindings(Order[] orders) => new CelBindings().Add("orders", orders);

    private static Order[] OrdersOf(int count) =>
        [.. Enumerable.Range(0, count).Select(index => new Order { Id = string.Create(CultureInfo.InvariantCulture, $"o{index}") })];

    private static CelValue RowOf(double value) => CelValue.FromList(CelList.Create([CelValue.FromDouble(value)]));

    // The asynchronous evaluation gives what the synchronous one gives with the synchronous twins of the functions.
    [Theory]
    [Trait("Requirement", "R-9")]
    [InlineData("inc(1) + 1")]
    [InlineData("inc(inc(1)) * 2")]
    [InlineData("inc(x) + inc(y)")]
    [InlineData("name('a').size() + inc(2)")]
    [InlineData("'abc'.upper().size() + inc(1)")]
    [InlineData("[1, 2, 3].map(i, inc(i)).size()")]
    [InlineData("fail(1) + 1")]
    [InlineData("fail(1) + inc(1)")]
    [InlineData("inc(1) / (inc(0) - 1)")]
    public async Task AsynchronousEvaluation_EqualsTheSynchronousOneWithTwins(string expression)
    {
        CelEvalResult<long> expected = SynchronousResult(functions.Sync, expression, null);

        CelEvalResult<long> actual = await Compile<long>(functions.Async, expression).EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(expected.IsSuccess, actual.IsSuccess);
        if (expected.IsSuccess)
        {
            Assert.Equal(expected.Value, actual.Value);
        }
        else
        {
            CelError want = Assert.Single(expected.Errors);
            CelError got = Assert.Single(actual.Errors);
            Assert.Equal((want.Code, want.Location.Offset), (got.Code, got.Location.Offset));
        }
    }

    // A strict call whose left operand waits and whose right operand is an error: the error is the result at once, because it takes
    // precedence over the unknown value that stands for the call. The synchronous twin fails in the left operand first, so the class of
    // the outcome is the same and the code and the place of the error are not.
    [Fact]
    [Trait("Requirement", "R-9")]
    public async Task ErrorOnTheRightOfAWaitingCall_TakesPrecedenceAndOnlyTheClassOfTheOutcomeIsTheSame()
    {
        CelEvalResult<long> synchronous = SynchronousResult(functions.Sync, "fail(1) + 1 / 0", null);

        CelEvalResult<long> asynchronous = await Compile<long>(functions.Async, "fail(1) + 1 / 0").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.False(synchronous.IsSuccess);
        Assert.False(asynchronous.IsSuccess);
        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(synchronous.Errors).Code);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(asynchronous.Errors).Code);
    }

    // The synchronous evaluation of an expression of the environment with the synchronous twins of the functions.
    private static CelEvalResult<long> SynchronousResult(CelEnvironment environment, string expression, CelEvalOptions? options) =>
        Compile<long>(environment, expression).Evaluate(AsyncFunctions.Bindings(), options, CancellationToken.None);

    // The result of a task that the call has already completed: the first pass finished, so nothing was awaited.
    private static T Completed<T>(ValueTask<T> task)
    {
        Assert.True(task.IsCompletedSuccessfully);
        return task.GetAwaiter().GetResult();
    }

    private static long Smallest(Func<bool> action)
    {
        Assert.True(action());
        Assert.True(action());

        long smallest = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool result = action();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(result);
            smallest = Math.Min(smallest, allocated);
        }

        return smallest;
    }
}

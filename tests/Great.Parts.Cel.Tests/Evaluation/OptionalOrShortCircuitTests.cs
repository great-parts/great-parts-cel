using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The calls or and orValue of the set optional are special forms: the node evaluates the receiver, charges its own unit, and evaluates
// the argument only for an empty receiver (docs/design/evaluation.md, "Optional values and the optional syntax"). Three things follow
// and are shown here: the argument is not run — no application function is called and no error of it is raised; its cost is not
// charged, so a cost limit that the argument alone would pass is not reached; and its memory is not allocated, and a limit that stops
// the argument stops it before its result is built.
public sealed class OptionalOrShortCircuitTests
{
    private int calls;

    private readonly CelEnvironment environment;

    public OptionalOrShortCircuitTests()
    {
        environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Optional(2))
            .AddFunction("tick", () => ++calls > 0)
            .AddVariable("n", CelType.Int)
            .AddVariable("x", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();
    }

    [Fact]
    public void ArgumentOfOr_IsNotRunForAReceiverThatHoldsAValue_AndIsRunOnceForAnEmptyOne()
    {
        _ = Evaluate("optional.of(1).or(optional.of(tick() ? 2 : 3))", new CelBindings());
        Assert.Equal(0, calls);

        _ = Evaluate("optional.none().or(optional.of(tick() ? 2 : 3))", new CelBindings());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ArgumentOfOrValue_IsNotRunForAReceiverThatHoldsAValue_AndIsRunOnceForAnEmptyOne()
    {
        _ = Evaluate("optional.of(1).orValue(tick() ? 2 : 3)", new CelBindings());
        Assert.Equal(0, calls);

        _ = Evaluate("optional.none().orValue(tick() ? 2 : 3)", new CelBindings());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ErrorOfTheArgument_IsRaisedForAnEmptyReceiverOnly()
    {
        Assert.True(Evaluate("optional.of(1).orValue(1 / 0)", new CelBindings()).GetValueOrThrow().TryGetInt64(out long one));
        Assert.Equal(1L, one);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(Evaluate("optional.none().orValue(1 / 0)", new CelBindings()).Errors).Code);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(Evaluate("optional.none().or(optional.of(1 / 0))", new CelBindings()).Errors).Code);
    }

    // optional.of(1).or(optional.of(2)) costs 2, the call of optional.of and the call of or; the right part adds 1 only for an empty
    // receiver. The limit 2 is a total that is allowed and a total of 3 is not.
    [Fact]
    public void LimitThatTheArgumentWouldExceed_IsNotReachedForAReceiverThatHoldsAValue()
    {
        var limit = new CelEvalOptions { CostLimit = 2 };

        CelValue held = Evaluate("optional.of(1).or(optional.of(2))", new CelBindings(), limit).GetValueOrThrow();
        CelError error = Assert.Single(Evaluate("optional.none().or(optional.of(2))", new CelBindings(), limit).Errors);

        Assert.Equal(1L, HeldOf(held));
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
        Assert.Equal(2L, HeldOf(Evaluate("optional.none().or(optional.of(2))", new CelBindings(), new CelEvalOptions { CostLimit = 3 }).GetValueOrThrow()));
    }

    // n is read for 1 unit: optional.of(1).orValue(n) costs 2 and optional.none().orValue(n) 3.
    [Fact]
    public void LimitOfOrValue_IsExceededByTheArgumentOfAnEmptyReceiverOnly()
    {
        CelBindings bindings = new CelBindings().Add("n", CelValue.FromInt64(7));
        var limit = new CelEvalOptions { CostLimit = 2 };

        CelValue held = Evaluate("optional.of(1).orValue(n)", bindings, limit).GetValueOrThrow();
        CelError error = Assert.Single(Evaluate("optional.none().orValue(n)", bindings, limit).Errors);

        Assert.True(held.TryGetInt64(out long value));
        Assert.Equal(1L, value);
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
        Assert.True(Evaluate("optional.none().orValue(n)", bindings, new CelEvalOptions { CostLimit = 3 }).GetValueOrThrow().TryGetInt64(out long seven));
        Assert.Equal(7L, seven);
    }

    // The argument builds a list of 1 000 elements when it runs. For a receiver that holds a value nothing of it is allocated; for an
    // empty one it is, and a limit that stops the loop of the argument stops it before the result is built.
    [Fact]
    public void MemoryOfTheArgument_IsAllocatedForAnEmptyReceiverOnly_AndNotPastALimit()
    {
        CelBindings bindings = new CelBindings().Add("x", IntList(1000));
        const string argument = "size(x.map(v, v + 1))";
        CelProgram<CelValue> holds = Compile("optional.of(1).orValue(" + argument + ")");
        CelProgram<CelValue> empty = Compile("optional.none().orValue(" + argument + ")");

        (long bytesHolding, CelValue resultHolding) = Allocated(holds, bindings, ulong.MaxValue);
        (long bytesEmpty, CelValue resultEmpty) = Allocated(empty, bindings, ulong.MaxValue);
        (long bytesStopped, CelValue resultStopped) = Allocated(empty, bindings, 5);

        Assert.True(resultHolding.TryGetInt64(out long one));
        Assert.True(resultEmpty.TryGetInt64(out long thousand));
        Assert.Equal((1L, 1000L), (one, thousand));
        Assert.True(bytesHolding <= 256, $"the receiver holds a value: {bytesHolding} bytes");
        Assert.True(bytesEmpty >= 16_000, $"the receiver is empty: {bytesEmpty} bytes");
        Assert.True(resultStopped.TryGetError(out CelError? error) && error.Code == CelErrorCode.CostLimitExceeded);
        Assert.True(bytesStopped <= 4096, $"the limit 5 stops the argument: {bytesStopped} bytes");
    }

    private CelProgram<CelValue> Compile(string expression) => environment.Compile<CelValue>(expression).GetProgramOrThrow();

    private CelEvalResult<CelValue> Evaluate(string expression, CelBindings bindings, CelEvalOptions? options = null) =>
        Compile(expression).Evaluate(bindings, options, CancellationToken.None);

    // The held int of an optional value.
    private static long HeldOf(CelValue optional)
    {
        Assert.True(optional.TryGetOptional(out CelOptional<CelValue> held) && held.HasValue);
        Assert.True(held.Value.TryGetInt64(out long value));
        return value;
    }

    // The smallest bytes allocated by Run in three evaluations after the first, and the result, as the cost property counts them.
    private static (long Bytes, CelValue Result) Allocated(CelProgram<CelValue> program, CelBindings bindings, ulong limit)
    {
        IReadOnlyDictionary<string, CelValue> values = bindings.Values;
        Plan plan = program.Plan;
        _ = plan.Run(plan.CreateFrame(values, limit, CancellationToken.None));
        long bytes = long.MaxValue;
        CelValue result = default;
        for (int i = 0; i < 3; i++)
        {
            Frame frame = plan.CreateFrame(values, limit, CancellationToken.None);
            long before = GC.GetAllocatedBytesForCurrentThread();
            result = plan.Run(frame);
            bytes = Math.Min(bytes, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return (bytes, result);
    }

    private static CelValue IntList(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromInt64(index);
        }

        return CelValue.FromList(CelList.Create(items));
    }
}

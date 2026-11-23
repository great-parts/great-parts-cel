using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The table of the asynchronous calls of one evaluation finds a call again by its node, its overload and its arguments. The hash of the
// arguments follows CEL equality and covers lists, maps, bytes, timestamps and durations, so that finding a call whose argument is a
// large list does not compare it with every other call of the node; and the work of the library for a call that waits, the copies of
// its arguments that a typed delegate makes, is charged to the evaluation as it is for a synchronous function.
public sealed class AsyncCallTableTests
{
    private const string TakesList = "takes(l)";

    private static long HashOf(CelValue argument) => AsyncCallTable.HashOf(1, null!, [argument], out _);

    private static bool IsPairwise(CelValue argument)
    {
        _ = AsyncCallTable.HashOf(1, null!, [argument], out bool pairwise);
        return pairwise;
    }

    private static CelValue List(params ReadOnlySpan<CelValue> items) => CelValue.FromList(CelList.Create(items));

    private static CelValue Map(params (CelValue Key, CelValue Value)[] entries) =>
        CelValue.FromMap(CelMap.Create(entries.Select(entry => KeyValuePair.Create(entry.Key, entry.Value))));

    private static CelValue Int(long value) => CelValue.FromInt64(value);

    private static CelValue ListOfInts(int count, long last) =>
        CelValue.FromList(CelList.Create([.. Enumerable.Range(1, count - 1).Select(index => Int(index)), Int(last)]));

    [Fact]
    [Trait("Requirement", "R-11")]
    public static void HashOfAList_AgreesWithCelEquality()
    {
        long ints = HashOf(List(Int(1)));

        Assert.Equal(ints, HashOf(List(CelValue.FromUInt64(1))));
        Assert.Equal(ints, HashOf(List(CelValue.FromDouble(1.0))));
        Assert.NotEqual(ints, HashOf(List(Int(2))));
        Assert.NotEqual(ints, HashOf(List(Int(1), Int(1))));
    }

    [Fact]
    [Trait("Requirement", "R-11")]
    public static void HashOfLists_OfOneThousandElements_DiffersInTheLastElement()
    {
        long first = HashOf(ListOfInts(1_000, 1_000));

        Assert.Equal(first, HashOf(ListOfInts(1_000, 1_000)));
        Assert.NotEqual(first, HashOf(ListOfInts(1_000, 1_001)));
        Assert.NotEqual(HashOf(ListOfInts(1_000, 1_001)), HashOf(ListOfInts(1_000, 1_002)));
    }

    [Fact]
    [Trait("Requirement", "R-11")]
    public static void HashOfAMap_DoesNotDependOnTheOrderOfTheEntries()
    {
        CelValue forward = Map((CelValue.FromString("a"), Int(1)), (CelValue.FromString("b"), Int(2)));
        CelValue backward = Map((CelValue.FromString("b"), Int(2)), (CelValue.FromString("a"), Int(1)));
        CelValue other = Map((CelValue.FromString("a"), Int(1)), (CelValue.FromString("b"), Int(3)));

        Assert.Equal(HashOf(forward), HashOf(backward));
        Assert.NotEqual(HashOf(forward), HashOf(other));
    }

    [Fact]
    [Trait("Requirement", "R-11")]
    public static void HashOfBytesTimestampsAndDurations_FollowsTheirContent()
    {
        byte[] content = [1, 2, 3];
        byte[] sameContent = [1, 2, 3];
        byte[] otherContent = [1, 2, 4];
        CelValue bytes = CelValue.FromBytes(content);
        CelValue sameBytes = CelValue.FromBytes(sameContent);
        CelValue otherBytes = CelValue.FromBytes(otherContent);
        CelValue moment = CelValue.FromTimestamp(new CelTimestamp(1_000, 0));
        CelValue sameMoment = CelValue.FromTimestamp(new CelTimestamp(1_000, 0));
        CelValue otherMoment = CelValue.FromTimestamp(new CelTimestamp(1_001, 0));
        CelValue span = CelValue.FromDuration(new CelDuration(5_000_000_000));
        CelValue sameSpan = CelValue.FromDuration(new CelDuration(5_000_000_000));
        CelValue otherSpan = CelValue.FromDuration(new CelDuration(6_000_000_000));

        Assert.Equal(HashOf(bytes), HashOf(sameBytes));
        Assert.NotEqual(HashOf(bytes), HashOf(otherBytes));
        Assert.Equal(HashOf(moment), HashOf(sameMoment));
        Assert.NotEqual(HashOf(moment), HashOf(otherMoment));
        Assert.Equal(HashOf(span), HashOf(sameSpan));
        Assert.NotEqual(HashOf(span), HashOf(otherSpan));
    }

    // Numbers equal by CEL equality are one double, so they hash alike at any size, and numbers that are not the same double hash apart:
    // the calls of a node with identifiers above 2^53 do not fall into one bucket.
    [Fact]
    [Trait("Requirement", "R-11")]
    public static void HashOfALargeNumber_IsTheHashOfItsDouble()
    {
        const long Large = 1L << 60;
        long ofInt = HashOf(Int(Large));

        Assert.Equal(ofInt, HashOf(CelValue.FromUInt64((ulong)Large)));
        Assert.Equal(ofInt, HashOf(CelValue.FromDouble(Large)));
        Assert.NotEqual(ofInt, HashOf(Int(Large + (1L << 13))));
        Assert.NotEqual(ofInt, HashOf(CelValue.FromDouble(double.PositiveInfinity)));
        Assert.Equal(HashOf(CelValue.FromDouble(double.PositiveInfinity)), HashOf(CelValue.FromDouble(double.PositiveInfinity)));
        Assert.False(IsPairwise(Int(Large)));
        Assert.False(IsPairwise(List(Int(Large), CelValue.FromDouble(double.NegativeInfinity))));
    }

    // A NaN hashes by a constant at any depth, so the call that holds one is found again.
    [Fact]
    [Trait("Requirement", "R-11")]
    public static void HashOfANaN_IsOneConstantAtAnyDepth()
    {
        CelValue nan = CelValue.FromDouble(double.NaN);

        Assert.Equal(HashOf(nan), HashOf(CelValue.FromDouble(double.NaN)));
        Assert.Equal(HashOf(List(nan)), HashOf(List(CelValue.FromDouble(double.NaN))));
        Assert.Equal(HashOf(Map((CelValue.FromString("a"), nan))), HashOf(Map((CelValue.FromString("a"), CelValue.FromDouble(double.NaN)))));
        Assert.NotEqual(HashOf(List(nan)), HashOf(List(CelValue.FromDouble(1.0))));
        Assert.False(IsPairwise(List(nan)));
    }

    // An object of the application has no hash of its content: the value is in the group of pairs, and its comparisons are charged.
    [Fact]
    [Trait("Requirement", "R-11")]
    public static void AnObjectOfTheApplication_IsPairwise()
    {
        CelValue application = CelValue.FromBareObject(new object());

        Assert.True(IsPairwise(application));
        Assert.True(IsPairwise(List(Int(1), application)));
        Assert.False(IsPairwise(List(Int(1), Int(2))));
    }

    // Twenty calls of one node whose arguments are lists of one thousand elements that differ in the last one: each runs once.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task CallsWithLargeListArguments_RunOnceEach()
    {
        var calls = new ConcurrentDictionary<long, int>();
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("l", CelType.ListOf(CelType.Int))
            .AddAsyncFunction("takes", async (IReadOnlyList<int> list, CancellationToken token) =>
            {
                _ = calls.AddOrUpdate(list[^1], 1, static (_, count) => count + 1);
                await Task.Yield();
                return (long)list.Count;
            })
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20].map(k, takes(l + [k])).size()").GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add("l", ListOfInts(999, 999));

        CelEvalResult<long> result = await program.EvaluateAsync(bindings, null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(20L, result.GetValueOrThrow());
        Assert.Equal(20, calls.Count);
        Assert.All(calls.Values, count => Assert.Equal(1, count));
    }

    // The environments of the cost tests: takes copies the list into an IReadOnlyList<int> (an element may not fit an Int32), takesLong
    // reads it through a view, which no element can fail, so the difference of their costs is the copy.
    private static CelEnvironment EnvironmentOf(bool asynchronous)
    {
        int started = 0;
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("l", CelType.ListOf(CelType.Int))
            .AddVariable("l2", CelType.ListOf(CelType.Int));
        return (asynchronous
            ? builder
                .AddAsyncFunction("takes", async (IReadOnlyList<int> list, CancellationToken token) =>
                {
                    await Task.Delay(Interlocked.Increment(ref started) % 3, token).ConfigureAwait(false);
                    return (long)list.Count;
                })
                .AddAsyncFunction("takesLong", async (IReadOnlyList<long> list, CancellationToken token) =>
                {
                    await Task.Delay(Interlocked.Increment(ref started) % 3, token).ConfigureAwait(false);
                    return (long)list.Count;
                })
            : builder
                .AddFunction("takes", (IReadOnlyList<int> list) => (long)list.Count)
                .AddFunction("takesLong", (IReadOnlyList<long> list) => (long)list.Count))
            .Build()
            .GetEnvironmentOrThrow();
    }

    private static CelBindings Bindings() => new CelBindings().Add("l", ListOfInts(1_000, 1_000)).Add("l2", ListOfInts(1_001, 1_001));

    private static CelProgram<long> Compile(CelEnvironment environment, string expression) =>
        environment.Compile<long>(expression).GetProgramOrThrow();

    // The synchronous evaluation in the environment with the synchronous twins of the functions.
    private static CelEvalResult<long> SynchronousResult(string expression, CelEvalOptions options) =>
        Compile(EnvironmentOf(false), expression).Evaluate(Bindings(), options, CancellationToken.None);

    // The copy of the argument for the typed delegate is charged as it is for the synchronous twin: the cost of the evaluation with the
    // typed function exceeds the cost with the function that keeps the list by the same units.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task CopyOfAnArgument_IsChargedAsForTheSynchronousTwin()
    {
        var options = new CelEvalOptions { CostLimit = 100_000 };
        ulong typedSync = SynchronousResult(TakesList, options).Cost!.Value;
        ulong viewSync = SynchronousResult("takesLong(l)", options).Cost!.Value;
        CelEvalResult<long> typed = await Compile(EnvironmentOf(true), TakesList).EvaluateAsync(Bindings(), options, CancellationToken.None).ConfigureAwait(true);
        CelEvalResult<long> view = await Compile(EnvironmentOf(true), "takesLong(l)").EvaluateAsync(Bindings(), options, CancellationToken.None).ConfigureAwait(true);

        ulong copy = typedSync - viewSync;
        Assert.True(copy >= 1_000, $"the copy of a list of 1000 elements is {copy} units");
        Assert.Equal(copy, typed.Cost!.Value - view.Cost!.Value);
        Assert.Equal(1_000L, typed.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task EvaluationWithoutALimit_CountsNoCopy()
    {
        CelEvalResult<long> result = await Compile(EnvironmentOf(true), TakesList).EvaluateAsync(Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(1_000L, result.GetValueOrThrow());
        Assert.Null(result.Cost);
    }

    // A limit below the copy of the argument ends the evaluation after the wave, at the node of the call, as the synchronous call ends in it.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task LimitBelowTheCopyOfAnArgument_IsExceededAtTheCall()
    {
        // The first pass charges the check of the argument, about 1 005 units, and the copy is 1 001 more.
        var options = new CelEvalOptions { CostLimit = 1_500 };
        CelEvalResult<long> synchronous = SynchronousResult(TakesList, options);

        CelEvalResult<long> asynchronous = await Compile(EnvironmentOf(true), TakesList).EvaluateAsync(Bindings(), options, CancellationToken.None).ConfigureAwait(true);

        CelError expected = Assert.Single(synchronous.Errors);
        CelError actual = Assert.Single(asynchronous.Errors);
        Assert.Equal(CelErrorCode.CostLimitExceeded, actual.Code);
        Assert.Equal((expected.Code, expected.Location.Offset), (actual.Code, actual.Location.Offset));
        Assert.InRange(asynchronous.Cost!.Value, 1_000UL, 1_500UL);
    }

    // The units of the calls of a wave are added in the order in which the calls started, so the call that passes the limit, and the cost
    // reported, are the same whichever call completes first.
    [Fact]
    [Trait("Requirement", "R-11")]
    public async Task LimitInTheMiddleOfAWave_IsExceededAtTheSameCallWhateverTheOrderOfCompletion()
    {
        // The first pass charges the check of both arguments, about 2 010 units; the copies are 1 001 and 1 002.
        var options = new CelEvalOptions { CostLimit = 3_600 };
        CelProgram<long> program = Compile(EnvironmentOf(true), "takes(l) + takes(l2)");
        CelEvalResult<long> first = await program.EvaluateAsync(Bindings(), options, CancellationToken.None).ConfigureAwait(true);

        CelError error = Assert.Single(first.Errors);
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
        Assert.InRange(first.Cost!.Value, 3_000UL, 3_599UL);
        for (int repeat = 0; repeat < 20; repeat++)
        {
            CelEvalResult<long> result = await program.EvaluateAsync(Bindings(), options, CancellationToken.None).ConfigureAwait(true);

            Assert.Equal(first.Cost, result.Cost);
            Assert.Equal(error.Location.Offset, Assert.Single(result.Errors).Location.Offset);
        }
    }
}

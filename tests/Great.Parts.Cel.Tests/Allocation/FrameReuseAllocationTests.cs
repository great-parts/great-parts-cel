using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Kinds;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// The frame of an evaluation is rented from the thread, which keeps it for the next evaluation: a thread that evaluates again and
// again allocates no frame, 0 bytes in all for an expression that creates no value, with a structure as the input and with an
// evaluation inside an evaluation as well. The measure is the smallest bytes of three calls after two that are not counted, as in the
// other allocation tests; the rest of the tests show that a rented frame carries nothing from one evaluation to the next.
public sealed class FrameReuseAllocationTests
{
    private const int Counted = 3;

    // How many evaluations one inside another the field `deep` makes: more than the frames that a thread keeps.
    private const int Levels = 6;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelEnvironment InnerEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("y")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelProgram<long> Inner = InnerEnvironment.Compile<long>("y * 2").GetProgramOrThrow();

    private static readonly CelBindings InnerBindings = new CelBindings().Add("y", 21L);

    // The getters of the fields evaluate programs on the same thread, in the middle of the evaluation that reads the field.
    private static readonly CelObjectType RowType = new CelObjectTypeBuilder<Row>("shop.Row")
        .AddField("doubled", CelType.Int, static _ => CelValue.FromInt64(Inner.Evaluate(InnerBindings, cancellationToken: CancellationToken.None).GetValueOrThrow()))
        .AddField("deep", CelType.Int, static _ => Deepen())
        .Build();

    private static readonly CelEnvironment RowEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CelTypeProvider.Create("shop", RowType))
        .AddVariable("row", RowType.Type)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings RowBindings = new CelBindings().Add("row", CelValue.FromObject(RowType, new Row()));

    private static readonly CelProgram<long> DeepProgram = RowEnvironment.Compile<long>("row.deep + 1").GetProgramOrThrow();

    private static readonly CelEnvironment StructEnvironment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<StructKind>());

    [ThreadStatic]
    private static int t_depth;

    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Evaluate_Repeated_AllocatesNoFrame()
    {
        CelProgram<bool> program = Environment.Compile<bool>("x + 1 > 0 && x < 100").GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add("x", 41L);

        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = program.Plan.CreateFrame(bindings.Values, null, CancellationToken.None);
        long frame = GC.GetAllocatedBytesForCurrentThread() - before;

        long smallest = Smallest(() => program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());

        Assert.True(frame > 0, "a frame that no thread keeps allocates");
        Assert.Equal(0L, smallest);
    }

    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Evaluate_WithACostLimitAndACancelableToken_AllocatesNoFrame()
    {
        CelProgram<bool> program = Environment.Compile<bool>("[1, 2, 3].all(i, i > 0) && x < 100").GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add("x", 41L);
        var options = new CelEvalOptions { CostLimit = 1000 };
        using var source = new CancellationTokenSource();

        long smallest = Smallest(() => program.Evaluate(bindings, options, source.Token).GetValueOrThrow());

        Assert.Equal(0L, smallest);
    }

    [Fact]
    [Trait("Requirement", "P-3")]
    public static void EvaluateInput_OnAStructure_Repeated_AllocatesNoFrameAndNoBox()
    {
        CelProgram<bool> program = StructEnvironment.Compile<bool>("id > 0 && name != ''").GetProgramOrThrow();
        var structure = new StructKind { Id = 1, Name = "n" };
        var options = new CelEvalOptions { CostLimit = 1000 };

        long plain = Smallest(() => program.EvaluateInput(structure, cancellationToken: CancellationToken.None).GetValueOrThrow());
        long limited = Smallest(() => program.EvaluateInput(structure, options: options, cancellationToken: CancellationToken.None).GetValueOrThrow());

        Assert.Equal(0L, plain);
        Assert.Equal(0L, limited);
    }

    // The getter of the field evaluates another program while the outer evaluation holds its frame: the thread rents a second frame, and
    // keeps both, so the next call allocates none.
    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Evaluate_InsideAnEvaluation_AllocatesNoFrameAfterTheFirstCall()
    {
        CelProgram<bool> outer = RowEnvironment.Compile<bool>("row.doubled == 42").GetProgramOrThrow();

        long smallest = Smallest(() => outer.Evaluate(RowBindings, cancellationToken: CancellationToken.None).GetValueOrThrow());

        Assert.Equal(0L, smallest);
    }

    // Evaluations nested deeper than the thread keeps frames make the others and give the values of the same program.
    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Evaluate_NestedDeeperThanTheFramesKept_GivesTheValues()
    {
        for (int i = 0; i < Counted; i++)
        {
            Assert.Equal(Levels + 1L, DeepProgram.Evaluate(RowBindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        }
    }

    // A canceled evaluation gives its frame back: the evaluation after it takes that frame and allocates none.
    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Evaluate_AfterACanceledEvaluation_TakesTheFrameThatItGaveBack()
    {
        CelProgram<bool> quick = Environment.Compile<bool>("x < 100").GetProgramOrThrow();
        CelProgram<long> canceled = Environment.Compile<long>("[1, 2, 3].map(i, i * x).size()").GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add("x", 41L);
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.True(quick.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.True(quick.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());

        long smallest = long.MaxValue;
        for (int i = 0; i < Counted; i++)
        {
            _ = Assert.ThrowsAny<OperationCanceledException>(() => canceled.Evaluate(bindings, cancellationToken: source.Token));

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool result = quick.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.True(result);
        }

        Assert.Equal(0L, smallest);
    }

    // The frame that a program gives back is cleared: a program of the same variable, evaluated next on the thread, does not see the
    // value of the last one, the variable that it read, or the slots of its comprehensions.
    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Evaluate_AfterAnotherProgram_SeesNothingOfIt()
    {
        CelProgram<long> sum = Environment.Compile<long>("[1, 2, 3].map(i, i * x).size() + [4, 5].filter(j, j > x).size()").GetProgramOrThrow();
        CelProgram<long> read = Environment.Compile<long>("x").GetProgramOrThrow();

        Assert.Equal(5L, sum.Evaluate(new CelBindings().Add("x", 2L), cancellationToken: CancellationToken.None).GetValueOrThrow());
        CelEvalResult<long> unbound = read.Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
        Assert.Equal(5L, sum.Evaluate(new CelBindings().Add("x", 2L), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(9L, read.Evaluate(new CelBindings().Add("x", 9L), cancellationToken: CancellationToken.None).GetValueOrThrow());

        Assert.False(unbound.IsSuccess);
        Assert.Equal(CelErrorCode.Unbound, Assert.Single(unbound.Errors).Code);
    }

    // The value of an evaluation holds nothing of its frame: the evaluations after it, on the same frame, do not change it.
    [Fact]
    [Trait("Requirement", "P-3")]
    public static void Result_AfterLaterEvaluationsOnTheSameFrame_IsUnchanged()
    {
        CelProgram<CelValue> pair = Environment.Compile<CelValue>("[x, x + 1]").GetProgramOrThrow();

        CelValue first = pair.Evaluate(new CelBindings().Add("x", 7L), cancellationToken: CancellationToken.None).GetValueOrThrow();
        _ = pair.Evaluate(new CelBindings().Add("x", 100L), cancellationToken: CancellationToken.None).GetValueOrThrow();
        _ = pair.Evaluate(new CelBindings().Add("x", 200L), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.True(first.TryGetList(out CelList? list));
        Assert.Equal(2, list.Count);
        Assert.Equal(CelValue.FromInt64(7), list[0]);
        Assert.Equal(CelValue.FromInt64(8), list[1]);
    }

    // The field `deep` evaluates the program that reads it, until Levels evaluations are nested.
    private static CelValue Deepen()
    {
        if (t_depth == Levels)
        {
            return CelValue.FromInt64(0);
        }

        t_depth++;
        try
        {
            return CelValue.FromInt64(DeepProgram.Evaluate(RowBindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        }
        finally
        {
            t_depth--;
        }
    }

    private static long Smallest(Func<bool> evaluate)
    {
        Assert.True(evaluate());
        Assert.True(evaluate());

        long smallest = long.MaxValue;
        for (int i = 0; i < Counted; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool result = evaluate();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(result);
            smallest = Math.Min(smallest, allocated);
        }

        return smallest;
    }

    private sealed class Row;
}

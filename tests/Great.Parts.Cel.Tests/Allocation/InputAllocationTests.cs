using System;
using System.Threading;
using Great.Parts.Cel.Generator.Samples.Orders;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// The evaluation on the input of an environment allocates the frame of the evaluation and nothing for the input, and bindings that are
// cleared and filled again in a loop allocate nothing for the names. The measure is the smallest bytes of three calls after two that are
// not counted, as in the other allocation tests: the few kilobytes that the runtime allocates on the test thread while it moves methods
// between tiers are not the program's.
public sealed class InputAllocationTests
{
    // The frame of one evaluation with its slots and argument stack and the result: they do not grow with the data (EvaluationAllocationTests).
    private const long FrameBytes = 512;

    private const int Counted = 3;

    private static readonly CelEnvironment Environment = CelEnvironment.Create(builder => builder.AddTypes(ShopContext.Default).UseInput<Order>());

    [Fact]
    [Trait("Requirement", "P-4")]
    public void EvaluateInput_OnAClassInput_AllocatesTheFrameOnly()
    {
        CelProgram<bool> program = Environment.Compile<bool>("total > 1000.0 && status == 'approved'").GetProgramOrThrow();
        var order = new Order { Id = "ORD-1", Status = "approved", Total = 1500.0 };

        Assert.True(program.EvaluateInput(order, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.True(program.EvaluateInput(order, cancellationToken: CancellationToken.None).GetValueOrThrow());

        long smallest = long.MaxValue;
        for (int i = 0; i < Counted; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool result = program.EvaluateInput(order, cancellationToken: CancellationToken.None).GetValueOrThrow();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(result);
            smallest = Math.Min(smallest, allocated);
        }

        Assert.True(smallest <= FrameBytes, $"{smallest} bytes allocated, at most {FrameBytes} expected");
    }

    [Fact]
    [Trait("Requirement", "R-4")]
    public void Clear_InALoop_AllocatesNothingForTheNames()
    {
        var bindings = new CelBindings();
        Fill(bindings, 0);
        Fill(bindings, 1);

        long smallest = long.MaxValue;
        for (int i = 0; i < Counted; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int item = 0; item < 100; item++)
            {
                Fill(bindings, item);
            }

            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0L, smallest);
    }

    private static void Fill(CelBindings bindings, long item)
    {
        bindings.Clear();
        bindings.Add("a", item).Add("b", item + 1).Add("c", true);
    }
}

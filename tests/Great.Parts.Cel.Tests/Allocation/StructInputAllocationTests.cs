using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Great.Parts.Cel.Generator.Samples.Kinds;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// The evaluation on an input that is a structure allocates the frame and nothing else for the structure: it is read by reference from
// the frame, through the typed description that the generator creates, not boxed. The measure is the smallest bytes of three calls
// after two that are not counted, as in the other allocation tests.
public sealed class StructInputAllocationTests
{
    private const int Counted = 3;

    private const string Text = "id > 0 && name != ''";

    private static readonly CelEnvironment StructEnvironment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<StructKind>());

    private static readonly CelEnvironment ClassEnvironment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<ClassKind>());

    // The two inputs have the same fields and the expression is the same, so the frame of the class input is the base of the measure. The
    // frame of the structure input is larger by the structure and the reference to the typed description; a box of the structure would
    // be an object more, with its header, so the call would allocate more than that.
    [Fact]
    [Trait("Requirement", "P-3")]
    public void EvaluateInput_OnAStructure_AllocatesTheFrameWithTheStructureAndNothingMore()
    {
        CelProgram<bool> structProgram = StructEnvironment.Compile<bool>(Text).GetProgramOrThrow();
        CelProgram<bool> classProgram = ClassEnvironment.Compile<bool>(Text).GetProgramOrThrow();
        var structure = new StructKind { Id = 1, Name = "n" };
        var instance = new ClassKind { Id = 1, Name = "n" };

        long structBytes = Smallest(() => structProgram.EvaluateInput(structure, cancellationToken: CancellationToken.None).GetValueOrThrow());
        long classBytes = Smallest(() => classProgram.EvaluateInput(instance, cancellationToken: CancellationToken.None).GetValueOrThrow());

        long expected = classBytes + Unsafe.SizeOf<StructKind>() + IntPtr.Size;
        Assert.True(structBytes <= expected, $"{structBytes} bytes allocated for the structure, at most {expected} expected ({classBytes} for the class)");
    }

    // The same with a cost limit, which makes the evaluation ask the description for the cost of each read before the read.
    [Fact]
    [Trait("Requirement", "P-3")]
    public void EvaluateInput_OnAStructureWithACostLimit_AllocatesTheFrameWithTheStructureAndNothingMore()
    {
        CelProgram<bool> structProgram = StructEnvironment.Compile<bool>(Text).GetProgramOrThrow();
        CelProgram<bool> classProgram = ClassEnvironment.Compile<bool>(Text).GetProgramOrThrow();
        var structure = new StructKind { Id = 1, Name = "n" };
        var instance = new ClassKind { Id = 1, Name = "n" };
        var options = new CelEvalOptions { CostLimit = 1000 };

        long structBytes = Smallest(() => structProgram.EvaluateInput(structure, options: options, cancellationToken: CancellationToken.None).GetValueOrThrow());
        long classBytes = Smallest(() => classProgram.EvaluateInput(instance, options: options, cancellationToken: CancellationToken.None).GetValueOrThrow());

        long expected = classBytes + Unsafe.SizeOf<StructKind>() + IntPtr.Size;
        Assert.True(structBytes <= expected, $"{structBytes} bytes allocated for the structure, at most {expected} expected ({classBytes} for the class)");
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
}

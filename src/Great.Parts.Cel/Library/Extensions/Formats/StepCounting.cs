using System.Runtime.CompilerServices;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The count of the steps of a scanner of the set formats. Every scanner is generic in the counter, so that a check of the linear
// time can count the units that a scanner reads, while the scanner that a function runs uses NoSteps, whose Step is empty and
// disappears when the scanner is compiled for it.
internal interface IStepCounter
{
    void Step();
}

// The counter of a function: counts nothing.
internal struct NoSteps : IStepCounter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Step()
    {
    }
}

// The counter of a test: one step for each UTF-16 unit that a scanner reads.
internal struct StepCount : IStepCounter
{
    // The steps counted so far.
    internal long Steps { get; private set; }

    public void Step() => Steps++;
}

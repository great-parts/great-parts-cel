using System;

namespace Great.Parts.Cel.Library.Regex;

// The program of a pattern: the instructions, the ranges of its classes, the size m and the index of the first instruction. It never
// changes after the build, so any number of threads run it at once.
internal sealed class CompiledPattern(Instruction[] instructions, int[] ranges, int size, int start)
{
    // The instructions; the last one is Match.
    internal ReadOnlySpan<Instruction> Instructions => instructions;

    // The pairs lo, hi of the classes, each class a run of pairs that an instruction names.
    internal ReadOnlySpan<int> Ranges => ranges;

    // The size m, as the parse counted it: the instructions plus the ranges.
    internal int Size { get; } = size;

    // The index of the instruction that a run starts at.
    internal int Start { get; } = start;
}

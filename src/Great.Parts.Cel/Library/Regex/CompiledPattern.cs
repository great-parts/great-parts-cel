using System;

namespace Great.Parts.Cel.Library.Regex;

// The program of a pattern: the instructions, the ranges of its classes, the size m, the index of the first instruction, the literal
// prefix that every match begins with and whether every match begins at the start of the text. It never changes after the build, so any
// number of threads run it at once.
internal sealed class CompiledPattern(Instruction[] instructions, int[] ranges, int size, int start, char[]? prefix, bool anchored)
{
    private Dfa? automaton;

    // The instructions; the last one is Match.
    internal ReadOnlySpan<Instruction> Instructions => instructions;

    // The pairs lo, hi of the classes, each class a run of pairs that an instruction names.
    internal ReadOnlySpan<int> Ranges => ranges;

    // The size m, as the parse counted it: the instructions plus the ranges.
    internal int Size { get; } = size;

    // The index of the instruction that a run starts at.
    internal int Start { get; } = start;

    // The literal prefix that every match begins with, of at least two code units (LiteralPrefix), or null when there is none. It is
    // at most the length that SubstringSearch keeps on the stack, so its weight is bounded by a constant.
    internal char[]? Prefix { get; } = prefix;

    // Whether every path from the first instruction passes the beginning of the text before it reads a code point, so that no match
    // begins after position 0.
    internal bool Anchored { get; } = anchored;

    // The deterministic automaton of the program, or null when it has none: a pattern from data has none, and neither has a pattern
    // that DfaBuilder refuses. It is set once, before the program is shared, and never changes after that.
    internal Dfa? Automaton
    {
        get => automaton;
        set
        {
            automaton = value;
            AutomatonTried = true;
        }
    }

    // Whether the build of the automaton was tried: a pattern from data never was, so a literal of the same text may build one.
    internal bool AutomatonTried { get; private set; }

    // The same program with another automaton: the instructions, the ranges, the prefix and the flag are the same arrays and values,
    // which never change, so the copy and the original run at once on any threads.
    internal CompiledPattern WithAutomaton(Dfa? automaton) => new(instructions, ranges, Size, Start, Prefix, Anchored) { Automaton = automaton };
}

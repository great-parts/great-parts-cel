using System.Threading;

namespace Great.Parts.Cel.Library.Regex;

// What the cache keeps for a pattern: its program, or the error that the parse or the build gave — a pattern that fails is cached
// too. The size of the program is kept so that a hit knows what to charge without a parse.
internal sealed class PatternEntry
{
    private int referenced;

    // The weight of a program is m + ⌈D/16⌉, D the bytes of the tables of its automaton, 0 when it has none: the unit of the weight is
    // 16 bytes, as an instruction of the program is.
    internal PatternEntry(CompiledPattern program)
    {
        Program = program;
        Size = program.Size;
        Weight = program.Size + (program.Automaton is { } automaton ? (automaton.TableBytes + 15) / 16 : 0);
    }

    internal PatternEntry(RegexError error)
    {
        Error = error;
        Size = 0;
        Weight = 1;
    }

    // The program, or null when the pattern failed.
    internal CompiledPattern? Program { get; }

    // The error, or null when the pattern compiled.
    internal RegexError? Error { get; }

    // The size m of the program, 0 for an error.
    internal int Size { get; }

    // What the entry counts against the capacity of the cache, in units of the size of a program: m plus the tables of the automaton
    // in units of 16 bytes, and 1 for an error.
    internal int Weight { get; }

    // A hit sets the flag with a plain write: a flag lost to a race only makes the entry the next to be removed.
    internal void MarkReferenced() => Volatile.Write(ref referenced, 1);

    // Clears the flag and tells whether it was set.
    internal bool TakeReferenced() => Interlocked.Exchange(ref referenced, 0) != 0;
}

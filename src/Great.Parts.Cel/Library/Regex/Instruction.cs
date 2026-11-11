namespace Great.Parts.Cel.Library.Regex;

// The kinds of the instructions of a program. A path goes from an instruction to its successor directly: there is no jump and no
// no-operation.
internal enum InstructionKind
{
    // One code point; A is the code point.
    Char = 0,

    // A code point and the rest of its orbit under simple case folding; A is a member of the orbit.
    CharFold = 1,

    // A code point in a list of ranges; A is the index of the first pair in the array of ranges, B the number of pairs.
    Class = 2,

    // A code point in a table of StaticTables; A is the table, B is 1 for the complement of the table.
    StaticClass = 3,

    // Any code point.
    Any = 4,

    // Any code point but U+000A.
    AnyNotNewline = 5,

    // Either of two successors: Next is the first, A is the second.
    Alt = 6,

    // A condition of zero width; A is the bit of the condition (EmptyCondition).
    Empty = 7,

    // The end of the program.
    Match = 8,
}

// The conditions of zero width: bits of the mask that describes a position of the text.
internal static class EmptyCondition
{
    internal const int BeginLine = 1;
    internal const int EndLine = 2;
    internal const int BeginText = 4;
    internal const int EndText = 8;
    internal const int WordBoundary = 16;
    internal const int NoWordBoundary = 32;
}

// An instruction of a program: four ints, 16 bytes. Next is the successor of every kind but Alt (see InstructionKind) and Match.
internal struct Instruction
{
    internal InstructionKind Kind;
    internal int Next;
    internal int A;
    internal int B;
}

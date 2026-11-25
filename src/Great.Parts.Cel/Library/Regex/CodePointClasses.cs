using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Library.Unicode;

namespace Great.Parts.Cel.Library.Regex;

// The alphabet of the automaton of a program: the code points split into classes, two code points being in one class when every
// instruction of the program accepts both or neither. The bounds of the instructions cut the code points into intervals; an interval
// has the set of the instructions that accept it, and the intervals with one set — next to each other or not — are one class, so
// that a table such as \p{L} with a space and a hyphen is four classes and not hundreds of intervals. A class is a number below
// Count; a code point below 128 is looked up in a table of bytes, any other one by a binary search over the starts of the merged
// intervals. The classes never change after the build.
internal sealed class CodePointClasses
{
    // The largest code point.
    private const int MaxCodePoint = 0x10FFFF;

    // The code point that stands for a lone surrogate.
    private const int ReplacementCharacter = 0xFFFD;

    private const int AsciiLimit = 128;

    // The most bounds that the instructions of a program may give before the build of the classes gives up: a program that cuts the
    // code points into more intervals than this has no automaton.
    private const int MaxBounds = 1 << 17;

    // A class number is packed above the start of its interval, which takes 21 bits.
    private const int ClassShift = 24;
    private const int StartMask = (1 << ClassShift) - 1;

    // The ints at the head of the array that hold the table of the code points below 128, a byte each.
    private const int AsciiInts = AsciiLimit / sizeof(int);

    // One array holds it all: the table of the code points below 128 (AsciiInts ints read as bytes), then the merged intervals, each
    // as the start with the class number packed above it.
    private readonly int[] data;

    private CodePointClasses(int[] data, int count)
    {
        this.data = data;
        Count = count;
    }

    // The number of classes, at most the limit that the build was given.
    internal int Count { get; }

    // The number of intervals of consecutive code points that the classes are made of; the size of the search of a code point above 127.
    internal int IntervalCount => data.Length - AsciiInts;

    // What the classes hold, in bytes: the table of the code points below 128, the starts of the intervals and their class numbers.
    internal int TableBytes => AsciiLimit + (IntervalCount * (sizeof(int) + sizeof(byte)));

    // The class of a code point.
    internal int Of(int codePoint) => (uint)codePoint < AsciiLimit ? AsciiBytes(data)[codePoint] : Lookup(codePoint);

    // The class of a code unit that is not a surrogate.
    internal int OfUnit(char unit) => unit < AsciiLimit ? AsciiBytes(data)[unit] : Lookup(unit);

    private static Span<byte> AsciiBytes(int[] data) => MemoryMarshal.AsBytes(data.AsSpan(0, AsciiInts));

    // Adds the code units of a class to the list, counted in count, when the class holds only units of the Basic Multilingual Plane
    // that are not surrogates, not U+FFFD — a lone surrogate of a text is read as U+FFFD, so a search for the unit would pass it —
    // and that fit in the room left; false otherwise.
    internal bool TryAddUnits(int cls, Span<char> units, ref int count)
    {
        int intervals = IntervalCount;
        for (int i = 0; i < intervals; i++)
        {
            int entry = data[AsciiInts + i];
            if ((entry >> ClassShift) != cls)
            {
                continue;
            }

            int first = entry & StartMask;
            int last = i + 1 < intervals ? (data[AsciiInts + i + 1] & StartMask) - 1 : MaxCodePoint;
            if (last > 0xFFFF
                || (last >= 0xD800 && first <= 0xDFFF)
                || (first <= ReplacementCharacter && last >= ReplacementCharacter)
                || last - first + 1 > units.Length - count)
            {
                return false;
            }

            for (int point = first; point <= last; point++)
            {
                units[count++] = (char)point;
            }
        }

        return true;
    }

    // The classes of a program, or null with the reason: more than maxClasses of them, or more work than the builder may do. The masks
    // are one ulong for each instruction, cleared by the caller; bit c of the mask of an instruction is set when the instruction
    // accepts the code points of class c. The scratch holds (maxClasses + 1) signatures of one bit for each instruction. The work —
    // the evaluations of an instruction on an interval and the bounds that are sorted — is added to work.
    internal static CodePointClasses? TryCreate(
        ReadOnlySpan<Instruction> code,
        ReadOnlySpan<int> ranges,
        int maxClasses,
        long maxWork,
        ref long work,
        Span<ulong> masks,
        Span<ulong> scratch,
        out DfaRefusal refusal)
    {
        refusal = DfaRefusal.None;
        long capacity = 1;
        int readers = 0;
        foreach (ref readonly Instruction instruction in code)
        {
            if (instruction.Kind <= InstructionKind.AnyNotNewline)
            {
                readers++;
                capacity += BoundCount(in instruction);
            }
        }

        if (capacity > MaxBounds || work + capacity > maxWork)
        {
            refusal = DfaRefusal.Work;
            return null;
        }

        work += capacity;
        int[] boundsArray = ArrayPool<int>.Shared.Rent((int)capacity);
        try
        {
            int intervals = CollectBounds(code, ranges, boundsArray.AsSpan(0, (int)capacity));
            if ((long)intervals * readers > maxWork - work)
            {
                refusal = DfaRefusal.Work;
                return null;
            }

            work += (long)intervals * readers;
            return Merge(boundsArray.AsSpan(0, intervals), code, ranges, maxClasses, masks, scratch, out refusal);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(boundsArray);
        }
    }

    // Gives each interval its class by the set of instructions that accept its first code point; the intervals of one set are one
    // class, and adjacent ones of one class become one interval. The bounds are overwritten with the starts of the merged intervals,
    // each with its class number above the start.
    private static CodePointClasses? Merge(
        Span<int> bounds,
        ReadOnlySpan<Instruction> code,
        ReadOnlySpan<int> ranges,
        int maxClasses,
        Span<ulong> masks,
        Span<ulong> scratch,
        out DfaRefusal refusal)
    {
        int words = (code.Length + 63) >> 6;
        if (!AssignClasses(bounds, code, ranges, maxClasses, scratch, words, out int merged, out int classCount))
        {
            refusal = DfaRefusal.Classes;
            return null;
        }

        refusal = DfaRefusal.None;
        for (int cls = 0; cls < classCount; cls++)
        {
            AddToMasks(scratch.Slice(cls * words, words), cls, masks);
        }

        var data = new int[AsciiInts + merged];
        bounds[..merged].CopyTo(data.AsSpan(AsciiInts));
        FillAscii(data, merged);
        return new CodePointClasses(data, classCount);
    }

    // Finds the class of each interval; false when there are more than maxClasses of them. The signatures of the classes are kept in
    // the scratch, one after the other, with the signature of the interval in hand after them.
    private static bool AssignClasses(
        Span<int> bounds,
        ReadOnlySpan<Instruction> code,
        ReadOnlySpan<int> ranges,
        int maxClasses,
        Span<ulong> scratch,
        int words,
        out int merged,
        out int classCount)
    {
        classCount = 0;
        merged = 0;
        int previousClass = -1;
        for (int interval = 0; interval < bounds.Length; interval++)
        {
            int first = bounds[interval];
            Span<ulong> signature = scratch.Slice(classCount * words, words);
            Signature(code, ranges, first, signature);
            int cls = IndexOf(scratch, words, classCount, signature, previousClass);
            if (cls < 0)
            {
                if (classCount == maxClasses)
                {
                    return false;
                }

                cls = classCount++;
            }

            if (cls != previousClass)
            {
                bounds[merged++] = first | (cls << ClassShift);
                previousClass = cls;
            }
        }

        return true;
    }

    // Sets bit cls of the mask of every instruction that the signature holds.
    private static void AddToMasks(ReadOnlySpan<ulong> signature, int cls, Span<ulong> masks)
    {
        for (int word = 0; word < signature.Length; word++)
        {
            for (ulong bits = signature[word]; bits != 0; bits &= bits - 1)
            {
                masks[(word << 6) + BitOperations.TrailingZeroCount(bits)] |= 1UL << cls;
            }
        }
    }

    // Fills the table of the code points below 128 from the intervals that follow it in the array.
    private static void FillAscii(int[] data, int intervals)
    {
        Span<byte> table = AsciiBytes(data);
        for (int i = 0; i < intervals; i++)
        {
            int entry = data[AsciiInts + i];
            int start = entry & StartMask;
            if (start >= AsciiLimit)
            {
                break;
            }

            int last = i + 1 < intervals ? Math.Min(data[AsciiInts + i + 1] & StartMask, AsciiLimit) : AsciiLimit;
            table.Slice(start, last - start).Fill((byte)(entry >> ClassShift));
        }
    }

    // The instructions that accept a code point, as a set of bits.
    private static void Signature(ReadOnlySpan<Instruction> code, ReadOnlySpan<int> ranges, int codePoint, Span<ulong> signature)
    {
        signature.Clear();
        for (int i = 0; i < code.Length; i++)
        {
            if (code[i].Kind <= InstructionKind.AnyNotNewline && Matcher.Accepts(in code[i], ranges, codePoint))
            {
                signature[i >> 6] |= 1UL << (i & 63);
            }
        }
    }

    // The number of the class with a signature among the first count classes of the scratch, or -1; the class of the interval before
    // is tried first, since the next interval is most often of the same class.
    private static int IndexOf(ReadOnlySpan<ulong> scratch, int words, int count, ReadOnlySpan<ulong> signature, int previousClass)
    {
        if (previousClass >= 0 && scratch.Slice(previousClass * words, words).SequenceEqual(signature))
        {
            return previousClass;
        }

        for (int cls = 0; cls < count; cls++)
        {
            if (scratch.Slice(cls * words, words).SequenceEqual(signature))
            {
                return cls;
            }
        }

        return -1;
    }

    // The most bounds that an instruction adds: the first code point of each range and the one after its last.
    private static int BoundCount(in Instruction instruction)
    {
        return instruction.Kind switch
        {
            InstructionKind.Char or InstructionKind.AnyNotNewline => 2,
            InstructionKind.CharFold => 8,
            InstructionKind.Class => instruction.B * 2,
            InstructionKind.StaticClass => StaticTables.Ranges(instruction.A).Length,
            _ => 0,
        };
    }

    // Fills the span with the code points at which the answer of an instruction may change, sorted and without repeats, from 0; returns
    // their number.
    private static int CollectBounds(ReadOnlySpan<Instruction> code, ReadOnlySpan<int> ranges, Span<int> bounds)
    {
        int count = 0;
        bounds[count++] = 0;
        foreach (ref readonly Instruction instruction in code)
        {
            AddBounds(bounds, ref count, in instruction, ranges);
        }

        Span<int> filled = bounds[..count];
        filled.Sort();
        int distinct = 1;
        for (int i = 1; i < filled.Length; i++)
        {
            if (filled[i] != filled[distinct - 1])
            {
                filled[distinct++] = filled[i];
            }
        }

        return distinct;
    }

    private static void AddBounds(Span<int> bounds, ref int count, in Instruction instruction, ReadOnlySpan<int> ranges)
    {
        switch (instruction.Kind)
        {
            case InstructionKind.Char:
                AddRange(bounds, ref count, instruction.A, instruction.A);
                break;
            case InstructionKind.CharFold:
                int member = instruction.A;
                do
                {
                    AddRange(bounds, ref count, member, member);
                    member = UnicodeTables.Next(member);
                }
                while (member != instruction.A);
                break;
            case InstructionKind.Class:
                AddPairs(bounds, ref count, ranges.Slice(instruction.A << 1, instruction.B << 1));
                break;
            case InstructionKind.StaticClass:
                AddPairs(bounds, ref count, StaticTables.Ranges(instruction.A));
                break;
            case InstructionKind.AnyNotNewline:
                AddRange(bounds, ref count, '\n', '\n');
                break;
        }
    }

    private static void AddPairs(Span<int> bounds, ref int count, ReadOnlySpan<int> pairs)
    {
        for (int i = 0; i < pairs.Length; i += 2)
        {
            AddRange(bounds, ref count, pairs[i], pairs[i + 1]);
        }
    }

    private static void AddRange(Span<int> bounds, ref int count, int low, int high)
    {
        bounds[count++] = low;
        if (high < MaxCodePoint)
        {
            bounds[count++] = high + 1;
        }
    }

    // The class of the interval that holds a code point: the last start that is not above it.
    private int Lookup(int codePoint)
    {
        int low = 0;
        int high = data.Length - AsciiInts - 1;
        while (low < high)
        {
            int middle = (low + high + 1) >> 1;
            if ((data[AsciiInts + middle] & StartMask) <= codePoint)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return data[AsciiInts + low] >> ClassShift;
    }
}

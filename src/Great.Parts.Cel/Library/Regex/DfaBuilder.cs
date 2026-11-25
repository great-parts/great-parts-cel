using System;
using System.Buffers;
using System.Numerics;

namespace Great.Parts.Cel.Library.Regex;

// Why a program has no automaton: the engine of sets of states runs it. None of these is an error of the pattern.
internal enum DfaRefusal
{
    None,

    // A condition of zero width other than the beginning and the end of the text.
    Conditions,

    // More classes of code points than the limit.
    Classes,

    // More states than the limit.
    States,

    // More work than the limit: the evaluations of an instruction that the classes and the transitions took.
    Work,
}

// The subset construction of the automaton of a program (Dfa). The automaton is built only for a program whose conditions of zero
// width are \A and \z (^ and $ without the flag m): those that look at the code point before or after the position — the line
// conditions and the word boundaries — have no automaton, and the engine of sets of states runs them. A program with more classes of
// code points than the limit, whose subset construction passes the limit of states, or which takes more than the limit of work, has
// none either.
//
// A state is the set of instructions that reading a code point entered, the forks left out: the instructions that read a code point
// and the conditions \z that the closure could not pass, because the text may end there. The closure that follows a code point holds
// no condition, since a code point has just been read and another follows or the text has ended — the answer for the end is kept
// apart. The closure of the first instruction is in each state, as the engine adds it at each position.
//
// The build takes its scratch from the shared pool and allocates only what it returns: the automaton, its tables and its classes.
internal ref struct DfaBuilder
{
    // The states, the classes and the work that an automaton may take: the limits of the library.
    internal const int MaxStates = 256;
    internal const int MaxClasses = 64;
    internal const long MaxWork = 200_000;

    // The units that the state of no live thread may be left by for the search to go on by a search for them: at most this many when
    // they are of the Basic Multilingual Plane, or any number of them when all are ASCII.
    internal const int MaxSkipUnits = 3;
    private const int AsciiUnits = 128;

    private const int DeadRow = 0;
    private const int MatchedRow = 1;

    // Each row of the list of rows is three ints: the offset of its key in the list of keys, the hash of the key, and the answer for
    // the end of the text.
    private const int RowFields = 3;

    private readonly ReadOnlySpan<Instruction> code;
    private readonly int start;
    private readonly int maxRows;
    private readonly long maxWork;
    private readonly int slotMask;
    private readonly int[] arena;
    private readonly int[] slots;
    private readonly Span<int> members;
    private readonly Span<int> stack;
    private readonly bool hasBegin;
    private SparseSet visited;
    private PooledList<int> keys;
    private PooledList<int> rows;
    private PooledList<ushort> table;
    private int rowCount;
    private int stride;
    private int initialRow;
    private int memberCount;
    private int baseOffset;
    private bool baseMatched;
    private bool matched;
    private long work;
    private DfaRefusal refusal;

    private DfaBuilder(ReadOnlySpan<Instruction> code, int start, bool hasBegin, int maxStates, long maxWork)
    {
        this.code = code;
        this.start = start;
        this.hasBegin = hasBegin;
        this.maxWork = maxWork;
        maxRows = maxStates + 2;

        // The sparse set of the closure, the members of the state in hand and the stack of the closure: one rented array. The stack
        // holds at most one entry more than the forks that entered the set, so it needs no more than the instructions and two.
        int length = code.Length;
        arena = ArrayPool<int>.Shared.Rent((4 * length) + 2);
        visited = new SparseSet(arena.AsSpan(0, length), arena.AsSpan(length, length));
        members = arena.AsSpan(2 * length, length);
        stack = arena.AsSpan(3 * length, length + 2);

        // The table of the rows that are known, open addressing, at least twice as many places as rows.
        int places = (int)BitOperations.RoundUpToPowerOf2((uint)(2 * maxRows));
        slots = ArrayPool<int>.Shared.Rent(places);
        slots.AsSpan(0, places).Clear();
        slotMask = places - 1;
        keys = new PooledList<int>(64);
        rows = new PooledList<int>(RowFields * 16);
        table = default;
        rowCount = 0;
        stride = 0;
        memberCount = 0;
        baseOffset = 0;
        baseMatched = false;
        matched = false;
        work = 0;
        refusal = DfaRefusal.None;
    }

    // The automaton of a program, or null when it has none.
    internal static Dfa? TryBuild(CompiledPattern program) => TryBuild(program, MaxStates, MaxClasses, MaxWork, out _);

    // The same, with the limits and the reason when there is none.
    internal static Dfa? TryBuild(CompiledPattern program, int maxStates, int maxClasses, long maxWork, out DfaRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStates, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxClasses, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxClasses, MaxClasses);

        // An offset of a row is an ushort.
        ArgumentOutOfRangeException.ThrowIfGreaterThan((maxStates + 1L) * maxClasses, ushort.MaxValue);
        if (!HasOnlyTextConditions(program.Instructions, out bool hasBegin))
        {
            refusal = DfaRefusal.Conditions;
            return null;
        }

        var builder = new DfaBuilder(program.Instructions, program.Start, hasBegin, maxStates, maxWork);
        try
        {
            Dfa? built = builder.Build(program, maxClasses);
            refusal = builder.refusal;
            return built;
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static bool HasOnlyTextConditions(ReadOnlySpan<Instruction> code, out bool hasBegin)
    {
        hasBegin = false;
        foreach (ref readonly Instruction instruction in code)
        {
            if (instruction.Kind != InstructionKind.Empty)
            {
                continue;
            }

            if (instruction.A != EmptyCondition.BeginText && instruction.A != EmptyCondition.EndText)
            {
                return false;
            }

            hasBegin |= instruction.A == EmptyCondition.BeginText;
        }

        return true;
    }

    private void Dispose()
    {
        ArrayPool<int>.Shared.Return(arena);
        ArrayPool<int>.Shared.Return(slots);
        keys.Dispose();
        rows.Dispose();
        table.Dispose();
    }

    private Dfa? Build(CompiledPattern program, int maxClasses)
    {
        CodePointClasses? classes = MakeClasses(program, maxClasses, out ulong[] ulongs);
        try
        {
            if (classes is null)
            {
                return null;
            }

            stride = classes.Count;
            table = new PooledList<ushort>(Math.Max(16 * stride, 16));
            Span<ulong> masks = ulongs.AsSpan(0, code.Length);
            return Run(masks) ? Finish(classes, program.Prefix) : null;
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(ulongs);
        }
    }

    // The classes of the program; the rented array holds the masks of the instructions and the scratch of the classes.
    private CodePointClasses? MakeClasses(CompiledPattern program, int maxClasses, out ulong[] ulongs)
    {
        int words = (code.Length + 63) >> 6;
        ulongs = ArrayPool<ulong>.Shared.Rent(code.Length + ((maxClasses + 1) * words));
        ulongs.AsSpan(0, code.Length).Clear();
        Span<ulong> masks = ulongs.AsSpan(0, code.Length);
        Span<ulong> scratch = ulongs.AsSpan(code.Length, (maxClasses + 1) * words);
        return CodePointClasses.TryCreate(code, program.Ranges, maxClasses, maxWork, ref work, masks, scratch, out refusal);
    }

    // The subset construction: the rows of the table. False, with the reason in refusal, when a limit stops it.
    private bool Run(ReadOnlySpan<ulong> masks)
    {
        // Row 0 cannot match, row 1 has matched: both stay where they are.
        int emptyKey = keys.Count;
        keys.Add(0);
        AddRow(emptyKey, 0, endAccepts: false);
        AddRow(emptyKey, 0, endAccepts: true);
        table.Items.AsSpan(stride, stride).Fill((ushort)stride);

        // The closure of the first instruction without the beginning of the text is in each state; it is kept as a key of its own.
        visited.Clear();
        memberCount = 0;
        matched = false;
        Close(start, followBegin: false, followEnd: false);
        baseMatched = matched;
        baseOffset = keys.Count;
        members[..memberCount].Sort();
        keys.Add(memberCount);
        keys.AddRange(members[..memberCount]);

        // The first state is that closure with the beginning of the text in it.
        visited.Clear();
        memberCount = 0;
        matched = false;
        Close(start, followBegin: true, followEnd: false);
        if (matched)
        {
            initialRow = MatchedRow;
            return true;
        }

        members[..memberCount].Sort();
        initialRow = RowFor(members[..memberCount], registered: !hasBegin, initialState: true);
        return initialRow >= 0 && Fill(masks);
    }

    // The next row for each class from each row, the rows that the filling adds included.
    private bool Fill(ReadOnlySpan<ulong> masks)
    {
        for (int row = 2; row < rowCount; row++)
        {
            for (int cls = 0; cls < stride; cls++)
            {
                int next = NextRow(row, cls, masks);
                if (next < 0)
                {
                    return false;
                }

                table.Items[(row * stride) + cls] = (ushort)(next * stride);
            }

            if (work > maxWork)
            {
                refusal = DfaRefusal.Work;
                return false;
            }
        }

        return true;
    }

    // The row that a row goes to on a class: the instructions that accept the class lead on, and the first instruction begins a match
    // at the next position. -1 when the automaton would pass a limit.
    private int NextRow(int row, int cls, ReadOnlySpan<ulong> masks)
    {
        ulong bit = 1UL << cls;
        int keyOffset = rows.Items[row * RowFields];
        int length = keys.Items[keyOffset];
        visited.Clear();
        memberCount = 0;
        matched = false;
        for (int place = 1; place <= length; place++)
        {
            int index = keys.Items[keyOffset + place];
            if ((masks[index] & bit) == 0)
            {
                continue;
            }

            Close(code[index].Next, followBegin: false, followEnd: false);
            if (matched)
            {
                return MatchedRow;
            }
        }

        work += length;
        AddBase();
        if (baseMatched)
        {
            return MatchedRow;
        }

        if (memberCount == 0)
        {
            return DeadRow;
        }

        members[..memberCount].Sort();
        return RowFor(members[..memberCount], registered: true, initialState: false);
    }

    // Adds the members of the closure of the first instruction that the state in hand does not have yet.
    private void AddBase()
    {
        int length = keys.Items[baseOffset];
        for (int place = 1; place <= length; place++)
        {
            int index = keys.Items[baseOffset + place];
            if (!visited.Contains(index))
            {
                visited.Add(index);
                members[memberCount++] = index;
            }
        }

        work += length;
    }

    // Adds the instruction and what it leads to without reading a code point. The members are the instructions that read a code point
    // and the conditions \z that were not passed. True in matched when the Match instruction was reached.
    private void Close(int seed, bool followBegin, bool followEnd)
    {
        int top = 0;
        stack[top++] = seed;
        while (top > 0)
        {
            int index = stack[--top];
            if (visited.Contains(index))
            {
                continue;
            }

            visited.Add(index);
            work++;
            ref readonly Instruction instruction = ref code[index];
            switch (instruction.Kind)
            {
                case InstructionKind.Alt:
                    stack[top++] = instruction.A;
                    stack[top++] = instruction.Next;
                    break;
                case InstructionKind.Match:
                    matched = true;
                    return;
                case InstructionKind.Empty:
                    bool passes = instruction.A == EmptyCondition.BeginText ? followBegin : followEnd;
                    if (passes)
                    {
                        stack[top++] = instruction.Next;
                    }
                    else if (instruction.A == EmptyCondition.EndText)
                    {
                        members[memberCount++] = index;
                    }

                    break;
                default:
                    members[memberCount++] = index;
                    break;
            }
        }
    }

    // Whether the text may end in a state, which is when Match is reached through the conditions \z of its members; the first state of
    // a text that is empty has the beginning too.
    private bool EndAccepts(ReadOnlySpan<int> key, bool initialState)
    {
        visited.Clear();
        memberCount = 0;
        matched = false;
        foreach (int index in key)
        {
            if (code[index].Kind == InstructionKind.Empty)
            {
                Close(code[index].Next, followBegin: initialState, followEnd: true);
                if (matched)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The row of a sorted set, a new one when it has none; -1, with the reason in refusal, when the automaton would have more rows than
    // the limit. The first state of a program that has \A is its own row: its answer for the end differs from that of the same set later
    // in the text. The set may be the members of the state in hand: it is copied before they are used again.
    private int RowFor(ReadOnlySpan<int> key, bool registered, bool initialState)
    {
        int hash = Hash(key);
        if (registered)
        {
            int known = Find(key, hash);
            if (known >= 0)
            {
                return known;
            }
        }

        if (rowCount == maxRows)
        {
            refusal = DfaRefusal.States;
            return -1;
        }

        int keyOffset = keys.Count;
        keys.Add(key.Length);
        keys.AddRange(key);
        bool endAccept = EndAccepts(keys.Items.AsSpan(keyOffset + 1, key.Length), initialState);
        int row = AddRow(keyOffset, hash, endAccept);
        if (registered)
        {
            int slot = hash & slotMask;
            while (slots[slot] != 0)
            {
                slot = (slot + 1) & slotMask;
            }

            slots[slot] = row + 1;
        }

        return row;
    }

    // The row of a set that a row is registered for, or -1.
    private readonly int Find(ReadOnlySpan<int> key, int hash)
    {
        int slot = hash & slotMask;
        while (true)
        {
            int entry = slots[slot];
            if (entry == 0)
            {
                return -1;
            }

            int row = entry - 1;
            int keyOffset = rows.Items[row * RowFields];
            if (rows.Items[(row * RowFields) + 1] == hash && keys.Items.AsSpan(keyOffset + 1, keys.Items[keyOffset]).SequenceEqual(key))
            {
                return row;
            }

            slot = (slot + 1) & slotMask;
        }
    }

    private int AddRow(int keyOffset, int hash, bool endAccepts)
    {
        rows.Add(keyOffset);
        rows.Add(hash);
        rows.Add(endAccepts ? 1 : 0);
        table.AddZeros(stride);
        return rowCount++;
    }

    private static int Hash(ReadOnlySpan<int> key)
    {
        uint hash = 2166136261;
        foreach (int member in key)
        {
            hash = (hash ^ (uint)member) * 16777619;
        }

        return (int)(hash ^ (hash >> 15));
    }

    // The automaton of the rows that the construction made, with the way to go on from the state of no live thread: the literal prefix
    // of the program when it has one, else the units that leave the state.
    private readonly Dfa Finish(CodePointClasses classes, char[]? programPrefix)
    {
        int baseLength = keys.Items[baseOffset];
        ReadOnlySpan<int> baseKey = keys.Items.AsSpan(baseOffset + 1, baseLength);
        int restRow = baseLength > 0 ? Find(baseKey, Hash(baseKey)) : -1;
        char[]? prefix = null;
        SearchValues<char>? units = null;
        int unitCount = 0;
        if (restRow >= 2)
        {
            prefix = programPrefix;
            if (prefix is null)
            {
                Span<char> leaving = stackalloc char[AsciiUnits];
                unitCount = LeavingUnits(classes, restRow, leaving);
                if (unitCount > MaxSkipUnits && leaving[..unitCount].ContainsAnyExceptInRange('\0', '\u007F'))
                {
                    unitCount = 0;
                }

                units = unitCount > 0 ? SearchValues.Create(leaving[..unitCount]) : null;
            }
        }

        // One array: the transitions of the rows, then the answers for the end of the text, one for each row.
        int transitions = rowCount * stride;
        var finalTable = new ushort[transitions + rowCount];
        table.Items.AsSpan(0, transitions).CopyTo(finalTable);
        for (int row = 0; row < rowCount; row++)
        {
            finalTable[transitions + row] = (ushort)rows.Items[(row * RowFields) + 2];
        }

        return new Dfa(finalTable, classes, initialRow, restRow, prefix, units, unitCount);
    }

    // The code units that leave the row of no live thread for another row, when they fit in the room given (the units of an interval
    // that does not are never collected); 0 when they are many, or none, or not all of a kind that a search for units finds. The
    // caller keeps them when they are at most MaxSkipUnits or all ASCII.
    private readonly int LeavingUnits(CodePointClasses classes, int restRow, Span<char> units)
    {
        int count = 0;
        int offset = restRow * stride;
        for (int cls = 0; cls < stride; cls++)
        {
            if (table.Items[offset + cls] != offset && !classes.TryAddUnits(cls, units, ref count))
            {
                return 0;
            }
        }

        return count;
    }
}

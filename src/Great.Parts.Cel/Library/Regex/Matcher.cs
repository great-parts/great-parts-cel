using System;
using System.Buffers;
using Great.Parts.Cel.Library.Unicode;

namespace Great.Parts.Cel.Library.Regex;

// The engine: does some part of a text match a program. It runs the states of the program as a set, in time linear in the text and
// with no backtracking and no recursion; there are no priorities and no groups, so a state is only in the set or not.
internal static class Matcher
{
    // The code point that stands for a lone surrogate.
    private const int ReplacementCharacter = 0xFFFD;

    // Whether a part of the text matches. A match may begin anywhere, so the first instruction is added again at each position.
    // The steps that the run made — the entries of an instruction into a set — are added to steps: at most (n + 1) · m for a text
    // of n UTF-16 code units and a program of size m.
    internal static bool IsMatch(CompiledPattern program, ReadOnlySpan<char> text, ref long steps)
    {
        ArgumentNullException.ThrowIfNull(program);

        // Five arrays — the two sparse sets of two each, and the stack of the closure — of two ints more than the instructions: one
        // rented array, so that nothing else is allocated in steady state.
        int slots = program.Instructions.Length + 2;
        int[] rented = ArrayPool<int>.Shared.Rent(5 * slots);
        try
        {
            return Run(program, text, rented, slots, ref steps);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    // The conditions of zero width that hold between two code points, as a mask of EmptyCondition; -1 stands for no code point.
    internal static int Context(int before, int after)
    {
        int conditions = 0;
        if (before < 0)
        {
            conditions |= EmptyCondition.BeginText | EmptyCondition.BeginLine;
        }
        else if (before == '\n')
        {
            conditions |= EmptyCondition.BeginLine;
        }

        if (after < 0)
        {
            conditions |= EmptyCondition.EndText | EmptyCondition.EndLine;
        }
        else if (after == '\n')
        {
            conditions |= EmptyCondition.EndLine;
        }

        return conditions | (IsWord(before) != IsWord(after) ? EmptyCondition.WordBoundary : EmptyCondition.NoWordBoundary);
    }

    private static bool Run(CompiledPattern program, ReadOnlySpan<char> text, Span<int> work, int slots, ref long steps)
    {
        ReadOnlySpan<Instruction> code = program.Instructions;
        ReadOnlySpan<int> ranges = program.Ranges;
        var current = new SparseSet(work[..slots], work.Slice(slots, slots));
        var next = new SparseSet(work.Slice(2 * slots, slots), work.Slice(3 * slots, slots));
        Span<int> stack = work.Slice(4 * slots, slots);
        int position = 0;
        int point = Read(text, 0, out int width);
        int conditions = Context(-1, point);
        while (true)
        {
            if (Add(code, ref current, stack, program.Start, conditions, ref steps))
            {
                return true;
            }

            if (point < 0)
            {
                return false;
            }

            int afterPosition = position + width;
            int after = Read(text, afterPosition, out int afterWidth);
            int afterConditions = Context(point, after);
            next.Clear();
            for (int place = 0; place < current.Count; place++)
            {
                ref readonly Instruction instruction = ref code[current.Member(place)];
                if (Accepts(in instruction, ranges, point) && Add(code, ref next, stack, instruction.Next, afterConditions, ref steps))
                {
                    return true;
                }
            }

            SparseSet carried = current;
            current = next;
            next = carried;
            position = afterPosition;
            point = after;
            width = afterWidth;
            conditions = afterConditions;
        }
    }

    // Adds an instruction and, through the forks and the conditions that hold, everything it leads to without reading a code point.
    // The stack holds at most one entry more than the forks that entered the set, so it never needs more than the slots it has.
    // True when the Match instruction entered the set.
    private static bool Add(ReadOnlySpan<Instruction> code, ref SparseSet set, Span<int> stack, int first, int conditions, ref long steps)
    {
        int top = 0;
        stack[top++] = first;
        while (top > 0)
        {
            int index = stack[--top];
            if (set.Contains(index))
            {
                continue;
            }

            set.Add(index);
            steps++;
            ref readonly Instruction instruction = ref code[index];
            switch (instruction.Kind)
            {
                case InstructionKind.Alt:
                    stack[top++] = instruction.A;
                    stack[top++] = instruction.Next;
                    break;
                case InstructionKind.Empty:
                    if ((conditions & instruction.A) != 0)
                    {
                        stack[top++] = instruction.Next;
                    }

                    break;
                case InstructionKind.Match:
                    return true;
            }
        }

        return false;
    }

    // Whether an instruction that reads a code point accepts it; the other kinds read nothing.
    private static bool Accepts(in Instruction instruction, ReadOnlySpan<int> ranges, int point)
    {
        switch (instruction.Kind)
        {
            case InstructionKind.Char:
                return point == instruction.A;
            case InstructionKind.CharFold:
                return AcceptsFolded(instruction.A, point);
            case InstructionKind.Class:
                return StaticTables.ContainsRange(ranges.Slice(instruction.A << 1, instruction.B << 1), point);
            case InstructionKind.StaticClass:
                return StaticTables.Contains(instruction.A, point) != (instruction.B != 0);
            case InstructionKind.Any:
                return true;
            case InstructionKind.AnyNotNewline:
                return point != '\n';
            default:
                return false;
        }
    }

    // A code point is accepted when it is the member of the orbit that the instruction names or one of the other members.
    private static bool AcceptsFolded(int member, int point)
    {
        if (point == member)
        {
            return true;
        }

        for (int other = UnicodeTables.Next(member); other != member; other = UnicodeTables.Next(other))
        {
            if (other == point)
            {
                return true;
            }
        }

        return false;
    }

    // The code point at an index of the text and the number of code units it takes; -1 and 0 at the end. A high surrogate followed
    // by a low one is one code point; a lone surrogate is U+FFFD and one position by itself.
    private static int Read(ReadOnlySpan<char> text, int index, out int width)
    {
        if ((uint)index >= (uint)text.Length)
        {
            width = 0;
            return -1;
        }

        int unit = text[index];
        width = 1;
        if (unit < 0xD800 || unit > 0xDFFF)
        {
            return unit;
        }

        if (unit <= 0xDBFF && index + 1 < text.Length)
        {
            int low = text[index + 1];
            if (low >= 0xDC00 && low <= 0xDFFF)
            {
                width = 2;
                return 0x10000 + ((unit - 0xD800) << 10) + (low - 0xDC00);
            }
        }

        return ReplacementCharacter;
    }

    // A word character for the conditions of a word boundary is one of [0-9A-Za-z_] and nothing else.
    private static bool IsWord(int point)
    {
        return (uint)(point - '0') <= 9 || (uint)((point | 0x20) - 'a') <= 'z' - 'a' || point == '_';
    }
}

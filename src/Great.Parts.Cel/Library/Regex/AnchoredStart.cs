using System;
using System.Buffers;

namespace Great.Parts.Cel.Library.Regex;

// Whether every match of a program begins at the start of the text: every path from the first instruction, through the forks and the
// conditions of zero width, meets the beginning of the text (\A, which ^ without the flag m is) before it reads a code point or ends the
// program. The conditions other than the beginning of the text only remove matches, so a path through them is followed to the end.
internal static class AnchoredStart
{
    internal static bool Of(ReadOnlySpan<Instruction> code, int start)
    {
        // The flags of the instructions seen and the stack of the paths still to follow: one rented array. An instruction is marked when
        // it is pushed, so the stack never holds more than the instructions.
        int length = code.Length;
        int[] rented = ArrayPool<int>.Shared.Rent(2 * length);
        try
        {
            Span<int> seen = rented.AsSpan(0, length);
            Span<int> stack = rented.AsSpan(length, length);
            seen.Clear();
            int top = 0;
            stack[top++] = start;
            seen[start] = 1;
            while (top > 0)
            {
                ref readonly Instruction instruction = ref code[stack[--top]];
                switch (instruction.Kind)
                {
                    case InstructionKind.Alt:
                        Push(stack, seen, ref top, instruction.A);
                        Push(stack, seen, ref top, instruction.Next);
                        break;
                    case InstructionKind.Empty when instruction.A == EmptyCondition.BeginText:
                        break;
                    case InstructionKind.Empty:
                        Push(stack, seen, ref top, instruction.Next);
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    private static void Push(Span<int> stack, Span<int> seen, ref int top, int index)
    {
        if (seen[index] == 0)
        {
            seen[index] = 1;
            stack[top++] = index;
        }
    }
}

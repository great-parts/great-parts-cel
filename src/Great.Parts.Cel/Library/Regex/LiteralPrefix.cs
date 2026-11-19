using System;
using System.Buffers;
using System.Text;
using Great.Parts.Cel.Library.Text;

namespace Great.Parts.Cel.Library.Regex;

// The literal prefix of a program: the code units that every match of the program begins with. It is read from the first
// instruction along the instructions that read one code point each — a Char — and through the conditions of zero width, and it stops
// at anything else: a fork, a class, a folded character (so a pattern that begins with (?i) has none), and the end of the program. A
// condition of zero width only removes matches, so the prefix of what remains is still the beginning of each of them.
//
// A code point that the text cannot show is not part of the prefix: U+FFFD, which a lone surrogate of a text is read as, and a
// surrogate, so a search for the prefix as code units never passes a place that the engine would read as the prefix. The prefix is
// cut at the length that SubstringSearch keeps on the stack, and not inside a pair of surrogates: a prefix of a prefix is still a
// prefix.
internal static class LiteralPrefix
{
    // The code point that stands for a lone surrogate.
    private const int ReplacementCharacter = 0xFFFD;

    // The prefix of at least two code units, or null when there is none.
    internal static char[]? Of(ReadOnlySpan<Instruction> code, int start)
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(SubstringSearch.StackPatternLength);
        try
        {
            int length = Read(code, start, buffer);
            return length < 2 ? null : buffer.AsSpan(0, length).ToArray();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    // Writes the prefix into the buffer, of which the first SubstringSearch.StackPatternLength units are used; returns its length.
    private static int Read(ReadOnlySpan<Instruction> code, int start, char[] buffer)
    {
        int length = 0;
        int index = start;
        for (int hops = 0; hops < code.Length; hops++)
        {
            ref readonly Instruction instruction = ref code[index];
            if (instruction.Kind == InstructionKind.Empty)
            {
                index = instruction.Next;
                continue;
            }

            if (instruction.Kind != InstructionKind.Char || !IsShown(instruction.A))
            {
                break;
            }

            var rune = new Rune(instruction.A);
            if (length + rune.Utf16SequenceLength > SubstringSearch.StackPatternLength)
            {
                break;
            }

            length += rune.EncodeToUtf16(buffer.AsSpan(length));
            index = instruction.Next;
        }

        return length;
    }

    // Whether a code point is one that a text shows as itself: a scalar value other than U+FFFD.
    private static bool IsShown(int codePoint) => codePoint != ReplacementCharacter && Rune.IsValid(codePoint);
}

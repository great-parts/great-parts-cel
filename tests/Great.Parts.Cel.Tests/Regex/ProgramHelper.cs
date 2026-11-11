using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The helpers of the tests of the program and the engine: the build of a pattern, a run with its steps, and the program as text.
internal static class ProgramHelper
{
    internal static CompiledPattern Compile(string pattern)
    {
        using PatternTree tree = ParseHelper.Parse(pattern);
        return ProgramBuilder.Build(tree);
    }

    internal static bool IsMatch(string pattern, string text) => IsMatch(Compile(pattern), text, out _);

    internal static bool IsMatch(CompiledPattern program, string text, out long steps)
    {
        steps = 0;
        return Matcher.IsMatch(program, text, ref steps);
    }

    // The program as text, from the first instruction, in the order a walk finds the instructions — the first successor before the
    // second — so that the text does not depend on the order in which the build made them: "0:char(a)>1 1:match". An instruction
    // names its successor by its place in the text; a fork names two.
    internal static string Listing(CompiledPattern program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ReadOnlySpan<Instruction> code = program.Instructions;
        var place = new Dictionary<int, int>();
        var order = new List<int>();
        var pending = new Stack<int>();
        pending.Push(program.Start);
        while (pending.Count > 0)
        {
            int index = pending.Pop();
            if (!place.TryAdd(index, order.Count))
            {
                continue;
            }

            order.Add(index);
            switch (code[index].Kind)
            {
                case InstructionKind.Match:
                    break;
                case InstructionKind.Alt:
                    pending.Push(code[index].A);
                    pending.Push(code[index].Next);
                    break;
                default:
                    pending.Push(code[index].Next);
                    break;
            }
        }

        var text = new StringBuilder();
        foreach (int index in order)
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(place[index].ToString(CultureInfo.InvariantCulture)).Append(':').Append(Describe(program, code[index], place));
        }

        return text.ToString();
    }

    // The instruction as text: its kind, what it tests and the places of its successors.
    private static string Describe(CompiledPattern program, Instruction instruction, Dictionary<int, int> place)
    {
        string next = ">" + (instruction.Kind == InstructionKind.Match ? string.Empty : place[instruction.Next].ToString(CultureInfo.InvariantCulture));
        switch (instruction.Kind)
        {
            case InstructionKind.Char:
                return $"char({Show(instruction.A)}){next}";
            case InstructionKind.CharFold:
                return $"fold({Show(ClassBuilder.MinFold(instruction.A))}){next}";
            case InstructionKind.Class:
                return $"class[{ParseHelper.Ranges(program.Ranges.Slice(instruction.A * 2, instruction.B * 2))}]{next}";
            case InstructionKind.StaticClass:
                return $"table({(instruction.B != 0 ? "!" : string.Empty)}{instruction.A.ToString(CultureInfo.InvariantCulture)}){next}";
            case InstructionKind.Any:
                return "any" + next;
            case InstructionKind.AnyNotNewline:
                return "any-not-newline" + next;
            case InstructionKind.Alt:
                return string.Create(CultureInfo.InvariantCulture, $"alt({place[instruction.Next]},{place[instruction.A]})");
            case InstructionKind.Empty:
                return $"empty({Condition(instruction.A)}){next}";
            default:
                return "match";
        }
    }

    private static string Condition(int condition)
    {
        return condition switch
        {
            EmptyCondition.BeginLine => "begin-line",
            EmptyCondition.EndLine => "end-line",
            EmptyCondition.BeginText => "begin-text",
            EmptyCondition.EndText => "end-text",
            EmptyCondition.WordBoundary => "word-boundary",
            _ => "not-word-boundary",
        };
    }

    private static string Show(int codePoint)
    {
        return codePoint is > 0x20 and < 0x7F
            ? ((char)codePoint).ToString()
            : string.Create(CultureInfo.InvariantCulture, $"U+{codePoint:X4}");
    }

    // The checks that every program meets: it ends with the one Match, every successor is an instruction, a class names a run of
    // pairs of the program, and a program has the instructions and the ranges that the parse counted.
    internal static void AssertWellFormed(string pattern)
    {
        using PatternTree tree = ParseHelper.Parse(pattern);
        int instructionsCounted = tree.Instructions(tree.Root) + 1;
        int size = tree.Size;
        int owned = tree.OwnedRanges;
        CompiledPattern program = ProgramBuilder.Build(tree);
        ReadOnlySpan<Instruction> code = program.Instructions;

        Assert.Equal(instructionsCounted, code.Length);
        Assert.Equal(InstructionKind.Match, code[^1].Kind);
        Assert.Equal(size, program.Size);
        Assert.InRange(program.Ranges.Length / 2, 0, owned);
        Assert.InRange(program.Start, 0, code.Length - 1);
        for (int i = 0; i < code.Length; i++)
        {
            Instruction instruction = code[i];
            Assert.True(Enum.IsDefined(instruction.Kind), $"{pattern}: instruction {i} has no kind");
            Assert.Equal(i == code.Length - 1, instruction.Kind == InstructionKind.Match);
            if (instruction.Kind != InstructionKind.Match)
            {
                Assert.InRange(instruction.Next, 0, code.Length - 1);
            }

            if (instruction.Kind == InstructionKind.Alt)
            {
                Assert.InRange(instruction.A, 0, code.Length - 1);
            }

            if (instruction.Kind == InstructionKind.Class)
            {
                Assert.InRange(instruction.A, 0, program.Ranges.Length / 2);
                Assert.InRange(instruction.A + instruction.B, 0, program.Ranges.Length / 2);
            }
        }
    }
}

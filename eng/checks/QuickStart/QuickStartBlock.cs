using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One step of the quick start of the README: a shell command, or the C# program that is written into the current directory of the session.</summary>
/// <param name="Line">The line of the README, from 1: the line of the command, or the opening fence of the block with the program.</param>
/// <param name="Text">The command, or the text of the program.</param>
/// <param name="IsProgram">Whether the step writes the program instead of running a command.</param>
internal sealed record QuickStartStep(int Line, string Text, bool IsProgram);

/// <summary>
/// The block of steps of the quick start of <c>README.md</c> (<c>docs/design/eng-release.md</c>, "Format of the block"): the lines between
/// <c>&lt;!-- quick-start:begin --&gt;</c> and <c>&lt;!-- quick-start:end --&gt;</c>. Only fenced blocks are read: <c>shell</c> blocks give one command
/// for every line that does not start with <c>#</c>, the one <c>csharp</c> block gives the program, blocks of other kinds are for the reader.
/// </summary>
internal sealed class QuickStartBlock
{
    /// <summary>The line that starts the block.</summary>
    internal const string BeginMarker = "<!-- quick-start:begin -->";

    /// <summary>The line that ends the block.</summary>
    internal const string EndMarker = "<!-- quick-start:end -->";

    /// <summary>The file of the program, in the current directory of the session.</summary>
    internal const string ProgramFile = "Program.cs";

    private const string ShellKind = "shell";
    private const string ProgramKind = "csharp";

    private QuickStartBlock(string path, int beginLine, IReadOnlyList<QuickStartStep> steps, IReadOnlyList<ExpectedLine> expected)
    {
        Path = path;
        BeginLine = beginLine;
        Steps = steps;
        Expected = expected;
    }

    /// <summary>The path of the README, relative to the root, with <c>/</c>.</summary>
    internal string Path { get; }

    /// <summary>The line of the begin marker, from 1.</summary>
    internal int BeginLine { get; }

    /// <summary>The steps in the order of the README.</summary>
    internal IReadOnlyList<QuickStartStep> Steps { get; }

    /// <summary>The lines that the last command prints at its end, read from the program as in the check of the examples; never empty, a block without them cannot be read.</summary>
    internal IReadOnlyList<ExpectedLine> Expected { get; }

    /// <summary>Reads the block of the page.</summary>
    /// <param name="path">The path of the page, relative to the root, with <c>/</c>.</param>
    /// <param name="text">The text of the page.</param>
    /// <exception cref="CheckInputException">The page has no block, has more than one, or has a block that cannot be read.</exception>
    internal static QuickStartBlock Read(string path, string text)
    {
        string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        (int begin, int end) = Markers(path, lines);
        List<MarkdownExamples.FencedBlock> inside = [.. MarkdownExamples.Blocks(lines).Where(block => block.FenceLine > begin && block.EndLine < end)];
        List<MarkdownExamples.FencedBlock> programs = [.. inside.Where(block => MarkdownExamples.FirstWord(block.Info) == ProgramKind)];
        if (programs.Count != 1 || programs[0].Info != ProgramKind)
        {
            throw Unreadable(path, $"it needs exactly one block '{ProgramKind}' with no word after it");
        }

        List<QuickStartStep> steps = [];
        foreach (MarkdownExamples.FencedBlock block in inside)
        {
            string kind = MarkdownExamples.FirstWord(block.Info);
            if (kind == ShellKind)
            {
                steps.AddRange(Commands(block));
            }
            else if (kind == ProgramKind)
            {
                steps.Add(new QuickStartStep(block.FenceLine, string.Join('\n', block.Lines) + "\n", IsProgram: true));
            }
        }

        if (!steps.Exists(step => !step.IsProgram))
        {
            throw Unreadable(path, $"it has no command in a block '{ShellKind}'");
        }

        int firstLine = programs[0].FenceLine + 1;
        Example? example = MarkdownExamples.Read(path, text).FirstOrDefault(candidate => candidate.FirstLine == firstLine);
        if (example is null || example.Expected.Count == 0)
        {
            throw Unreadable(path, $"the block '{ProgramKind}' shows no expected output: give '// Output: <line>' comments or a 'text' block after it");
        }

        return new QuickStartBlock(path, begin, steps, example.Expected);
    }

    // The lines of the begin and the end marker, from 1; exactly one pair, in this order.
    private static (int Begin, int End) Markers(string path, string[] lines)
    {
        List<int> begins = Lines(lines, BeginMarker);
        List<int> ends = Lines(lines, EndMarker);
        if (begins.Count == 0 && ends.Count == 0)
        {
            throw new CheckInputException($"{path} has no quick-start block: the lines '{BeginMarker}' and '{EndMarker}' are missing");
        }

        if (begins.Count != 1 || ends.Count != 1 || begins[0] > ends[0])
        {
            string found = $"{begins.Count.ToString(CultureInfo.InvariantCulture)} lines '{BeginMarker}' and {ends.Count.ToString(CultureInfo.InvariantCulture)} lines '{EndMarker}'";
            throw Unreadable(path, $"it is one begin marker followed by one end marker, and the page has {found}");
        }

        return (begins[0], ends[0]);
    }

    private static List<int> Lines(string[] lines, string marker) =>
        [.. Enumerable.Range(0, lines.Length).Where(index => lines[index].Trim() == marker).Select(index => index + 1)];

    private static IEnumerable<QuickStartStep> Commands(MarkdownExamples.FencedBlock block)
    {
        for (int offset = 0; offset < block.Lines.Count; offset++)
        {
            string command = block.Lines[offset].Trim();
            if (command.Length > 0 && command[0] != '#')
            {
                yield return new QuickStartStep(block.FenceLine + 1 + offset, command, IsProgram: false);
            }
        }
    }

    private static CheckInputException Unreadable(string path, string reason) => new($"the quick-start block of {path} cannot be read: {reason}");
}

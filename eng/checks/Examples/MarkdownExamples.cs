using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the C# examples of a Markdown page (<c>docs/design/eng-docs.md</c>, "examples"): a fenced block whose information string starts
/// with the word <c>csharp</c>. The words after it are its options: <c>net11.0</c> and <c>skip: &lt;reason&gt;</c>, the reason being the rest of the
/// line. The expected output is the comments <c>// Output:</c> of the code or, when it has none, a <c>text</c> block directly after the
/// example, with only empty lines between them.
/// </summary>
internal static class MarkdownExamples
{
    private const string Language = "csharp";
    private const string TextLanguage = "text";
    private const string SkipDescription = "'csharp skip'";
    private const string SkipReasonPlace = "reason after the word skip in the same line";
    private static readonly char[] Blanks = [' ', '\t'];

    // The opening fence: three or more backticks or tildes with an information string; a backtick fence has no backtick in it.
    private static readonly Regex Opening = new(@"^(?<indent> *)(?<fence>`{3,}|~{3,})(?<info>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    // The option skip: a word of its own, with the reason after a colon.
    private static readonly Regex SkipOption = new(@"(?:^|\s)skip(?=$|[\s:])(?::(?<reason>.*))?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    /// <summary>Reads the examples of the page.</summary>
    /// <param name="path">The path of the page, relative to the root, with <c>/</c>.</param>
    /// <param name="text">The text of the page.</param>
    internal static IReadOnlyList<Example> Read(string path, string text)
    {
        string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        List<FencedBlock> blocks = Blocks(lines);
        List<Example> examples = [];
        for (int index = 0; index < blocks.Count; index++)
        {
            if (FirstWord(blocks[index].Info) == Language)
            {
                examples.Add(Build(path, blocks, index, lines));
            }
        }

        return examples;
    }

    private static Example Build(string path, List<FencedBlock> blocks, int index, string[] lines)
    {
        FencedBlock block = blocks[index];
        string info = block.Info;
        Match skip = SkipOption.Match(info);
        string options = skip.Success ? info[..skip.Index] : info;
        string target = options.Split(Blanks, StringSplitOptions.RemoveEmptyEntries).Contains(Example.DecimalTarget, StringComparer.Ordinal)
            ? Example.DecimalTarget
            : Example.DefaultTarget;
        ExampleSkip? skipped = skip.Success
            ? new ExampleSkip(new Position(block.FenceLine, block.Indent + 1), skip.Groups["reason"].Value.Trim(), SkipDescription, SkipReasonPlace)
            : null;

        int firstLine = block.FenceLine + 1;
        IReadOnlyList<ExpectedLine> expected = ExpectedOutput.FromComments(block.Lines, firstLine);
        if (expected.Count == 0)
        {
            expected = TextBlockAfter(blocks, index, lines);
        }

        return new Example(path, firstLine, block.Lines, target, skipped, expected);
    }

    // The lines of the text block that follows the example with only empty lines between them, or none.
    private static List<ExpectedLine> TextBlockAfter(List<FencedBlock> blocks, int index, string[] lines)
    {
        if (index + 1 >= blocks.Count || FirstWord(blocks[index + 1].Info) != TextLanguage)
        {
            return [];
        }

        FencedBlock example = blocks[index];
        FencedBlock shown = blocks[index + 1];
        for (int line = example.EndLine + 1; line < shown.FenceLine; line++)
        {
            if (!string.IsNullOrWhiteSpace(lines[line - 1]))
            {
                return [];
            }
        }

        List<ExpectedLine> expected = [];
        for (int offset = 0; offset < shown.Lines.Count; offset++)
        {
            string text = shown.Lines[offset];
            int removed = Math.Min(shown.Indent, text.Length - text.TrimStart(' ').Length);
            expected.Add(new ExpectedLine(text[removed..], new Position(shown.FenceLine + 1 + offset, removed + 1)));
        }

        return expected;
    }

    private static string FirstWord(string info) => info.Split(Blanks, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private static List<FencedBlock> Blocks(string[] lines)
    {
        List<FencedBlock> blocks = [];
        int index = 0;
        while (index < lines.Length)
        {
            Match opening = Opening.Match(lines[index]);
            if (!opening.Success || (opening.Groups["fence"].Value[0] == '`' && opening.Groups["info"].Value.Contains('`', StringComparison.Ordinal)))
            {
                index++;
                continue;
            }

            char fence = opening.Groups["fence"].Value[0];
            int length = opening.Groups["fence"].Length;
            List<string> content = [];
            int next = index + 1;
            while (next < lines.Length && !Closes(lines[next], fence, length))
            {
                content.Add(lines[next]);
                next++;
            }

            // The line of the closing fence; an unclosed block ends with the last line of the page.
            blocks.Add(new FencedBlock(opening.Groups["info"].Value.Trim(), index + 1, opening.Groups["indent"].Length, content, Math.Min(next + 1, lines.Length)));
            index = next + 1;
        }

        return blocks;
    }

    private static bool Closes(string line, char fence, int length)
    {
        string trimmed = line.Trim(' ');
        return trimmed.Length >= length && trimmed.All(character => character == fence);
    }

    private sealed record FencedBlock(string Info, int FenceLine, int Indent, List<string> Lines, int EndLine);
}

using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>The line and the column of a token of a text file, both starting with 1; the column counts UTF-16 code units.</summary>
/// <param name="Line">The line.</param>
/// <param name="Column">The column.</param>
internal readonly record struct TextPosition(int Line, int Column);

/// <summary>
/// The positions of the cases in the text of a <c>SimpleTestFile</c>. The binary form has no positions, so a scan of the text
/// skips comments and strings, counts braces and records each <c>section</c> token at the top level and each <c>test</c> token
/// inside a section. The n-th <c>test</c> token of the m-th section is the n-th case of the m-th section.
/// </summary>
internal static class CasePositions
{
    private const string SectionToken = "section";
    private const string TestToken = "test";

    /// <summary>Returns the positions of the <c>test</c> tokens of each section, in the order of the text.</summary>
    /// <param name="text">The text of the file.</param>
    internal static IReadOnlyList<IReadOnlyList<TextPosition>> Read(string text)
    {
        List<List<TextPosition>> sections = [];
        int depth = 0;
        int line = 1;
        int lineStart = 0;
        int index = 0;
        while (index < text.Length)
        {
            char character = text[index];
            int start = index;
            index++;
            switch (character)
            {
                case '\n':
                    line++;
                    lineStart = index;
                    break;
                case '#':
                    index = LineEnd(text, index);
                    break;
                case '"' or '\'':
                    index = StringEnd(text, index, character);
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                default:
                    if (IsWordCharacter(character))
                    {
                        index = WordEnd(text, index);
                        Record(sections, text.AsSpan(start, index - start), depth, new TextPosition(line, start - lineStart + 1));
                    }

                    break;
            }
        }

        return sections;
    }

    private static void Record(List<List<TextPosition>> sections, ReadOnlySpan<char> word, int depth, TextPosition position)
    {
        if (depth == 0 && word.SequenceEqual(SectionToken))
        {
            sections.Add([]);
        }
        else if (depth == 1 && sections.Count > 0 && word.SequenceEqual(TestToken))
        {
            sections[^1].Add(position);
        }
    }

    private static bool IsWordCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '.';

    private static int WordEnd(string text, int index)
    {
        while (index < text.Length && IsWordCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int LineEnd(string text, int index)
    {
        int length = text.AsSpan(index).IndexOf('\n');
        return length < 0 ? text.Length : index + length;
    }

    // A string of the text format ends at its unescaped quote and never spans lines; an unterminated string ends at the line end.
    private static int StringEnd(string text, int index, char quote)
    {
        while (index < text.Length && text[index] != '\n')
        {
            char character = text[index];
            index += character == '\\' ? 2 : 1;
            if (character == quote)
            {
                return index;
            }
        }

        return Math.Min(index, text.Length);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The expected output of an example and its comparison with the output of the program (<c>docs/design/eng-docs.md</c>, "examples").
/// The expected output is given by comments <c>// Output: &lt;line&gt;</c> in the lines that print, or by a <c>text</c> block after the
/// example.
/// </summary>
internal static class ExpectedOutput
{
    private const string Marker = "// Output:";

    /// <summary>
    /// Reads the comments <c>// Output: &lt;line&gt;</c> of the code, in source order. The comments are found in the syntax tree, so the text
    /// of a string literal is not one. One space after the colon is not part of the line.
    /// </summary>
    /// <param name="lines">The lines of the code.</param>
    /// <param name="firstLine">The line of the file that holds the first line of the code.</param>
    internal static IReadOnlyList<ExpectedLine> FromComments(IReadOnlyList<string> lines, int firstLine)
    {
        string code = string.Join('\n', lines);
        SourceLines places = new(code);
        List<ExpectedLine> expected = [];
        foreach (SyntaxTrivia comment in CSharpSyntaxTree.ParseText(code).GetRoot().DescendantTrivia().Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)))
        {
            string text = comment.ToString();
            if (!text.StartsWith(Marker, StringComparison.Ordinal))
            {
                continue;
            }

            Position at = places.Locate(comment.SpanStart);
            string line = text[Marker.Length..];
            expected.Add(new ExpectedLine(line.StartsWith(' ') ? line[1..] : line, new Position(firstLine + at.Line - 1, at.Column)));
        }

        return expected;
    }

    /// <summary>
    /// Compares the output of the program with the expected output. Only line endings are normalized, and a final line break is
    /// ignored. The finding is at the first expected line that differs; when the program prints more, at the last expected line, or at
    /// the first line of the example when nothing is expected.
    /// </summary>
    /// <param name="example">The example.</param>
    /// <param name="output">What the program wrote to standard output.</param>
    /// <returns>The finding, or <see langword="null"/> when the output is the expected one.</returns>
    internal static Finding? Compare(Example example, string output)
    {
        string actual = output.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (actual.EndsWith('\n'))
        {
            actual = actual[..^1];
        }

        if (actual == string.Join('\n', example.Expected.Select(line => line.Text)))
        {
            return null;
        }

        string[] printed = actual.Length == 0 ? [] : actual.Split('\n');
        (Position position, string message) = Difference(example, printed);
        return new Finding(example.Path, position.Line, position.Column, FindingSeverity.Error, EngCodes.Eng301, message);
    }

    private static (Position Position, string Message) Difference(Example example, string[] printed)
    {
        IReadOnlyList<ExpectedLine> expected = example.Expected;
        for (int index = 0; index < Math.Min(expected.Count, printed.Length); index++)
        {
            if (expected[index].Text != printed[index])
            {
                return (expected[index].Position, $"example output '{printed[index]}' differs from expected '{expected[index].Text}'");
            }
        }

        if (printed.Length > expected.Count)
        {
            return expected.Count == 0
                ? (example.Start, $"example prints '{printed[0]}' but shows no output")
                : (expected[^1].Position, $"example output continues with '{printed[expected.Count]}' after the last expected line '{expected[^1].Text}'");
        }

        return (expected[printed.Length].Position, $"example output ends before the expected line '{expected[printed.Length].Text}'");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Tells whether the change of a file between two commits does not change what the differential run does (<c>docs/design/eng-release.md</c>,
/// "The paths of ENG904"): a <c>*.cs</c> file that differs only in comments and in blank lines, and the <c>PublicAPI.Shipped.txt</c> and
/// <c>PublicAPI.Unshipped.txt</c> of one project that hold the same set of lines together. A line of a preprocessor directive and the text of a
/// disabled branch are kept as they are: they are code. The reading of a <c>*.cs</c> file is cautious: a text that the reader cannot follow
/// (a raw string literal, a string literal or a comment that is not closed) is a change of the code, so a mistake costs a repeated run, never a
/// skipped change of the code.
/// </summary>
internal static class CommentOnlyChange
{
    private const string Shipped = "PublicAPI.Shipped.txt";
    private const string Unshipped = "PublicAPI.Unshipped.txt";

    /// <summary>Whether the path is a file that <see cref="IsOnlyComments"/> can compare.</summary>
    /// <param name="path">The path relative to the repository root, with <c>/</c>.</param>
    internal static bool Holds(string path) => path.EndsWith(".cs", StringComparison.Ordinal) || IsPublicApi(path);

    /// <summary>Whether the file differs between the two commits in nothing but comments, blank lines, or the file of the API in which a line lies.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="before">The commit of the long run.</param>
    /// <param name="after">The checked commit.</param>
    /// <param name="path">The path of the file, for which <see cref="Holds"/> is true.</param>
    internal static bool IsOnlyComments(string root, string before, string after, string path)
    {
        if (IsPublicApi(path))
        {
            string directory = path[..(path.LastIndexOf('/') + 1)];
            return ApiLines(root, before, directory).SetEquals(ApiLines(root, after, directory));
        }

        List<string>? first = Normalize(Show(root, before, path));
        List<string>? second = Normalize(Show(root, after, path));
        return first is not null && second is not null && first.SequenceEqual(second, StringComparer.Ordinal);
    }

    private static bool IsPublicApi(string path)
    {
        int slash = path.LastIndexOf('/');
        string name = path[(slash + 1)..];
        return path.StartsWith("src/", StringComparison.Ordinal) && slash > 0 && (name == Shipped || name == Unshipped);
    }

    // The lines of both files of the API of a project together; a file that does not exist at the commit has no lines.
    private static HashSet<string> ApiLines(string root, string commit, string directory)
    {
        HashSet<string> lines = new(StringComparer.Ordinal);
        foreach (string name in new[] { Shipped, Unshipped })
        {
            string? text = Show(root, commit, directory + name);
            foreach (string line in (text ?? string.Empty).Split('\n'))
            {
                string trimmed = line.Trim().TrimStart((char)0xFEFF);
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }
        }

        return lines;
    }

    // The text of the file at the commit; no value when the file does not exist there.
    private static string? Show(string root, string commit, string path)
    {
        GitResult shown = Git.Run(root, "show", $"{commit}:{path}");
        return shown.Succeeded ? shown.Output : null;
    }

    // The lines of the text without the comments, without the trailing white space and without the blank lines; no value for a text that
    // the reader cannot follow, and for a file that does not exist.
    private static List<string>? Normalize(string? text)
    {
        if (text is null)
        {
            return null;
        }

        CodeReader reader = new(text.TrimStart((char)0xFEFF));
        if (!reader.ReadCode(inHole: false))
        {
            return null;
        }

        return [.. reader.Code.ToString().Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0)];
    }

    // Copies the code of a C# text and leaves out the comments; every method returns false for a text that it cannot follow.
    private sealed class CodeReader(string text)
    {
        private int _position;

        internal StringBuilder Code { get; } = new();

        // Reads to the end of the text, or, in the hole of an interpolated string, to the brace that closes the hole.
        internal bool ReadCode(bool inHole)
        {
            int depth = 0;
            bool lineStart = true;
            while (_position < text.Length)
            {
                char c = text[_position];
                if (c == '\n')
                {
                    Code.Append(c);
                    _position++;
                    lineStart = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    Code.Append(c);
                    _position++;
                }
                else if (lineStart && c == '#' && !inHole)
                {
                    CopyLine();
                }
                else if (inHole && c == '}' && depth == 0)
                {
                    Code.Append(c);
                    _position++;
                    return true;
                }
                else
                {
                    lineStart = false;
                    depth += c == '{' ? 1 : c == '}' ? -1 : 0;
                    if (!ReadToken(c, inHole))
                    {
                        return false;
                    }
                }
            }

            return !inHole;
        }

        // A line of a preprocessor directive is code, with its comment.
        private void CopyLine()
        {
            while (_position < text.Length && text[_position] != '\n')
            {
                Code.Append(text[_position]);
                _position++;
            }
        }

        private bool ReadToken(char c, bool inHole)
        {
            char next = _position + 1 < text.Length ? text[_position + 1] : '\0';
            if (c == '/' && next == '/')
            {
                // A line comment in the hole of an interpolated string may be a part of a format: the reader does not guess.
                return !inHole && SkipLineComment();
            }

            if (c == '/' && next == '*')
            {
                return SkipBlockComment();
            }

            if (c == '"')
            {
                return ReadString();
            }

            if (c == '\'')
            {
                return ReadCharacter();
            }

            Code.Append(c);
            _position++;
            return true;
        }

        private bool SkipLineComment()
        {
            while (_position < text.Length && text[_position] != '\n')
            {
                _position++;
            }

            return true;
        }

        private bool SkipBlockComment()
        {
            int end = text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                return false;
            }

            _position = end + 2;
            Code.Append(' ');
            return true;
        }

        private bool ReadCharacter()
        {
            Code.Append('\'');
            _position++;
            while (_position < text.Length && text[_position] != '\n')
            {
                char c = text[_position];
                Code.Append(c);
                _position++;
                if (c == '\'')
                {
                    return true;
                }

                if (c == '\\' && _position < text.Length && text[_position] != '\n')
                {
                    Code.Append(text[_position]);
                    _position++;
                }
            }

            return false;
        }

        // The prefix of the literal ($ and @) has been copied already; the opening quote is at the position.
        private bool ReadString()
        {
            if (text.AsSpan(_position).StartsWith("\"\"\"", StringComparison.Ordinal))
            {
                return false;
            }

            bool verbatim = false;
            bool interpolated = false;
            for (int index = _position - 1; index >= 0 && (text[index] == '$' || text[index] == '@'); index--)
            {
                verbatim |= text[index] == '@';
                interpolated |= text[index] == '$';
            }

            Code.Append('"');
            _position++;
            while (_position < text.Length)
            {
                char c = text[_position];
                if (c == '"' && verbatim && Peek() == '"')
                {
                    Code.Append("\"\"");
                    _position += 2;
                }
                else if (c == '"')
                {
                    Code.Append(c);
                    _position++;
                    return true;
                }
                else if (!ReadStringPart(c, verbatim, interpolated))
                {
                    return false;
                }
            }

            return false;
        }

        // One character of the body of a string, or its escape, or its hole; false when the string cannot be followed.
        private bool ReadStringPart(char c, bool verbatim, bool interpolated)
        {
            if (c == '\n' && !verbatim)
            {
                return false;
            }

            if (c == '\\' && !verbatim)
            {
                if (Peek() is '\0' or '\n')
                {
                    return false;
                }

                Code.Append(c).Append(text[_position + 1]);
                _position += 2;
                return true;
            }

            if (interpolated && (c == '{' || c == '}'))
            {
                if (Peek() == c)
                {
                    Code.Append(c).Append(c);
                    _position += 2;
                    return true;
                }

                if (c == '}')
                {
                    return false;
                }

                Code.Append(c);
                _position++;
                return ReadCode(inHole: true);
            }

            Code.Append(c);
            _position++;
            return true;
        }

        private char Peek() => _position + 1 < text.Length ? text[_position + 1] : '\0';
    }
}

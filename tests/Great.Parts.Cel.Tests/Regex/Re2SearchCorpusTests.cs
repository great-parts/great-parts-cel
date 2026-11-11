using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The search tests of RE2 as Go keeps them: Regex/Data/re2-search.txt is src/regexp/testdata/re2-search.txt of Go 1.27.3, and the
// reader follows testRE2 of exec_test.go. The test needs only whether there is a match, so a field of the results is a match when it
// is not "-"; the spans, the groups and the leftmost-longest rule are not looked at. Each pattern is asked twice, as Go asks it: the
// search with the pattern as it is, and the match of the whole subject with \A(?:pattern)\z; the four fields of a line are the
// whole-subject match and the search under leftmost-first, and the same two under leftmost-longest, so fields 0 and 2 are compared
// with the whole-subject answer and fields 1 and 3 with the search.
//
// Two kinds of entry are skipped, and nothing else:
//  - a pattern with \C. The library refuses it by design (it is not in Go either, and Go's own test skips it); the test asserts the
//    refusal instead, "invalid escape sequence" at the \C, and takes the results lines of the pattern as skipped.
//  - a pattern with \B on a subject that has a character beyond U+007F. RE2 looks for a boundary at every byte of UTF-8, so it finds
//    "not a word boundary" inside a character; the engine looks at the places between code points, as Go does, and Go's test skips
//    these cases for the same reason. The text of the file is read as UTF-8, so a subject is a string of code points.
public sealed class Re2SearchCorpusTests
{
    private static readonly Lazy<Corpus> Data = new(Corpus.Load);

    public static TheoryData<int> Lines
    {
        get
        {
            var data = new TheoryData<int>();
            foreach (Entry entry in Data.Value.Entries)
            {
                data.Add(entry.Line);
            }

            return data;
        }
    }

    // The file is the one that Go ships: the hash is that of the bytes copied from golang:1.27.3, so a change of the file, a line
    // ending that a checkout rewrote, or a truncation fails here before any case does.
    [Fact]
    public static void Corpus_IsTheFileOfGo()
    {
        byte[] bytes = File.ReadAllBytes(Corpus.Path);

        Assert.Equal("B6876D87B65A31A3D909F58F7100D9F7B501DFD4C79C3E6A73C50868DE434C9D", Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.Equal(236, Data.Value.Groups);
        Assert.Equal(1416, Data.Value.QuotedLines);
        Assert.Equal(1888, Data.Value.ResultLines);
    }

    [Theory]
    [MemberData(nameof(Lines))]
    public static void Case(int line)
    {
        Entry entry = Data.Value.ByLine[line];
        string pattern = entry.Pattern;

        int backslashC = FirstEscape(pattern, 'C');
        if (backslashC >= 0)
        {
            AssertRefusedAtTheEscape(entry, backslashC);
            return;
        }

        using PatternTree tree = Parse(entry, pattern);
        CompiledPattern search = ProgramBuilder.Build(tree);
        string whole = "\\A(?:" + pattern + ")\\z";
        using PatternTree wholeTree = Parse(entry, whole);
        CompiledPattern wholeProgram = ProgramBuilder.Build(wholeTree);

        bool hasNotWordBoundary = pattern.Contains("\\B", StringComparison.Ordinal);
        foreach (Result result in entry.Results)
        {
            if (hasNotWordBoundary && !IsSingleBytes(result.Subject))
            {
                continue;
            }

            bool wholeAnswer = ProgramHelper.IsMatch(wholeProgram, result.Subject, out _);
            bool searchAnswer = ProgramHelper.IsMatch(search, result.Subject, out _);
            for (int field = 0; field < 4; field++)
            {
                bool expected = result.Fields[field] != "-";
                bool actual = field % 2 == 0 ? wholeAnswer : searchAnswer;
                Assert.True(
                    expected == actual,
                    $"corpus line {result.Line}: pattern {Show(pattern)} on subject {Show(result.Subject)}, field {field} "
                    + $"({(field % 2 == 0 ? "whole subject" : "search")}, {(field < 2 ? "leftmost-first" : "leftmost-longest")}): "
                    + $"the file says {(expected ? "match" : "no match")}, the library says {(actual ? "match" : "no match")}");
            }
        }
    }

    // A pattern that Go accepts and the library refuses is a failure that names the line.
    private static PatternTree Parse(Entry entry, string pattern)
    {
        if (!PatternParser.TryParse(pattern, out PatternTree? tree, out RegexError? error))
        {
            Assert.Fail($"corpus line {entry.Line}: pattern {Show(pattern)} is refused at offset {error!.Offset}: {error.Reason}");
        }

        return tree!;
    }

    private static void AssertRefusedAtTheEscape(Entry entry, int offset)
    {
        RegexError error = ParseHelper.Refuse(entry.Pattern);

        Assert.Equal("invalid escape sequence", error.Reason);
        Assert.Equal(offset, error.Offset);
    }

    // The offset of the first backslash that begins the escape \<letter>, a backslash that is itself escaped being skipped; -1 for none.
    private static int FirstEscape(string pattern, char letter)
    {
        int i = 0;
        while (i < pattern.Length)
        {
            if (pattern[i] != '\\')
            {
                i++;
                continue;
            }

            if (i + 1 < pattern.Length && pattern[i + 1] == letter)
            {
                return i;
            }

            i += 2;
        }

        return -1;
    }

    private static bool IsSingleBytes(string text)
    {
        foreach (char c in text)
        {
            if (c >= 0x80)
            {
                return false;
            }
        }

        return true;
    }

    private static string Show(string text)
    {
        var shown = new StringBuilder("\"");
        foreach (char c in text)
        {
            shown.Append(c switch
            {
                '\n' => "\\n",
                '\\' => "\\\\",
                '"' => "\\\"",
                < ' ' or >= (char)0x7F => string.Create(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}"),
                _ => c.ToString(),
            });
        }

        return shown.Append('"').ToString();
    }

    private sealed record Result(int Line, string Subject, string[] Fields);

    private sealed record Entry(int Line, string Pattern, List<Result> Results);

    // The corpus as the reader of testRE2 takes it. A line that begins with # is a comment, one that begins with an upper-case letter
    // names a group; "strings" starts a list of subjects and "regexps" ends it; a quoted line is a subject in the list and a pattern
    // after it; a line that begins with "-" or a digit holds the four results of the last pattern on the next subject of the list.
    // A blank line, a line of another form, a pattern that comes before all the results of the last, a result line that has no
    // subject left, and a subject that is left at the end of the file are an error: the reader would get out of step with the file.
    private sealed class Corpus
    {
        internal static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "Regex", "Data", "re2-search.txt");

        internal List<Entry> Entries { get; } = [];

        internal Dictionary<int, Entry> ByLine { get; } = [];

        internal int Groups { get; private set; }

        internal int QuotedLines { get; private set; }

        internal int ResultLines { get; private set; }

        private readonly List<string> subjects = [];
        private bool inSubjects;
        private Entry? current;

        internal static Corpus Load()
        {
            var corpus = new Corpus();
            string[] lines = File.ReadAllText(Path, new UTF8Encoding(false, true)).Split('\n');

            // The last element after the final line feed is empty and is not a line.
            int count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
            for (int index = 0; index < count; index++)
            {
                corpus.Read(index + 1, lines[index].TrimEnd('\r'));
            }

            corpus.CheckThatTheLastPatternHasItsResults("end of file");
            return corpus;
        }

        private void Read(int number, string line)
        {
            if (line.Length == 0)
            {
                throw new InvalidDataException($"{number}: unexpected blank line");
            }

            if (line[0] == '#' || line[0] is >= 'A' and <= 'Z')
            {
                return;
            }

            switch (line)
            {
                case "strings":
                    CheckThatTheLastPatternHasItsResults(number.ToString(CultureInfo.InvariantCulture));
                    current = null;
                    Groups++;
                    subjects.Clear();
                    inSubjects = true;
                    return;
                case "regexps":
                    inSubjects = false;
                    return;
            }

            if (line[0] == '"')
            {
                ReadQuoted(number, line);
            }
            else if (line[0] == '-' || line[0] is >= '0' and <= '9')
            {
                ReadResults(number, line);
            }
            else
            {
                throw new InvalidDataException($"{number}: out of sync: {line}");
            }
        }

        private void ReadQuoted(int number, string line)
        {
            QuotedLines++;
            string text = Unquote(number, line);
            if (inSubjects)
            {
                subjects.Add(text);
                return;
            }

            CheckThatTheLastPatternHasItsResults(number.ToString(CultureInfo.InvariantCulture));
            current = new Entry(number, text, []);
            Entries.Add(current);
            ByLine.Add(number, current);
        }

        // The results of the last pattern on the next subject of the list.
        private void ReadResults(int number, string line)
        {
            ResultLines++;
            if (current is null || current.Results.Count >= subjects.Count)
            {
                throw new InvalidDataException($"{number}: out of sync: no subject remaining");
            }

            string[] fields = line.Split(';');
            if (fields.Length != 4)
            {
                throw new InvalidDataException($"{number}: {fields.Length} results, want 4");
            }

            current.Results.Add(new Result(number, subjects[current.Results.Count], fields));
        }

        private void CheckThatTheLastPatternHasItsResults(string where)
        {
            if (current is not null && current.Results.Count != subjects.Count)
            {
                throw new InvalidDataException($"{where}: out of sync: the last pattern has {current.Results.Count} of {subjects.Count}");
            }
        }

        // The quotation of Go for the escapes that the file uses: \\ \" \n \t \r \xHH up to 7F and \uHHHH. Another escape fails.
        private static string Unquote(int number, string line)
        {
            if (line.Length < 2 || line[^1] != '"')
            {
                throw new InvalidDataException($"{number}: unterminated string");
            }

            var text = new StringBuilder();
            for (int i = 1; i < line.Length - 1; i++)
            {
                char c = line[i];
                if (c != '\\')
                {
                    text.Append(c);
                    continue;
                }

                i++;
                switch (line[i])
                {
                    case '\\':
                        text.Append('\\');
                        break;
                    case '"':
                        text.Append('"');
                        break;
                    case 'n':
                        text.Append('\n');
                        break;
                    case 't':
                        text.Append('\t');
                        break;
                    case 'r':
                        text.Append('\r');
                        break;
                    case 'x':
                        int value = int.Parse(line.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        if (value > 0x7F)
                        {
                            throw new InvalidDataException($"{number}: the escape \\x{value:X2} is beyond 7F");
                        }

                        text.Append((char)value);
                        i += 2;
                        break;
                    case 'u':
                        text.Append((char)int.Parse(line.AsSpan(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default:
                        throw new InvalidDataException($"{number}: the escape \\{line[i]} is not one that the file uses");
                }
            }

            return text.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

// For an expression written as a JSON string in which some characters are escape sequences, with the segments that an application would
// give (a segment after every escape, so that each character has its place): every error of the text at a character that is not an
// escape sequence is at the same character of the document, and at an escape sequence at its backslash. The numbers are those of a fixed
// generator, so a run is the same on every runtime.
[Trait("Requirement", "F-31")]
public sealed class SourceSegmentsPropertyTests
{
    private const string Name = "e.json";
    private const string Letters = "abcdefghijklmnopqrstuvwxyz0123456789";

    // The document starts with the text before the first character of the string.
    private const string DocumentStart = "{\"expr\": \"";

    private static readonly CelEnvironment Env = TreeEnvironments.Env;

    private static readonly string[] Tokens = ["+", ")", "$", "&&"];

    [Fact]
    public void ErrorsOfRandomJsonStrings_AreAtTheCharacterOfTheDocument()
    {
        ulong state = 2026;
        int Next(int limit)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return (int)(state % (ulong)limit);
        }

        int errors = 0;
        for (int i = 0; i < 400; i++)
        {
            var text = new StringBuilder();
            var document = new StringBuilder(DocumentStart);
            var escaped = new List<bool>();
            ImmutableArray<CelSourceSegment>.Builder segments = ImmutableArray.CreateBuilder<CelSourceSegment>();
            int delta = int.MinValue;
            int tokenCount = Next(7) + 2;
            for (int t = 0; t < tokenCount; t++)
            {
                string token = Next(3) == 0 ? Tokens[Next(Tokens.Length)] : Word(Next);
                string piece = t == 0 ? token : " " + token;
                foreach (char c in piece)
                {
                    bool escape = Next(4) == 0;
                    int offset = document.Length;
                    if (offset - text.Length != delta)
                    {
                        delta = offset - text.Length;
                        segments.Add(new CelSourceSegment(text.Length, new CelSourceLocation(Name, offset, 1, offset + 1)));
                    }

                    if (escape)
                    {
                        document.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        delta = int.MinValue;
                    }
                    else
                    {
                        document.Append(c);
                    }

                    text.Append(c);
                    escaped.Add(escape);
                }
            }

            document.Append("\"}");

            CelParseResult parsed = Env.Parse(text.ToString(), new CelCompileOptions { SourceSegments = segments.ToImmutable() });

            foreach (CelError error in parsed.Errors)
            {
                if (error.SourceLocation is not { } located)
                {
                    Assert.Fail("The error has no place in the document.");
                    return;
                }

                int at = error.Location.Offset;
                if (at >= text.Length || char.IsWhiteSpace(text[at]))
                {
                    continue;
                }

                char expected = escaped[at] ? '\\' : text[at];
                Assert.True(
                    document[located.Offset] == expected,
                    "The character '" + text[at] + "' of the expression at " + at + " is '" + document[located.Offset] + "' in the document: " + document);
                errors++;
            }
        }

        Assert.True(errors > 300, "The strings have only " + errors + " errors.");
    }

    private static string Word(Func<int, int> next)
    {
        var word = new StringBuilder();
        int length = next(6) + 1;
        for (int i = 0; i < length; i++)
        {
            word.Append(Letters[next(Letters.Length)]);
        }

        return word.ToString();
    }
}

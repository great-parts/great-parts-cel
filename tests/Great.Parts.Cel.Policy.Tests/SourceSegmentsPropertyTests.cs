using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;

namespace Great.Parts.Cel.Policy.Tests;

// End to end, from the document to the error of an expression: for every error of the text of an expression that the reader took from a
// document in any style of scalar, with both line endings, the character of the document at the offset of SourceLocation is the
// character of the expression at the offset of Location, and the line and the column of SourceLocation are those of that offset.
[Trait("Requirement", "F-31")]
public sealed class SourceSegmentsPropertyTests
{
    private static readonly string[] LineEndings = ["\n", "\r\n"];

    private static readonly string[] SpaceSeparators = [" "];

    [Fact]
    public void ErrorsOfRandomExpressions_AreAtTheCharacterOfTheDocument()
    {
        var random = new TestRandom(101);
        int errors = 0;
        for (int i = 0; i < 400; i++)
        {
            string eol = LineEndings[random.Next(LineEndings.Length)];
            errors += AssertErrorsAtTheirCharacters(PlainDocument(random, eol));
            errors += AssertErrorsAtTheirCharacters(SingleQuotedDocument(random, eol));
            errors += AssertErrorsAtTheirCharacters(DoubleQuotedDocument(random, eol));
            errors += AssertErrorsAtTheirCharacters(BlockDocument(random, eol, '|'));
            errors += AssertErrorsAtTheirCharacters(BlockDocument(random, eol, '>'));
        }

        // The documents have errors, or the property would check nothing.
        Assert.True(errors > 1000, "The documents have only " + errors + " errors.");
    }

    // The number of the errors of the expression of the only match of the document that were checked.
    private static int AssertErrorsAtTheirCharacters(string document)
    {
        CelPolicy policy = CelPolicy.Parse(document, Source).GetPolicyOrThrow();
        PolicyString expression = policy.Document.Rule!.Matches[0].Output!;

        CelParseResult parsed = Env.Parse(expression.Text, new CelCompileOptions { SourceSegments = expression.Segments });

        var lines = new LineTable(document);
        int checkedErrors = 0;
        foreach (CelError error in parsed.Errors)
        {
            int at = error.Location.Offset;
            if (error.SourceLocation is not { } place)
            {
                Assert.Fail("The error has no place in the document: " + document);
                return checkedErrors;
            }

            Assert.True(place.Offset <= document.Length, "The place is outside the document: " + document);
            if (at < expression.Text.Length && !char.IsWhiteSpace(expression.Text[at]))
            {
                Assert.True(
                    document[place.Offset] == expression.Text[at],
                    "The character '" + expression.Text[at] + "' of the expression at " + at + " is '" + document[place.Offset] + "' in the document: " + document);
                checkedErrors++;
            }

            Assert.Equal(lines.Locate(place.Offset), (place.Line, place.Column));
        }

        return checkedErrors;
    }

    private static string Word(TestRandom random)
    {
        const string Letters = "abcdefghijklmnopqrstuvwxyz0123456789";
        var word = new StringBuilder();
        int length = random.Next(1, 7);
        for (int i = 0; i < length; i++)
        {
            word.Append(Letters[random.Next(Letters.Length)]);
        }

        return word.ToString();
    }

    // Words and the tokens that the expression does not accept, joined by the separators, so that the expression has errors.
    private static string Join(TestRandom random, string[] separators)
    {
        string[] tokens = ["+", ")", "$", "&&"];
        var text = new StringBuilder(Word(random));
        int count = random.Next(2, 9);
        for (int i = 0; i < count; i++)
        {
            string token = random.Next(3) == 0 ? tokens[random.Next(tokens.Length)] : Word(random);
            text.Append(separators[random.Next(separators.Length)]).Append(token);
        }

        return text.ToString();
    }

    private static string Document(string eol, string value) =>
        "rule:" + eol + "  match:" + eol + "    - output: " + value;

    private static string PlainDocument(TestRandom random, string eol) =>
        Document(eol, Join(random, [" ", " ", eol + "        ", eol + eol + "        "]));

    private static string SingleQuotedDocument(TestRandom random, string eol) =>
        Document(eol, "'" + Join(random, [" ", eol + "        ", "''", eol + eol + "        "]) + "'");

    // The escapes that stand for a character that the expression takes as it is: a tab and a backslash, and a line break escaped.
    private static string DoubleQuotedDocument(TestRandom random, string eol) =>
        Document(eol, "\"" + Join(random, [" ", eol + "        ", "\\t", "\\\\", "\\" + eol + "         ", eol + eol + "        "]) + "\"");

    private static string BlockDocument(TestRandom random, string eol, char style)
    {
        var text = new StringBuilder("rule:").Append(eol).Append("  match:").Append(eol).Append("    - output: ").Append(style).Append(eol);
        int lines = random.Next(1, 6);
        for (int i = 0; i < lines; i++)
        {
            text.Append("        ").Append(Join(random, SpaceSeparators)).Append(eol);
            if (random.Next(4) == 0)
            {
                text.Append(eol);
            }
        }

        return text.ToString();
    }
}

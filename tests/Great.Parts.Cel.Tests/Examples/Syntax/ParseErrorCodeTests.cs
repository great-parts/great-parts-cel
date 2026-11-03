using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// Parse errors of the complete grammar with their codes, messages and places: the offset in UTF-16 code units
// from 0, the line and the column from 1. The errors of a text are listed by offset.
public sealed class ParseErrorCodeTests
{
    // One line per case: the CelErrorCode member, the syntax options ("std" those of the environment, "opt"
    // with the optional syntax), the expression, the offset, the line and the column of its first error, and
    // the message; a message ending in "…" gives its start. "\n" in an expression is a line break.
    private const string Cases = """
        SyntaxError|std|-!x|1|1|2|syntax error: …
        SyntaxError|std|x +\n  -!y|7|2|4|syntax error: …
        SyntaxError|std|`a`|0|1|1|syntax error: …
        SyntaxError|std|``|0|1|1|syntax error: …
        SyntaxError|std|a.`b|2|1|3|syntax error: …
        SyntaxError|std|a.`b?c`|2|1|3|syntax error: …
        SyntaxError|std|f(1,)|4|1|5|syntax error: …
        SyntaxError|std|0x10m|4|1|5|syntax error: …
        ReservedIdentifier|std|if|0|1|1|reserved identifier 'if'
        ReservedIdentifier|std|package(1)|0|1|1|reserved identifier 'package'
        ReservedIdentifier|std|x +\n  while|6|2|3|reserved identifier 'while'
        InvalidMacroCall|std|[1].all(1, true)|7|1|8|invalid call of macro 'all': argument must be a simple name
        InvalidMacroCall|std|[1]\n.exists(__result__, true)|11|2|8|invalid call of macro 'exists': iteration variable overwrites accumulator variable
        InvalidMacroCall|std|[1].map(1, 2)|7|1|8|invalid call of macro 'map': argument must be a simple name
        InvalidMacroCall|std|[1].filter(a.b, true)|10|1|11|invalid call of macro 'filter': argument must be a simple name
        InvalidMacroCall|std|has(a)|3|1|4|invalid call of macro 'has': argument must be a field selection
        InvalidMacroCall|std|has(a[0])|3|1|4|invalid call of macro 'has': argument must be a field selection
        InvalidMacroCall|opt|has(a.?b)|3|1|4|invalid call of macro 'has': argument must be a field selection
        SyntaxNotEnabled|std|a.?b|2|1|3|'.?' is not enabled: add the extension set optional
        SyntaxNotEnabled|std|a\n. ?b|4|2|3|'.?' is not enabled: add the extension set optional
        SyntaxNotEnabled|std|a[?0]|2|1|3|'[?' is not enabled: add the extension set optional
        SyntaxNotEnabled|std|[?a, b]|1|1|2|'?' is not enabled: add the extension set optional
        SyntaxNotEnabled|std|{?1: 2}|1|1|2|'?' is not enabled: add the extension set optional
        SyntaxNotEnabled|std|a.b{?f: 1}|4|1|5|'?' is not enabled: add the extension set optional
        SyntaxNotEnabled|std|1.5m|0|1|1|decimal literal is not enabled: …
        SyntaxNotEnabled|std|x + 100m|4|1|5|decimal literal is not enabled: …
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    public static TheoryData<string, string, string, int, int, int, string> Errors
    {
        get
        {
            var data = new TheoryData<string, string, string, int, int, int, string>();
            foreach (string line in Cases.ReplaceLineEndings("\n").Split('\n'))
            {
                string[] cells = line.Split('|');
                data.Add(cells[0], cells[1], cells[2], Number(cells[3]), Number(cells[4]), Number(cells[5]), cells[6]);
            }

            return data;
        }
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    [MemberData(nameof(Errors))]
    public void InvalidText_IsReportedWithItsCodeAndPlace(string code, string syntax, string expression, int offset, int line, int column, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(message);

        CelError error = Parse(syntax, expression)[0];

        Assert.Equal(code, error.Code.ToString());
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        AssertMessage(message, error.Message);
        Assert.Equal((offset, line, column), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.Location.SourceName);
    }

    // After a syntax error the parser resumes at the next "," of the innermost open construct, or after its
    // closer; after the other codes it goes on as if the construct were valid. Every error is reported.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("std", "[1 2] + (3 4) + {5 6: 7}", "SyntaxError@3 SyntaxError@11 SyntaxError@19")]
    [InlineData("std", "f(1 2, 3 4)", "SyntaxError@4 SyntaxError@9")]
    [InlineData("std", "if + loop", "ReservedIdentifier@0 ReservedIdentifier@5")]
    [InlineData("std", "if + [1 2]", "ReservedIdentifier@0 SyntaxError@8")]
    [InlineData("std", "[1].all(1, true) || has(a)", "InvalidMacroCall@7 InvalidMacroCall@23")]
    [InlineData("std", "a.?b.?c", "SyntaxNotEnabled@2 SyntaxNotEnabled@5")]
    public void SeveralErrors_AreAllReportedInTheOrderOfTheText(string syntax, string expression, string errors)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Assert.Equal(
            errors,
            string.Join(' ', Parse(syntax, expression).Select(error => error.Code + "@" + error.Location.Offset.ToString(CultureInfo.InvariantCulture))));
    }

    private ImmutableArray<CelError> Parse(string syntax, string expression)
    {
        SyntaxOptions? options = syntax switch
        {
            "std" => null,
            "opt" => SyntaxOptions.Standard with { OptionalSyntax = true },
            _ => throw new UnreachableException(syntax),
        };
        ParseResult result = environment.Parse(expression.Replace("\\n", "\n", StringComparison.Ordinal), options);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Expression);
        Assert.NotEmpty(result.Errors);
        return result.Errors;
    }

    private static void AssertMessage(string expected, string actual)
    {
        if (expected is [.., '…'])
        {
            Assert.StartsWith(expected[..^1], actual, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expected, actual);
        }
    }

    private static int Number(string text) => int.Parse(text, CultureInfo.InvariantCulture);
}

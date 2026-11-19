using System;
using System.Threading;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// Writing a string as a CEL literal and checking a name. QuoteString gives a literal in double quotes that parses back
// into the same string; IsIdentifier is IDENT of langdef and not a reserved word.
[Trait("Requirement", "F-14")]
public sealed class CelSyntaxTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow();

    [Theory]
    [InlineData("abc", "\"abc\"")]
    [InlineData("", "\"\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("line\nnext\r\ttab", "\"line\\nnext\\r\\ttab\"")]
    [InlineData("\u0001\u007F", "\"\\x01\\x7F\"")]
    [InlineData("é😀", "\"é😀\"")]
    [InlineData("'single'", "\"'single'\"")]
    public static void QuoteString_WritesALiteral(string value, string literal)
    {
        Assert.Equal(literal, CelSyntax.QuoteString(value));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("say \"hi\"\n\\")]
    [InlineData("\u0000\u001F\u007F\u0085")]
    [InlineData("😀 and é")]
    public static void QuoteString_ParsesBackIntoTheSameString(string value)
    {
        string parsed = Environment.Compile<string>(CelSyntax.QuoteString(value)).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal(value, parsed);
    }

    [Fact]
    public void QuoteString_RefusesALoneSurrogate()
    {
        Assert.Throws<ArgumentException>("value", () => CelSyntax.QuoteString("a\uD800b"));
        Assert.Throws<ArgumentException>("value", () => CelSyntax.QuoteString("\uDC00"));
        Assert.Throws<ArgumentNullException>("value", () => CelSyntax.QuoteString(null!));
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData("_total2", true)]
    [InlineData("Total", true)]
    [InlineData("2total", false)]
    [InlineData("", false)]
    [InlineData("a.b", false)]
    [InlineData("a-b", false)]
    [InlineData("é", false)]
    [InlineData("in", false)]
    [InlineData("true", false)]
    [InlineData("null", false)]
    [InlineData("package", false)]
    [InlineData("loop", false)]
    [InlineData("namespace", false)]
    [InlineData("int", true)]
    public static void IsIdentifier_IsIdentAndNotReserved(string value, bool expected)
    {
        Assert.Equal(expected, CelSyntax.IsIdentifier(value));
    }
}

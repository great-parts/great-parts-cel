using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// Parse errors come as a result with their place: the offset in UTF-16 code units from 0, and the line
// and the column from 1. Lines are separated by "\n", "\r\n" and "\r". Only the first error is
// asserted: later parts of the parser may report more errors after it.
public sealed class ParseErrorTests
{
    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("9223372036854775808", "int", 0, 1, 1)]
    [InlineData("-9223372036854775809", "int", 0, 1, 1)]
    [InlineData("18446744073709551616u", "uint", 0, 1, 1)]
    [InlineData("1 +\n  9223372036854775808", "int", 6, 2, 3)]
    [InlineData("1 +\r\n0x10000000000000000", "int", 5, 2, 1)]
    [InlineData("[1,\r'\\q']", "string", 4, 2, 1)]
    [InlineData("'\U0001F600' + 99999999999999999999", "int", 7, 1, 8)]
    public void InvalidLiteral_IsReportedAtTheLiteral(string expression, string kind, int offset, int line, int column)
    {
        CelError error = FirstError(expression);

        Assert.Equal(CelErrorCode.InvalidLiteral, error.Code);
        Assert.StartsWith("invalid " + kind + " literal: ", error.Message, StringComparison.Ordinal);
        AssertPlace(error, offset, line, column);
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("1 +", 3, 1, 4)]
    [InlineData("(1 + 2", 6, 1, 7)]
    [InlineData("1 2", 2, 1, 3)]
    [InlineData("1 # 2", 2, 1, 3)]
    [InlineData("'abc", 0, 1, 1)]
    [InlineData("[1,\n2", 5, 2, 2)]
    public void SyntaxError_IsReportedWhereParsingStopped(string expression, int offset, int line, int column)
    {
        CelError error = FirstError(expression);

        Assert.Equal(CelErrorCode.SyntaxError, error.Code);
        Assert.StartsWith("syntax error: ", error.Message, StringComparison.Ordinal);
        AssertPlace(error, offset, line, column);
    }

    private CelError FirstError(string expression)
    {
        ParseResult result = environment.Parse(expression);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Expression);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
        return result.Errors[0];
    }

    private static void AssertPlace(CelError error, int offset, int line, int column)
    {
        Assert.Equal(offset, error.Location.Offset);
        Assert.Equal(line, error.Location.Line);
        Assert.Equal(column, error.Location.Column);
        Assert.Null(error.Location.SourceName);
        Assert.Null(error.SourceLocation);
    }
}

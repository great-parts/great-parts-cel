using System;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The compilation boundary of parsing (docs/design/facade.md, "Compilation boundary"): an unexpected exception during
// parsing becomes InternalError of the stage Parse, after the errors found before it; OutOfMemoryException passes.
public sealed class ParseBoundaryTests
{
    private const string InternalErrorMessage = "internal error during parsing; the original exception is in CelError.Exception";

    [Fact]
    public static void ExceptionBeforeTheFirstToken_IsInternalErrorAtTheStart()
    {
        var exception = new InvalidOperationException("defect");

        ParseResult result = CelEnvironment.ParseWithinBoundary(new Parser("x\n+ 1", SyntaxOptions.Standard), _ => throw exception);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.InternalError, CelErrorStage.Parse, InternalErrorMessage, 0, 1, 1, (CelSourceLocation?)null),
            (error.Code, error.Stage, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column, error.SourceLocation));
        Assert.Same(exception, error.Exception);
    }

    // The errors found before the exception are kept, and InternalError comes last, at the token the parser was at:
    // here the end of the text.
    [Fact]
    public static void ExceptionAfterErrors_IsInternalErrorAfterThemAtTheCurrentToken()
    {
        var exception = new FormatException("defect");

        ParseResult result = CelEnvironment.ParseWithinBoundary(
            new Parser("[1 2,\n if", SyntaxOptions.Standard),
            parser =>
            {
                parser.Run();
                throw exception;
            });

        Assert.Equal(
            "SyntaxError@3 ReservedIdentifier@7 SyntaxError@9 InternalError@9",
            string.Join(' ', result.Errors.Select(error => error.Code + "@" + error.Location.Offset.ToString(CultureInfo.InvariantCulture))));
        CelError error = result.Errors[^1];
        Assert.Equal((CelErrorStage.Parse, 2, 4), (error.Stage, error.Location.Line, error.Location.Column));
        Assert.Same(exception, error.Exception);
    }

    // InsufficientMemoryException is an OutOfMemoryException that code can throw.
    [Fact]
    public static void OutOfMemoryException_IsNotCaught()
    {
        Assert.Throws<InsufficientMemoryException>(
            () => CelEnvironment.ParseWithinBoundary(new Parser("x", SyntaxOptions.Standard), _ => throw new InsufficientMemoryException()));
    }

    // Without an exception the boundary gives the result of the parser as it is.
    [Fact]
    public static void ParseWithoutException_IsTheResultOfTheParser()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        ParseResult result = environment.Parse("[1 2] + if");

        Assert.Equal(
            new[] { CelErrorCode.SyntaxError, CelErrorCode.ReservedIdentifier },
            result.Errors.Select(error => error.Code));
    }
}

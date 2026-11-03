using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// An expression stored in a document — here at the offset 120, line 7, column 15 of rules/discount.yaml —
// is compiled with that start in the compile options. Every error of the compilation and of each evaluation
// of its program then has, besides its place in the expression, its place in the document: the offset of the
// start plus the offset; the line of the start plus the line minus 1; on the first line of the expression the
// column of the start plus the column minus 1, on the other lines the column itself.
// Evaluations pass CancellationToken.None: these examples do not cancel.
public sealed class SourceLocationTests
{
    private const string Document = "rules/discount.yaml";

    private static readonly CelCompileOptions InDocument = new() { SourceLocation = new CelSourceLocation(Document, 120, 7, 15) };

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("x 1", CelErrorCode.SyntaxError, CelErrorStage.Parse, 2, 1, 3, 122, 7, 17)]
    [InlineData("x +\n  (1", CelErrorCode.SyntaxError, CelErrorStage.Parse, 8, 2, 5, 128, 8, 5)]
    [InlineData("x +\n  if", CelErrorCode.ReservedIdentifier, CelErrorStage.Parse, 6, 2, 3, 126, 8, 3)]
    [InlineData("y +\n  x", CelErrorCode.UndeclaredReference, CelErrorStage.Check, 0, 1, 1, 120, 7, 15)]
    [InlineData("x +\n  y", CelErrorCode.UndeclaredReference, CelErrorStage.Check, 6, 2, 3, 126, 8, 3)]
    public void CompilationError_HasItsPlaceInTheDocument(
        string expression, CelErrorCode code, CelErrorStage stage, int offset, int line, int column, int documentOffset, int documentLine, int documentColumn)
    {
        CelError plain = environment.Compile<long>(expression).Errors[0];
        CelError error = environment.Compile<long>(expression, InDocument).Errors[0];

        Assert.Equal((code, stage), (error.Code, error.Stage));
        Assert.Equal(plain.Message, error.Message);
        AssertPlaces(error, (offset, line, column), (documentOffset, documentLine, documentColumn));
        Assert.Null(plain.SourceLocation);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void EvaluationError_HasItsPlaceInTheDocument()
    {
        CelProgram<long> program = environment.Compile<long>("x / 0", InDocument).GetProgramOrThrow();

        CelError error = Assert.Single(program.Evaluate(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.DivisionByZero, CelErrorStage.Evaluate), (error.Code, error.Stage));
        AssertPlaces(error, (2, 1, 3), (122, 7, 17));
    }

    // Without a start the errors have no place in a document, as before.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void OptionsWithoutStart_GiveNoPlaceInADocument()
    {
        var options = new CelCompileOptions();

        CelError error = environment.Compile<long>("x 1", options).Errors[0];

        Assert.Null(options.SourceLocation);
        Assert.Null(options.ExpectedType);
        Assert.Equal((2, 1, 3), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.SourceLocation);
    }

    // A start must name its document: a position without a source name is a position in an expression.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void StartWithoutSourceName_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new CelCompileOptions { SourceLocation = default(CelSourceLocation) });
    }

    private static void AssertPlaces(CelError error, (int Offset, int Line, int Column) inExpression, (int Offset, int Line, int Column) inDocument)
    {
        Assert.Equal(inExpression, (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.Location.SourceName);
        Assert.Equal<CelSourceLocation?>(new CelSourceLocation(Document, inDocument.Offset, inDocument.Line, inDocument.Column), error.SourceLocation);
    }
}

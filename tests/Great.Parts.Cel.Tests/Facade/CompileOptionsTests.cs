using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// CelCompileOptions and the place of the errors of an expression in its source document (docs/design/facade.md,
// "Source location of an expression").
public sealed class CompileOptionsTests
{
    private const string Document = "config/policy.json";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("limit")
        .AddVariable<string>("name")
        .Build()
        .GetEnvironmentOrThrow();

    private static CelCompileOptions At(int offset, int line, int column) =>
        new() { SourceLocation = new CelSourceLocation(Document, offset, line, column) };

    [Fact]
    public static void NewOptions_HaveNoStartAndNoExpectedType()
    {
        var options = new CelCompileOptions();

        Assert.Null(options.SourceLocation);
        Assert.Null(options.ExpectedType);
    }

    [Fact]
    public static void Start_IsKept()
    {
        Assert.Equal(new CelSourceLocation(Document, 3, 2, 1), At(3, 2, 1).SourceLocation);
        Assert.Null(new CelCompileOptions { SourceLocation = null }.SourceLocation);
    }

    [Fact]
    public static void StartWithoutSourceName_ThrowsArgumentException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CelCompileOptions { SourceLocation = default(CelSourceLocation) });

        Assert.Equal("value", exception.ParamName);
    }


    // Every error of the stage is mapped: on the first line of the expression the columns add up, on the other lines
    // the column is that in the expression; offsets add up, lines add up less one.
    [Fact]
    public static void ParseErrors_HaveTheirPlaceInTheDocument()
    {
        CelCompileResult<long> result = Environment.Compile<long>("[1 2,\r\n  3 4]", At(40, 5, 9));

        Assert.Equal(
            new[] { (3, 1, 4, 43, 5, 12), (11, 2, 5, 51, 6, 5) },
            result.Errors.Select(error => Places(error)));
        Assert.All(result.Errors, error => Assert.Equal(Document, error.SourceLocation?.SourceName));
    }

    [Fact]
    public static void CheckErrors_HaveTheirPlaceInTheDocument()
    {
        CelCompileResult<bool> result = Environment.Compile<bool>("size +\n  count", At(100, 10, 20));

        Assert.Equal(
            new[] { (CelErrorStage.Check, 0, 1, 1, 100, 10, 20), (CelErrorStage.Check, 9, 2, 3, 109, 11, 3) },
            result.Errors.Select(error => (error.Stage, error.Location.Offset, error.Location.Line, error.Location.Column, error.SourceLocation!.Value.Offset, error.SourceLocation.Value.Line, error.SourceLocation.Value.Column)));
    }

    // The mapped error is a new error with the same code, stage, message and place in the expression.
    [Fact]
    public static void MappedError_KeepsEverythingElse()
    {
        CelError plain = Environment.Compile<long>("limit +").Errors[0];

        CelError mapped = Environment.Compile<long>("limit +", At(10, 2, 3)).Errors[0];

        Assert.Equal(
            (plain.Code, plain.Stage, plain.Message, plain.Location, plain.Exception),
            (mapped.Code, mapped.Stage, mapped.Message, mapped.Location, mapped.Exception));
        Assert.Equal(new CelSourceLocation(Document, 17, 2, 10), mapped.SourceLocation);
        Assert.Null(plain.SourceLocation);
    }

    // The program keeps the start: its evaluation errors, a failed conversion of the result included, are mapped.
    [Fact]
    public static void EvaluationErrors_HaveTheirPlaceInTheDocument()
    {
        CelProgram<long> overflow = Environment.Compile<long>("0 +\n  limit + 1", At(7, 3, 4)).GetProgramOrThrow();
        CelProgram<long> conversion = Environment.Compile<long>("name", new CelCompileOptions { ExpectedType = CelType.Dyn, SourceLocation = At(7, 3, 4).SourceLocation }).GetProgramOrThrow();
        var bindings = new CelBindings().Add("limit", long.MaxValue).Add("name", "n");

        CelError overflowError = Assert.Single(overflow.Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);
        CelError conversionError = Assert.Single(conversion.Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Equal(
            (CelErrorCode.Overflow, 12, 2, 9, new CelSourceLocation(Document, 19, 4, 9)),
            (overflowError.Code, overflowError.Location.Offset, overflowError.Location.Line, overflowError.Location.Column, overflowError.SourceLocation));
        Assert.Equal(
            (CelErrorCode.ResultConversion, new CelSourceLocation(Document, 7, 3, 4)),
            (conversionError.Code, conversionError.SourceLocation));
    }

    // Without a start in the options the errors have no place in a document.
    [Fact]
    public static void OptionsWithoutStart_GiveErrorsWithoutPlaceInADocument()
    {
        CelCompileResult<long> compiled = Environment.Compile<long>("limit + 1 +", new CelCompileOptions());
        CelProgram<long> program = Environment.Compile<long>("limit + 1", new CelCompileOptions()).GetProgramOrThrow();

        Assert.Null(Assert.Single(compiled.Errors).SourceLocation);
        Assert.Null(Assert.Single(program.Evaluate(new CelBindings().Add("limit", long.MaxValue), cancellationToken: CancellationToken.None).Errors).SourceLocation);
    }

    // A start near the largest offset, line or column gives positions that stop at int.MaxValue instead of
    // overflowing.
    [Fact]
    public static void StartNearTheLargestNumbers_GivesPositionsThatStopThere()
    {
        CelError error = Environment.Compile<long>("limit +", At(int.MaxValue - 1, int.MaxValue, int.MaxValue - 2)).Errors[0];

        Assert.Equal(new CelSourceLocation(Document, int.MaxValue, int.MaxValue, int.MaxValue), error.SourceLocation);
    }

    private static (int, int, int, int, int, int) Places(CelError error) =>
        (error.Location.Offset, error.Location.Line, error.Location.Column,
            error.SourceLocation!.Value.Offset, error.SourceLocation.Value.Line, error.SourceLocation.Value.Column);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Diagnostics;

// CelError.Create and the public constructor of CelException: the errors of the documents of extension packages are the errors of the
// library in form: a code, a stage, a message, a place in the document that is the same for Location, the start of the Span and
// SourceLocation, a length, a subject and two types, and no exception of application code; the exception of several errors keeps all of
// them and the exception of the first that has one.
[Trait("Requirement", "F-31")]
public sealed class CreateErrorTests
{
    private static readonly CelSourceLocation Place = new("p.yaml", 10, 2, 3);

    [Fact]
    public void Create_KeepsEveryPartAndPutsThePlaceInThreeProperties()
    {
        CelError error = CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, "unsupported policy tag: foo", Place, 3, "foo");

        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Equal("unsupported policy tag: foo", error.Message);
        Assert.Equal(Place, error.Location);
        Assert.Equal(Place, error.SourceLocation);
        Assert.Equal(Place, error.Span.Start);
        Assert.Equal(3, error.Span.Length);
        Assert.Equal("foo", error.Subject);
        Assert.Null(error.Exception);
        Assert.Null(error.ExpectedType);
        Assert.Null(error.ActualType);
        Assert.StartsWith("InvalidPolicy at p.yaml:2:3:", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Create_KeepsTheTypes()
    {
        CelError error = CelError.Create(CelErrorCode.IncompatibleOutputTypes, CelErrorStage.Check, "m", Place, 1, null, CelType.Bool, CelType.String);

        Assert.Equal(CelType.Bool, error.ExpectedType);
        Assert.Equal(CelType.String, error.ActualType);
        Assert.Null(error.Subject);
    }

    [Fact]
    public void Create_WithoutAPlace_HasNoPosition()
    {
        CelError error = CelError.Create(CelErrorCode.MissingExtensionDependency, CelErrorStage.Check, "m", subject: "optional");

        Assert.Equal(default, error.Location);
        Assert.Null(error.SourceLocation);
        Assert.Equal(0, error.Span.Length);
        Assert.Equal("MissingExtensionDependency: m", error.ToString());
    }

    [Fact]
    public void Create_RejectsAnEmptyMessageAnUnknownCodeAndStageAndABadPlace()
    {
        Assert.Throws<ArgumentException>(() => CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, string.Empty, Place));
        Assert.Throws<ArgumentNullException>(() => CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, null!, Place));
        Assert.Throws<ArgumentException>(() => CelError.Create((CelErrorCode)12345, CelErrorStage.Parse, "m", Place));
        Assert.Throws<ArgumentException>(() => CelError.Create(CelErrorCode.InvalidPolicy, (CelErrorStage)99, "m", Place));
        Assert.Throws<ArgumentException>(() => CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, "m", default(CelSourceLocation)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, "m", length: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, "m", Place, -1));
    }

    [Fact]
    public void Exception_KeepsTheErrorsAndTheExceptionOfTheFirstThatHasOne()
    {
        InvalidOperationException cause = new("boom");
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddFunction<long, long>("boom", _ => throw cause)
            .Build()
            .GetEnvironmentOrThrow();
        CelError first = CelError.Create(CelErrorCode.InvalidPolicy, CelErrorStage.Parse, "first", Place, 1);
        CelError second = environment.Compile<long>("boom(1)").GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors[0];

        var exception = new CelException([first, second]);

        CelError[] expected = [first, second];
        Assert.Equal(expected, exception.Errors.ToArray());
        Assert.Same(cause, exception.InnerException);
        Assert.Contains("InvalidPolicy at line 2, column 3: first", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ApplicationException", exception.Message, StringComparison.Ordinal);
        Assert.Null(new CelException([first]).InnerException);
    }

    [Fact]
    public void Exception_CopiesTheErrorsOfTheCaller()
    {
        CelError error = CelError.Create(CelErrorCode.InvalidYaml, CelErrorStage.Parse, "m", Place);
        List<CelError> errors = [error];

        var exception = new CelException(errors);
        errors.Clear();

        Assert.Single(exception.Errors);
    }

    [Fact]
    public void Exception_RejectsNoErrorsAndANullError()
    {
        Assert.Throws<ArgumentException>(() => new CelException([]));
        Assert.Throws<ArgumentNullException>(() => new CelException(null!));
        Assert.Throws<ArgumentNullException>(() => new CelException([null!]));
    }
}

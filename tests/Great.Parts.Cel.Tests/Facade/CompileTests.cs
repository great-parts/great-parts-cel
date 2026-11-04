using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// CelEnvironment.Compile<T> and CelCompileResult<T>: parse, then check, then plan; each stage runs only
// when the previous one found no errors (docs/design/facade.md, "Stages").
public sealed class CompileTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("count")
        .Build()
        .GetEnvironmentOrThrow();

    // The text also has undeclared names, which checking would report: a text with parse errors is not
    // checked.
    [Fact]
    public void TextWithParseErrors_HasOnlyTheParseErrors()
    {
        CelCompileResult<long> compiled = Environment.Compile<long>("first + second +");

        Assert.False(compiled.IsSuccess);
        Assert.NotEmpty(compiled.Errors);
        Assert.All(compiled.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
    }

    [Fact]
    public void TextWithCheckErrors_HasEveryCheckErrorAndNoProgram()
    {
        CelCompileResult<long> compiled = Environment.Compile<long>("first + count + second");

        Assert.False(compiled.IsSuccess);
        Assert.Equal(
            new[] { (CelErrorCode.UndeclaredReference, CelErrorStage.Check, 0), (CelErrorCode.UndeclaredReference, CelErrorStage.Check, 16) },
            compiled.Errors.Select(error => (error.Code, error.Stage, error.Location.Offset)));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => compiled.Program);
        Assert.Equal("The program was not compiled: the result has compilation errors.", exception.Message);
        Assert.Equal(compiled.Errors, Assert.Throws<CelException>(compiled.GetProgramOrThrow).Errors);
    }

    [Fact]
    public void CompiledText_HoldsTheProgramWithTheTypeOfTheExpression()
    {
        CelCompileResult<CelValue> compiled = Environment.Compile<CelValue>("[count, -count]");

        Assert.True(compiled.IsSuccess);
        Assert.Empty(compiled.Errors);
        Assert.Same(compiled.Program, compiled.GetProgramOrThrow());
        Assert.Equal(CelType.ListOf(CelType.Int), compiled.Program.ResultType);
    }

    // The expected type that T implies is checked: a type that T does not hold is a compilation error.
    [Fact]
    public void TypeThatTDoesNotHold_IsExpectedTypeMismatch()
    {
        CelCompileResult<string> compiled = Environment.Compile<string>("count");

        Assert.False(compiled.IsSuccess);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(compiled.Errors).Code);
    }

    [Fact]
    public void Compile_OfNull_ThrowsArgumentNullException()
    {
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Environment.Compile<long>(null!)).ParamName);
    }

    // T outside the tables is a violated condition of the call, before the text is read; T of time, optional or decimal
    // is not implemented yet and not ignored.
    [Fact]
    public void Compile_ToATypeOutsideTheTables_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Environment.Compile<short>("count +"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<Guid>("count"));
        Assert.Throws<NotImplementedException>(() => Environment.Compile<DateTimeOffset>("count"));
        Assert.True(Environment.Compile<long?>("count").IsSuccess);
    }
}

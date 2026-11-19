using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The rows of the closed list of exceptions that the examples do not reach: the exception of every failed result is made in one
// place, so its inner exception is that of the first error that has one; the rows that stay as they were.
public sealed class ExceptionRowsTests
{
    private static readonly InvalidOperationException FirstCause = new("first");
    private static readonly ArgumentException SecondCause = new("second");

    [Fact]
    public void BuildResult_InnerException_IsTheExceptionOfTheFirstErrorThatHasOne()
    {
        ImmutableArray<CelError> errors = [Error("no exception", null), Error("first", FirstCause), Error("second", SecondCause)];
        CelBuildResult result = new(errors);

        CelException fromMethod = Assert.Throws<CelException>(result.GetEnvironmentOrThrow);
        CelException fromProperty = Assert.Throws<CelException>(() => result.Environment);

        Assert.Same(FirstCause, fromMethod.InnerException);
        Assert.Same(FirstCause, fromProperty.InnerException);
        Assert.Equal(errors, fromMethod.Errors);
        Assert.Same(SecondCause, fromMethod.Errors[2].Exception);
    }

    [Fact]
    public void CompileResult_InnerException_IsTheExceptionOfTheFirstErrorThatHasOne()
    {
        CelCompileResult<long> result = new([Error("no exception", null), Error("second", SecondCause)]);

        Assert.Same(SecondCause, Assert.Throws<CelException>(result.GetProgramOrThrow).InnerException);
        Assert.Same(SecondCause, Assert.Throws<CelException>(() => result.Program).InnerException);
    }

    [Fact]
    public void EvalResult_InnerException_IsTheExceptionOfTheFirstErrorThatHasOne()
    {
        CelEvalResult<string> result = new([Error("first", FirstCause)]);

        Assert.Same(FirstCause, Assert.Throws<CelException>(() => result.Value).InnerException);
        Assert.Same(FirstCause, Assert.Throws<CelException>(result.GetValueOrThrow).InnerException);
    }

    [Fact]
    public void ErrorsWithoutAnException_HaveNoInnerException_AndTheMessageListsEveryError()
    {
        CelBuildResult result = new([Error("one", null), Error("two", null)]);

        CelException exception = Assert.Throws<CelException>(result.GetEnvironmentOrThrow);

        Assert.Null(exception.InnerException);
        Assert.Contains("one", exception.Message, StringComparison.Ordinal);
        Assert.Contains("two", exception.Message, StringComparison.Ordinal);
    }

    // A default structure is not a result of a call, whatever its value type: a reference type too.
    [Fact]
    public void DefaultResultOfAReferenceType_IsNotSuccessfulAndItsReadsAreInvalid()
    {
        CelEvalResult<string> result = default;

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors.IsEmpty);
        Assert.False(result.Errors.IsDefault);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Throws<InvalidOperationException>(result.GetValueOrThrow);
        Assert.Throws<InvalidOperationException>(() => result.Cost);
    }

    // The result of a call keeps its errors: empty and not default on success, so reading it never throws InvalidOperationException.
    [Fact]
    public void SuccessfulResult_HasEmptyNotDefaultErrors_AndNoCostWithoutALimit()
    {
        CelEvalResult<long> result = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow()
            .Compile<long>("1 + 2").GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Errors.IsDefault);
        Assert.True(result.Errors.IsEmpty);
        Assert.Equal(3L, result.Value);
        Assert.Null(result.Cost);
    }

    // Rows of the list that keep their exception: the state of an object, the contract of a .NET interface.
    [Fact]
    public void DurationThatDoesNotFitALong_TotalNanoseconds_IsInvalidOperationException()
    {
        var length = new CelDuration(seconds: 315_576_000_000, nanoseconds: 999_999_999);

        Assert.Throws<InvalidOperationException>(() => length.TotalNanoseconds);
    }

    [Fact]
    public void EntryEnumeratorOfAJsonObject_Reset_IsNotSupportedException()
    {
        using JsonDocument document = JsonDocument.Parse("""{"a": 1}""");
        using IEnumerator<KeyValuePair<CelValue, CelValue>> entries = JsonRules.Entries(document.RootElement, null, readValues: true);

        Assert.Throws<NotSupportedException>(entries.Reset);
    }

    private static CelError Error(string message, Exception? cause) =>
        new(CelErrorCode.InvalidConfiguration, CelErrorStage.Build, message, default, null, cause);
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// CelProgram<T>.Evaluate, CelEvalResult<T> and the internal entry point EvaluateWithoutTypeCheck
// (docs/design/facade.md, "Errors", "Result conversion", "Internal entry points").
public sealed class EvaluateTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("count")
        .AddVariable("anything", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // Each evaluation has its own frame, so evaluations on different bindings at the same time do not
    // see each other's values.
    [Fact]
    public void OneProgram_EvaluatedFromSeveralThreadsAtOnce_GivesTheResultOfEachBinding()
    {
        CelProgram<long> program = Environment.Compile<long>("count + count / 2 + -count").GetProgramOrThrow();
        long[] results = new long[2000];

        Parallel.For(0, results.Length, i => results[i] = Run(program, new CelBindings().Add("count", (long)i * 4)).GetValueOrThrow());

        Assert.Equal(Enumerable.Range(0, results.Length).Select(i => (long)i * 2), results);
    }

    [Fact]
    public void ValueThatIsOutOfTheRangeOfT_IsResultConversionAtTheRoot()
    {
        CelProgram<int> program = Environment.Compile<int>("\n  count + 1").GetProgramOrThrow();

        CelEvalResult<int> result = Run(program, new CelBindings().Add("count", (long)int.MaxValue));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal((9, 2, 9), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Equal("result of type 'int' cannot be converted to .NET type 'System.Int32'", error.Message);
        Assert.Null(error.SourceLocation);
    }

    [Fact]
    public void ValueOfATypeWithoutARowForT_IsResultConversionAtTheRoot()
    {
        CelProgram<bool> program = Environment.Compile<bool>("anything").GetProgramOrThrow();

        CelEvalResult<bool> result = Run(program, new CelBindings().Add("anything", 1.5));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.ResultConversion, 0), (error.Code, error.Location.Offset));
        Assert.Equal("result of type 'double' cannot be converted to .NET type 'System.Boolean'", error.Message);
    }

    [Fact]
    public void ErrorValueOfTheRoot_IsTheOneErrorOfTheResult()
    {
        CelEvalResult<long> result = Run(Environment.Compile<long>("count / 0").GetProgramOrThrow(), new CelBindings().Add("count", 7L));

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.DivisionByZero, CelErrorStage.Evaluate, 6), (error.Code, error.Stage, error.Location.Offset));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal("The evaluation gave no value: the result has errors.", exception.Message);
        Assert.Equal(result.Errors, Assert.Throws<CelException>(() => result.GetValueOrThrow()).Errors);
    }

    [Fact]
    public void VariableWithoutAValue_IsUnboundWhenTheExpressionReadsIt()
    {
        CelError error = Assert.Single(Run(Environment.Compile<long>("1 + count").GetProgramOrThrow(), new CelBindings()).Errors);

        Assert.Equal((CelErrorCode.Unbound, 4), (error.Code, error.Location.Offset));
    }

    [Fact]
    public void SuccessfulEvaluation_HoldsTheValueAndNoErrors()
    {
        CelEvalResult<string> text = Run(Environment.Compile<string>("anything").GetProgramOrThrow(), new CelBindings().Add("anything", "value"));
        CelEvalResult<string> nothing = Run(Environment.Compile<string>("null").GetProgramOrThrow(), new CelBindings());

        Assert.True(text.IsSuccess);
        Assert.Empty(text.Errors);
        Assert.Equal("value", text.Value);
        Assert.Equal("value", text.GetValueOrThrow());
        Assert.True(nothing.IsSuccess);
        Assert.Null(nothing.Value);
    }

    [Fact]
    public void Evaluate_OfNull_ThrowsArgumentNullException()
    {
        CelProgram<long> program = Environment.Compile<long>("count").GetProgramOrThrow();

        Assert.Equal("bindings", Assert.Throws<ArgumentNullException>(() => Run(program, null!)).ParamName);
    }

    // Cancellation is not implemented yet and is not ignored.
    [Fact]
    public void Evaluate_WithATokenThatCanBeCanceled_ThrowsNotImplementedException()
    {
        CelProgram<long> program = Environment.Compile<long>("count").GetProgramOrThrow();
        using var source = new CancellationTokenSource();

        Assert.Throws<NotImplementedException>(() => program.Evaluate(new CelBindings().Add("count", 1L), null, source.Token));
    }

    // Without type checking a name is read from the bindings, declared or not, and the value is returned
    // as it is.
    [Fact]
    public void EvaluateWithoutTypeCheck_ReadsUndeclaredNamesAndReturnsTheValueAsItIs()
    {
        ParseResult parsed = Environment.Parse("[other, count]");
        Assert.True(parsed.IsSuccess);

        CelEvalResult<CelValue> result = Environment.EvaluateWithoutTypeCheck(
            parsed.Expression,
            new CelBindings().Add("other", "text").Add("count", 3L));

        Assert.True(result.Value.TryGetList(out CelList? list));
        Assert.Equal(new[] { CelValueKind.String, CelValueKind.Int }, list.Select(element => element.Kind));
    }

    [Fact]
    public void EvaluateWithoutTypeCheck_OfAnErrorValue_HasTheOneError()
    {
        ParseResult parsed = Environment.Parse("count + other");
        Assert.True(parsed.IsSuccess);

        CelEvalResult<CelValue> result = Environment.EvaluateWithoutTypeCheck(parsed.Expression, new CelBindings().Add("count", 1L));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.Unbound, CelErrorStage.Evaluate, 8), (error.Code, error.Stage, error.Location.Offset));
    }

    [Fact]
    public void InternalEntryPoints_OfNull_ThrowArgumentNullException()
    {
        ParseResult parsed = Environment.Parse("count");
        Assert.True(parsed.IsSuccess);

        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Environment.Parse(null!)).ParamName);
        Assert.Equal("parsed", Assert.Throws<ArgumentNullException>(() => Environment.Check(null!)).ParamName);
        Assert.Equal(
            "parsed",
            Assert.Throws<ArgumentNullException>(() => Environment.EvaluateWithoutTypeCheck(null!, new CelBindings())).ParamName);
        Assert.Equal(
            "bindings",
            Assert.Throws<ArgumentNullException>(() => Environment.EvaluateWithoutTypeCheck(parsed.Expression, null!)).ParamName);
    }

    // Evaluations pass CancellationToken.None: a token that can be canceled is not implemented yet.
    private static CelEvalResult<T> Run<T>(CelProgram<T> program, CelBindings bindings) =>
        program.Evaluate(bindings, cancellationToken: CancellationToken.None);
}

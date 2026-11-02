using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The error codes of the cel-spec cases of the file basic that disable type checking. The suite runner
// only tests that such a case gives an error; these examples fix which error the internal entry point
// for evaluation without type checking gives, and where.
[Trait("Requirement", "R-1")]
public sealed class WithoutTypeCheckTests
{
    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    // Case basic/variables/self_eval_unbound_lookup: a name without a value in the bindings, declared
    // or not, is unbound at its identifier.
    [Fact]
    public void UnboundName_IsUnbound()
    {
        CelError error = SingleError("x");

        Assert.Equal(CelErrorCode.Unbound, error.Code);
        Assert.Equal("no value is bound to variable 'x'", error.Message);
        Assert.Equal((0, 1, 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // Case basic/functions/unbound: a function without overloads of its name and argument count has no
    // overload that fits; the error is at the call, which is the "(" after the name.
    [Fact]
    public void UnknownFunction_IsEvalNoMatchingOverload()
    {
        CelError error = SingleError("f_unknown(17)");

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
        Assert.Equal((9, 1, 10), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private CelError SingleError(string expression)
    {
        ParseResult parsed = environment.Parse(expression);
        Assert.True(parsed.IsSuccess);

        CelEvalResult<CelValue> result = environment.EvaluateWithoutTypeCheck(parsed.Expression, new CelBindings());

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Null(error.SourceLocation);
        return error;
    }
}

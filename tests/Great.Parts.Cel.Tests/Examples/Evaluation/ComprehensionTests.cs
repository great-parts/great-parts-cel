using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The standard macros at evaluation. A list gives its elements in order, a map its keys. There is no
// early exit on an error: all and exists absorb an error by the rules of && and ||, when another element
// decides the result; exists_one, map and filter keep it. The outcome of each expression is that of
// cel-go v0.32.0.
public sealed class ComprehensionTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("ds", CelType.Dyn)
        .AddVariable("dn", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("d", 1L)
        .Add("ds", "abc")
        .Add("dn", CelValue.Null);

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("[1, 2, 3].all(x, x > 0)")]
    [InlineData("[].all(x, false)")]
    [InlineData("[1, 2].exists(x, x == 2)")]
    [InlineData("![1, 2, 3].exists_one(x, x > 1)")]
    [InlineData("[1, 2, 3].exists_one(x, x == 2)")]
    [InlineData("[1, 2].map(x, x * 2) == [2, 4]")]
    [InlineData("[1, 2, 3].map(x, x > 1, x * 10) == [20, 30]")]
    [InlineData("[1, 2, 3].filter(x, x != 2) == [1, 3]")]
    [InlineData("{'a': 1, 'b': 2}.all(k, k != 'c')")]
    [InlineData("{'a': 1}.map(k, k + '!') == ['a!']")]
    [InlineData("[0, -1].all(x, 1 / x > 0) == false")]
    [InlineData("[0, 1].exists(x, 1 / x > 0)")]
    public void Macro_GivesItsValue(string expression)
    {
        Assert.True(environment.Compile<bool>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The error of the element 0 is the value: no other element decides all or exists, and exists_one, map
    // and filter do not absorb errors. The place is the "/" of the predicate or the transform.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("[0, 1].all(x, 1 / x > 0)", 16)]
    [InlineData("[0].exists(x, 1 / x > 0)", 16)]
    [InlineData("[0, 1].exists_one(x, 1 / x > 0)", 23)]
    [InlineData("[0, 1].map(x, 1 / x)", 16)]
    [InlineData("[0, 1].filter(x, 1 / x > 0)", 19)]
    public void ErrorThatNoElementAbsorbs_IsTheValueOfTheMacro(string expression, int offset)
    {
        CelError error = SingleError(expression);

        Assert.Equal((CelErrorCode.DivisionByZero, "division by zero"), (error.Code, error.Message));
        Assert.Equal(offset, error.Location.Offset);
    }

    // A range of the static type dyn that holds neither a list nor a map: the error is at the range, here
    // the variable at offset 0. A string is not a range either. cel-go v0.32.0 gives an error, "expected
    // iterable type".
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("d.all(x, true)", "int")]
    [InlineData("ds.exists(c, true)", "string")]
    [InlineData("dn.map(x, x)", "null_type")]
    public void RangeThatIsNotAListOrAMap_IsEvalInvalidComprehensionRangeAtTheRange(string expression, string type)
    {
        CelError error = SingleError(expression);

        Assert.Equal(CelErrorCode.EvalInvalidComprehensionRange, error.Code);
        Assert.Equal("value of type '" + type + "' cannot be the range of a comprehension (must be list or map)", error.Message);
        Assert.Equal((0, 1, 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private CelError SingleError(string expression)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }
}

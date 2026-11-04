using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// EvalNoMatchingOverload (3000) for an operand of the static type dyn: type checking accepts the call,
// and at evaluation the value fits none of its candidates. The message names the function as the syntax
// tree does and the types of the values by name; the place is the call. The variables are dyn and hold
// 1, 's', [1] and null. cel-go v0.32.0 gives an error for each of these expressions.
public sealed class NoMatchingOverloadTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("ds", CelType.Dyn)
        .AddVariable("dl", CelType.Dyn)
        .AddVariable("dn", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("d", 1L)
        .Add("ds", "s")
        .Add("dl", CelValue.FromList(CelList.Create(CelValue.FromInt64(1))))
        .Add("dn", CelValue.Null);

    [Theory]
    [Trait("Requirement", "+R-6")]
    [Trait("Requirement", "F-4")]
    [InlineData("ds - 1", "no such overload: _-_(string, int)", 3)]
    [InlineData("dl + 1", "no such overload: _+_(list, int)", 3)]
    [InlineData("d < 's'", "no such overload: _<_(int, string)", 2)]
    [InlineData("size(d)", "no such overload: size(int)", 4)]
    [InlineData("!d", "no such overload: !_(int)", 0)]
    public void DynOperandThatFitsNoCandidate_IsEvalNoMatchingOverloadAtTheCall(string expression, string message, int offset)
    {
        AssertError(expression, message, offset);
    }

    // Lists, maps and null have no ordering: the ordering overloads take none of them.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("dl < dl", "no such overload: _<_(list, list)", 3)]
    [InlineData("dn < dn", "no such overload: _<_(null_type, null_type)", 3)]
    public void OrderingOfValuesWithoutOrder_IsEvalNoMatchingOverload(string expression, string message, int offset)
    {
        AssertError(expression, message, offset);
    }

    // A condition of ?: that is not a bool is named with its type; an operand of an && chain that is not a
    // bool, when no operand decides the result, is named with the types of all operands of the chain.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("d ? 1 : 2", "no such overload: _?_:_(int)", 2)]
    [InlineData("d && true", "no such overload: _&&_(int, bool)", 2)]
    public void ConditionOrLogicalOperandThatIsNotABool_IsEvalNoMatchingOverload(string expression, string message, int offset)
    {
        AssertError(expression, message, offset);
    }

    // The operand that decides the result is the result, and the other one is not looked at.
    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("d && false", false)]
    [InlineData("false && d", false)]
    [InlineData("true || d", true)]
    public void DecidingOperand_HidesTheOperandThatIsNotABool(string expression, bool expected)
    {
        Assert.Equal(expected, environment.Compile<bool>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    private void AssertError(string expression, string message, int offset)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal(message, error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }
}

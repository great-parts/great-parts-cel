using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The overload logical_or: a true operand absorbs an error of the other operand. The variable "unbound"
// is declared as bool and has no value, so reading it is an error; "one" is declared as dyn and holds 1.
public sealed class LogicalOrTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<bool>("unbound")
        .AddVariable("one", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("one", 1L);

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("false || false", false)]
    [InlineData("false || true", true)]
    [InlineData("true || false", true)]
    [InlineData("false || false || false || true", true)]
    [InlineData("unbound || true", true)]
    [InlineData("true || unbound", true)]
    [InlineData("unbound || false || true", true)]
    [InlineData("one || true", true)]
    public void Or_IsTrueWhenAnOperandIsTrue(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate(expression).GetValueOrThrow());
    }

    // Without a true operand, the first operand that is an error is the value of the expression.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void ErrorWithoutTrueOperand_IsTheValue()
    {
        CelError first = Assert.Single(Evaluate("unbound || false").Errors);
        CelError second = Assert.Single(Evaluate("false || unbound").Errors);

        Assert.Equal((CelErrorCode.Unbound, 0), (first.Code, first.Location.Offset));
        Assert.Equal((CelErrorCode.Unbound, 9), (second.Code, second.Location.Offset));
        Assert.Equal("no value is bound to variable 'unbound'", first.Message);
    }

    // An operand of type dyn that holds no bool fits no overload; the error is at the "||" call.
    [Fact]
    [Trait("Requirement", "+R-6")]
    public void DynOperandThatIsNotABool_IsEvalNoMatchingOverloadAtTheCall()
    {
        CelError error = Assert.Single(Evaluate("one || false").Errors);

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Equal((4, 1, 5), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private CelEvalResult<bool> Evaluate(string expression) =>
        environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
}

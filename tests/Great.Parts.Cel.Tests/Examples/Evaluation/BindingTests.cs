using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// A binding is checked against the declared type of its variable when the expression first reads it. A
// value of another kind is BindingTypeMismatch at the identifier, whatever the expression does with it; a
// binding that is not read gives no error.
// There is no conversion between numeric kinds. cel-go does not check bindings against declarations,
// so these examples do not rely on it. A string bound to an int or a wrapper variable is stated in
// Values/ValueTypeTests.
public sealed class BindingTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable<long?>("w")
        .AddVariable<WideFlags>("e")
        .Build()
        .GetEnvironmentOrThrow();

    // An enumeration on ulong, with a value above the range of int.
    private enum WideFlags : ulong
    {
        None = 0,
        High = 9_223_372_036_854_775_808,
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void UIntBoundToAnIntVariable_IsBindingTypeMismatch()
    {
        AssertMismatch(SingleError("x", new CelBindings().Add("x", 5UL)), "x", "uint", "int", 0);
    }

    // A string is not a list. Whether a list with an element of another kind fits list(int) is not stated here.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void StringBoundToAListVariable_IsBindingTypeMismatch()
    {
        AssertMismatch(SingleError("size(l)", new CelBindings().Add("l", "abc")), "l", "string", "list(int)", 5);
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void ListOfIntsBoundToAListOfIntVariable_Fits()
    {
        var bindings = new CelBindings().Add("l", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2)));

        Assert.Equal(3L, environment.Compile<long>("l[0] + l[1]").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // A wrapper takes null and a value of its primitive type.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void NullAndIntBoundToAWrapperVariable_Fit()
    {
        CelProgram<bool> program = environment.Compile<bool>("w == null").GetProgramOrThrow();

        Assert.True(program.Evaluate(new CelBindings().Add<long?>("w", null), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.False(program.Evaluate(new CelBindings().Add<long?>("w", 7), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The value of x is never read: || is decided by true first.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void BindingThatIsNotRead_GivesNoError()
    {
        CelEvalResult<bool> result = environment.Compile<bool>("true || x == 1").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("x", "text"), cancellationToken: CancellationToken.None);

        Assert.True(result.GetValueOrThrow());
    }

    // The value of an enumeration is its underlying value as an int; above the range of int the binding
    // is ConversionRange where the expression reads it, not an exception of CelBindings.Add.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void EnumerationValueAboveTheRangeOfInt_IsConversionRangeAtTheRead()
    {
        var bindings = new CelBindings().Add("e", WideFlags.High);

        CelError error = SingleError("1 + e", bindings);

        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
        Assert.Equal("9223372036854775808 is out of the range of int", error.Message);
        Assert.Equal((4, 1, 5), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Equal(0L, environment.Compile<long>("e").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("e", WideFlags.None), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    private CelError SingleError(string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    private static void AssertMismatch(CelError error, string name, string actual, string declared, int offset)
    {
        Assert.Equal(CelErrorCode.BindingTypeMismatch, error.Code);
        Assert.Equal(
            "value of variable '" + name + "' has type '" + actual + "', which does not match the declared type '" + declared + "'",
            error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }
}

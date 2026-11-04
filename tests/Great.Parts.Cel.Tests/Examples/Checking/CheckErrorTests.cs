using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The errors of type checking, their texts and their places. cel-go v0.32.0 finds the same errors at the
// same offsets (a probe with the environment flags of the oracle); its wording of NoSuchField and
// InvalidComprehensionRange differs, and texts are not part of the comparison with cel-go.
public sealed class CheckErrorTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<bool>("b")
        .AddVariable("w", CelType.WrapperOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    // An operand of && or || must be bool, also the predicate of all and exists, which becomes an operand
    // of the step of the expansion.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("1 && true", 0, "expected type 'bool' but found 'int'")]
    [InlineData("true || 'a'", 8, "expected type 'bool' but found 'string'")]
    [InlineData("[1, 2].all(x, x)", 14, "expected type 'bool' but found 'int'")]
    [InlineData("[1, 2].exists(x, 'a')", 17, "expected type 'bool' but found 'string'")]
    public void OperandThatIsNotBool_IsTypeMismatchAtTheOperand(string expression, int offset, string message)
    {
        AssertSingleError(expression, CelErrorCode.TypeMismatch, offset, message);
    }

    // Only maps, dyn and type parameters have fields in this part: a selection on any other type is
    // NoSuchField at the selection, the "." — for a presence test, the node of has at its "(".
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("1.f", 1, "f", "int")]
    [InlineData("1u.f", 2, "f", "uint")]
    [InlineData("2.5.f", 3, "f", "double")]
    [InlineData("'s'.f", 3, "f", "string")]
    [InlineData("b'x'.g", 4, "g", "bytes")]
    [InlineData("b.f", 1, "f", "bool")]
    [InlineData("w.f", 1, "f", "wrapper(int)")]
    [InlineData("[1].f", 3, "f", "list(int)")]
    [InlineData("has(1.f)", 3, "f", "int")]
    [InlineData("has(m.a.b)", 3, "b", "int")]
    public void SelectionOnATypeWithoutFields_IsNoSuchFieldAtTheSelection(string expression, int offset, string field, string type)
    {
        AssertSingleError(expression, CelErrorCode.NoSuchField, offset, "undefined field '" + field + "' in type '" + type + "'");
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("1.all(x, true)", 0, "int")]
    [InlineData("'abc'.exists(c, true)", 0, "string")]
    [InlineData("true && b'ab'.all(c, true)", 8, "bytes")]
    [InlineData("[1].all(x, 2u.exists(y, true))", 11, "uint")]
    public void RangeThatIsNotAListAMapOrDyn_IsInvalidComprehensionRangeAtTheRange(string expression, int offset, string type)
    {
        AssertSingleError(
            expression,
            CelErrorCode.InvalidComprehensionRange,
            offset,
            "expression of type '" + type + "' cannot be the range of a comprehension (must be list, map, or dyn)");
    }

    // Ranges of the types dyn and map pass type checking; what a dyn range holds is found at evaluation.
    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("dyn(1).all(x, true)")]
    [InlineData("m.all(k, m[k] > 0)")]
    [InlineData("{'a': 1}.exists(k, k == 'a')")]
    public void RangeOfTypeDynOrMap_PassesTypeChecking(string expression)
    {
        Assert.True(environment.Compile<CelValue>(expression).IsSuccess);
    }

    // Checking goes on after an error, so a compilation returns every error, in the order of the text.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void ErrorsOfOneText_AreAllReportedInTheOrderOfTheText()
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>("[y, 1.f, 1 && true]");

        Assert.False(compiled.IsSuccess);
        Assert.Equal(
            new[] { (CelErrorCode.UndeclaredReference, 1), (CelErrorCode.NoSuchField, 5), (CelErrorCode.TypeMismatch, 9) },
            compiled.Errors.Select(error => (error.Code, error.Location.Offset)));
        Assert.All(compiled.Errors, error => Assert.Equal(CelErrorStage.Check, error.Stage));
    }

    // An invalid range does not make the macro a part with an error: its type is still that of the result,
    // so the call around all or exists is looked up and fails too, while the list of map fits size. The
    // errors come in the order of their places, not in the order the checker finds them: "(" of size at 4
    // before the range at 5. cel-go v0.32.0 gives the same errors at the same places in the same order.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("size(1.all(x, true))", CelErrorCode.NoMatchingOverload, 4, CelErrorCode.InvalidComprehensionRange, 5, "size", "bool")]
    [InlineData("size(1.exists(x, true))", CelErrorCode.NoMatchingOverload, 4, CelErrorCode.InvalidComprehensionRange, 5, "size", "bool")]
    [InlineData("1.all(x, true) + 1", CelErrorCode.InvalidComprehensionRange, 0, CelErrorCode.NoMatchingOverload, 15, "_+_", "bool, int")]
    public void InvalidRangeOfAMacroInsideACall_IsTwoErrorsInTheOrderOfTheirPlaces(
        string expression,
        CelErrorCode firstCode,
        int firstOffset,
        CelErrorCode secondCode,
        int secondOffset,
        string function,
        string argumentTypes)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);

        Assert.Equal(
            new[] { (firstCode, firstOffset), (secondCode, secondOffset) },
            compiled.Errors.Select(error => (error.Code, error.Location.Offset)));
        Assert.Equal(
            "found no matching overload for '" + function + "' applied to '(" + argumentTypes + ")'",
            Assert.Single(compiled.Errors, error => error.Code == CelErrorCode.NoMatchingOverload).Message);
        Assert.Equal(
            "expression of type 'int' cannot be the range of a comprehension (must be list, map, or dyn)",
            Assert.Single(compiled.Errors, error => error.Code == CelErrorCode.InvalidComprehensionRange).Message);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void InvalidRangeOfMapInsideSize_IsOneError()
    {
        CelError error = Assert.Single(environment.Compile<CelValue>("size(1.map(x, x))").Errors);

        Assert.Equal((CelErrorCode.InvalidComprehensionRange, 5), (error.Code, error.Location.Offset));
    }

    // A part with an error makes no further error: the call or the list around it is not looked up.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("y + 1", CelErrorCode.UndeclaredReference, 0)]
    [InlineData("1.f + 1", CelErrorCode.NoSuchField, 1)]
    [InlineData("-(1 && true)", CelErrorCode.TypeMismatch, 2)]
    [InlineData("size(1.f)", CelErrorCode.NoSuchField, 6)]
    [InlineData("[y] + ['a'] + [1]", CelErrorCode.UndeclaredReference, 1)]
    public void PartWithAnError_MakesNoFurtherError(string expression, CelErrorCode code, int offset)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal((code, offset), (error.Code, error.Location.Offset));
    }

    private void AssertSingleError(string expression, CelErrorCode code, int offset, string message)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);

        Assert.False(compiled.IsSuccess);
        CelError error = Assert.Single(compiled.Errors);
        Assert.Equal((code, CelErrorStage.Check, message), (error.Code, error.Stage, error.Message));
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.Location.SourceName);
    }
}

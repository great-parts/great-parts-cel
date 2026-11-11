using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.MathSet.MathEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.MathSet;

// The functions of the set math on their edges: the maximum and the minimum of numbers of one kind and of different kinds, with ties,
// zeros and NaN; the rounding functions with the sign of the zero; the integers at the ends of their ranges; the bit operations and
// the shifts at and above 64. Each row of the first table is an expression that is true.
public sealed class MathFunctionTests
{
    private const string Holds = """
        math.greatest(1, 2) == 2
        math.greatest(2, 1) == 2
        math.least(1, 2) == 1
        math.greatest(1, 3u) == 3u
        math.greatest(3u, 1) == 3u
        math.least(1u, 2) == 1u
        math.greatest(2.5, 1) == 2.5
        math.least(2.5, 1u) == 1u
        math.greatest(2u, 2.5) == 2.5
        math.least(2.5) == 2.5
        math.greatest(1, 2, 3) == 3
        math.least(3, 1, 2) == 1
        math.greatest(1u, 5u, 3u, 4u) == 5u
        math.least(1.5, -2.5, 3.5) == -2.5
        math.greatest([1, 5, 3]) == 5
        math.least([4.5, 2.5, 3.5]) == 2.5
        math.greatest([7u]) == 7u
        math.greatest(1, dyn(2.5), 3u) == 3u
        type(math.greatest(1, 1.0)) == int
        type(math.greatest(1.0, 1)) == double
        type(math.least(2u, 2)) == uint
        type(math.least(2, 2u)) == int
        type(math.greatest([dyn(1), dyn(2.5), dyn(2u)])) == double
        type(math.greatest([dyn(2.5), dyn(1), dyn(2u)])) == double
        type(math.least([dyn(2u), dyn(2), dyn(2.0)])) == uint
        1.0 / math.greatest(-0.0, 0.0) < 0.0
        1.0 / math.greatest(0.0, -0.0) > 0.0
        1.0 / math.least(0.0, -0.0) > 0.0
        math.isNaN(math.greatest(0.0 / 0.0))
        math.isNaN(math.least(0.0 / 0.0))
        math.greatest(9223372036854775807, 9223372036854775808u) == 9223372036854775808u
        math.greatest(9007199254740993, 9007199254740992.0) == 9007199254740993
        math.ceil(1.2) == 2.0
        math.ceil(-1.2) == -1.0
        math.ceil(3.0) == 3.0
        1.0 / math.ceil(-0.5) < 0.0
        math.floor(1.8) == 1.0
        math.floor(-1.2) == -2.0
        math.trunc(1.9) == 1.0
        math.trunc(-1.9) == -1.0
        1.0 / math.trunc(-0.5) < 0.0
        math.round(2.5) == 3.0
        math.round(-2.5) == -3.0
        math.round(1.4) == 1.0
        math.round(0.49999999999999994) == 0.0
        1.0 / math.round(-0.4) < 0.0
        math.isNaN(math.round(0.0 / 0.0))
        math.isInf(1.0 / 0.0)
        math.isInf(-1.0 / 0.0)
        !math.isInf(1.0)
        !math.isInf(0.0 / 0.0)
        math.isNaN(0.0 / 0.0)
        !math.isNaN(1.0)
        !math.isNaN(1.0 / 0.0)
        math.isFinite(1.0)
        !math.isFinite(1.0 / 0.0)
        !math.isFinite(0.0 / 0.0)
        math.abs(-5) == 5
        math.abs(5) == 5
        math.abs(-2.5) == 2.5
        math.abs(7u) == 7u
        math.abs(-9223372036854775807) == 9223372036854775807
        1.0 / math.abs(-0.0) > 0.0
        math.sign(-3.5) == -1.0
        math.sign(3.5) == 1.0
        math.sign(0.0) == 0.0
        1.0 / math.sign(-0.0) > 0.0
        math.isNaN(math.sign(0.0 / 0.0))
        math.sign(-7) == -1
        math.sign(0) == 0
        math.sign(9) == 1
        math.sign(0u) == 0u
        math.sign(5u) == 1u
        math.sqrt(16.0) == 4.0
        math.sqrt(9) == 3.0
        math.sqrt(25u) == 5.0
        math.isNaN(math.sqrt(-4.0))
        math.isNaN(math.sqrt(-4))
        math.bitAnd(12, 10) == 8
        math.bitAnd(-1, 5) == 5
        math.bitAnd(12u, 10u) == 8u
        math.bitOr(12, 10) == 14
        math.bitOr(12u, 10u) == 14u
        math.bitXor(12, 10) == 6
        math.bitXor(12u, 10u) == 6u
        math.bitNot(5) == -6
        math.bitNot(0) == -1
        math.bitNot(0u) == 18446744073709551615u
        math.bitShiftLeft(1, 3) == 8
        math.bitShiftLeft(1, 0) == 1
        math.bitShiftLeft(1, 63) == -9223372036854775808
        math.bitShiftLeft(1, 64) == 0
        math.bitShiftLeft(1, 100) == 0
        math.bitShiftLeft(1u, 63) == 9223372036854775808u
        math.bitShiftLeft(1u, 64) == 0u
        math.bitShiftRight(16, 2) == 4
        math.bitShiftRight(8, 0) == 8
        math.bitShiftRight(-1, 1) == 9223372036854775807
        math.bitShiftRight(-1, 63) == 1
        math.bitShiftRight(-1, 64) == 0
        math.bitShiftRight(16u, 2) == 4u
        math.bitShiftRight(18446744073709551615u, 64) == 0u
        """;

    // "expression | code | message": the error has the code and the message at the `(` of the first call.
    private const string Errors = """
        math.greatest(0.0 / 0.0, 1.0) | InvalidArgument | invalid argument to 'math.@max': NaN values cannot be ordered
        math.greatest(1.0, 0.0 / 0.0) | InvalidArgument | invalid argument to 'math.@max': NaN values cannot be ordered
        math.least(0.0 / 0.0, 1.0) | InvalidArgument | invalid argument to 'math.@min': NaN values cannot be ordered
        math.least(1.0, 0.0 / 0.0) | InvalidArgument | invalid argument to 'math.@min': NaN values cannot be ordered
        math.greatest(1.0, 2.0, 0.0 / 0.0) | InvalidArgument | invalid argument to 'math.@max': NaN values cannot be ordered
        math.greatest([0.0 / 0.0, 1.0]) | InvalidArgument | invalid argument to 'math.@max': NaN values cannot be ordered
        math.least([1.0, 2.0, 0.0 / 0.0]) | InvalidArgument | invalid argument to 'math.@min': NaN values cannot be ordered
        math.abs(-9223372036854775807 - 1) | Overflow | integer overflow
        math.bitShiftLeft(1, -1) | InvalidArgument | math.bitShiftLeft() negative offset: -1
        math.bitShiftLeft(1u, -3) | InvalidArgument | math.bitShiftLeft() negative offset: -3
        math.bitShiftRight(1, -1) | InvalidArgument | math.bitShiftRight() negative offset: -1
        math.bitShiftRight(1u, -2) | InvalidArgument | math.bitShiftRight() negative offset: -2
        """;

    public static TheoryData<string> HoldingCases => HoldingData();

    public static TheoryData<string, string, string> ErrorCases => ErrorData();

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_HasItsCodeAndMessageAtTheCall(string expression, string code, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal((Enum.Parse<CelErrorCode>(code), message, expression.IndexOf('(', StringComparison.Ordinal)), (error.Code, error.Message, error.Location.Offset));
    }

    // The empty list is the error of math.@max, and of math.@min for the least; the macros cannot write the empty list, so the
    // list comes from a variable of the type dyn.
    [Theory]
    [InlineData("math.greatest(x)", "invalid argument to 'math.@max': math.@max(list) argument must not be empty")]
    [InlineData("math.least(x)", "invalid argument to 'math.@min': math.@min(list) argument must not be empty")]
    public static void EmptyList_IsInvalidArgument(string expression, string message)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, new CelBindings().Add("x", ListOf()));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.InvalidArgument, message), (error.Code, error.Message));
    }

    // The list overloads check only that the argument is a list: an element that is not a number is refused when the function reads it.
    [Fact]
    public static void ListWithAnElementThatIsNotANumber_IsEvalNoMatchingOverload()
    {
        CelValue list = ListOf(CelValue.FromInt64(1), CelValue.FromString("a"));

        CelError error = Assert.Single(Evaluate("math.greatest(x)", new CelBindings().Add("x", list)).Errors);

        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, "no such overload: math.@max(list)"), (error.Code, error.Message));
    }

    // A one-element list is returned without a comparison, so its NaN is the result; a list of kinds that no static type of the
    // list parameters holds together is read all the same.
    [Fact]
    public static void ListOfOneNaN_IsItsResult_AndMixedKindsAreRead()
    {
        CelValue mixed = ListOf(CelValue.FromInt64(3), CelValue.FromDouble(7.5), CelValue.FromUInt64(5));
        CelValue alone = ListOf(CelValue.FromDouble(double.NaN));

        Assert.Equal(CelValue.FromDouble(7.5), Evaluate("math.greatest(x)", new CelBindings().Add("x", mixed)).GetValueOrThrow());
        Assert.Equal(CelValue.FromInt64(3), Evaluate("math.least(x)", new CelBindings().Add("x", mixed)).GetValueOrThrow());
        Assert.True(Evaluate("math.isNaN(math.greatest(x))", new CelBindings().Add("x", alone)).GetValueOrThrow().TryGetBoolean(out bool isNaN) && isNaN);
    }

    // A dyn value picks the overload at run time: a number of any kind passes the one-number form, a string passes none.
    [Fact]
    public static void DynArguments_ChooseTheOverloadByKind()
    {
        Assert.Equal(CelValue.FromUInt64(4), Evaluate("math.greatest(x, y)", new CelBindings().Add("x", 4UL).Add("y", 2UL)).GetValueOrThrow());
        Assert.Equal(CelValue.FromDouble(4.5), Evaluate("math.greatest(x, y)", new CelBindings().Add("x", 4L).Add("y", 4.5)).GetValueOrThrow());
        Assert.Equal(CelValue.FromInt64(3), Evaluate("math.abs(x)", new CelBindings().Add("x", -3L)).GetValueOrThrow());

        CelError error = Assert.Single(Evaluate("math.greatest(x)", new CelBindings().Add("x", "a")).Errors);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
    }

    private static TheoryData<string> HoldingData()
    {
        TheoryData<string> data = [];
        foreach (string row in Holds.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            data.Add(row);
        }

        return data;
    }

    private static TheoryData<string, string, string> ErrorData()
    {
        TheoryData<string, string, string> data = [];
        foreach (string row in Errors.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1], cells[2]);
        }

        return data;
    }
}

using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.MathSet.MathEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.MathSet;

// The value of cel.bind: the name is the value of its expression in the result and shadows every other name, an inner bind shadows an
// outer one, the value is the one that its expression has — an error too, which no read of the name means — and the result is typed by its
// own expression. Each row of the table is an expression that is true.
public sealed class BindEvaluationTests
{
    private const string Holds = """
        cel.bind(v, 5, v * v) == 25
        cel.bind(v, 1, 2) == 2
        cel.bind(v, 1, cel.bind(v, v + 1, v)) == 2
        cel.bind(a, 1, cel.bind(b, a + 1, a + b)) == 3
        cel.bind(a, 1, cel.bind(b, a + 1, cel.bind(c, a + b, c * 2))) == 6
        cel.bind(v, 'a', v + 'b') == 'ab'
        cel.bind(v, 1 / 0, 7) == 7
        cel.bind(v, 1 / 0, cel.bind(w, v == 1 || true, w)) == true
        cel.bind(i, 10, i + j) == 12
        cel.bind(v, [1, 2], v.size()) == 2
        cel.bind(v, 3, [1, 2].map(e, e * v)) == [3, 6]
        cel.bind(v, 3, [1, 2].exists(e, e == v - 1))
        cel.bind(v, 1, v) + cel.bind(v, 2, v) == 3
        cel.bind(v, dyn(1), v) == 1
        cel.bind(v, i, v + j) == 3
        """;

    public static TheoryData<string> HoldingCases => HoldingData();

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    // The error of the value of the name is the error of every read of it, at the expression that failed; a name that is not read
    // does not produce it.
    [Fact]
    public static void ErrorOfTheValue_IsTheValueOfEachRead()
    {
        const string Expression = "cel.bind(v, 1 / 0, v)";

        CelError error = Assert.Single(Evaluate(Expression).Errors);

        Assert.Equal(CelErrorCode.DivisionByZero, error.Code);
        Assert.Equal(Expression.IndexOf('/', StringComparison.Ordinal), error.Location.Offset);
    }

    // The name has the type of its value and the result the type of its expression.
    [Fact]
    public static void Result_HasTheTypeOfItsExpression()
    {
        Assert.False(MathEnvironment.Environment.Compile<CelValue>("cel.bind(v, 1, v + 'a')").IsSuccess);
        Assert.True(MathEnvironment.Environment.Compile<long>("cel.bind(v, 1, v + 1)").IsSuccess);
        Assert.False(MathEnvironment.Environment.Compile<string>("cel.bind(v, 1, v + 1)").IsSuccess);
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
}

using System;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Library.Extensions.TwoVar;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Plans of the comprehensions of two variables: the slots of the frame, and the same values and the same cost from a plan without type
// checking, which plans the expansion the same way as the plan of a checked expression.
public sealed class TwoVariablePlanTests
{
    // A comprehension of two variables has three slots: the first variable, the second and the accumulator; one of one variable has two.
    [Theory]
    [InlineData("ints.all(x, true)", 2)]
    [InlineData("ints.all(i, v, true)", 3)]
    [InlineData("ints.transformList(i, v, v)", 3)]
    [InlineData("ints.transformMap(i, v, v)", 3)]
    [InlineData("ints.transformMapEntry(i, v, {i: v})", 3)]
    [InlineData("ints.all(i, v, ints.exists(j, w, true))", 6)]
    [InlineData("ints.all(i, v, ints.exists(w, true))", 5)]
    [InlineData("ints.all(x, ints.all(i, v, true))", 5)]
    public static void Comprehension_HasItsSlotsInTheFrame(string expression, int locals)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Assert.Equal(locals, PlanWithoutTypeCheck(expression).LocalCount);
        Assert.Equal(locals, PlanChecked(expression).LocalCount);
    }

    // The plan without type checking reads the names from the bindings and the variables of the macro from its slots, and gives the
    // value of the plan of a checked expression.
    [Theory]
    [InlineData("ints.all(i, v, v > 5 * i)")]
    [InlineData("!ints.exists(i, v, v == 20 && i == 2)")]
    [InlineData("ints.existsOne(i, v, v > 25)")]
    [InlineData("counts.transformList(k, v, k) == ['a', 'b']")]
    [InlineData("ints.transformList(i, v, v > 10, v / 10) == [2, 3]")]
    [InlineData("counts.transformMap(k, v, v * 5) == {'a': 5, 'b': 10}")]
    [InlineData("ints.transformMap(i, v, i != 1, v) == {0: 10, 2: 30}")]
    [InlineData("counts.transformMapEntry(k, v, {v: k}) == {1: 'a', 2: 'b'}")]
    [InlineData("twenty.transformMapEntry(i, v, {v: i})[20] == 19")]
    [InlineData("twenty.transformMap(i, v, v * 2)[16] == 34")]
    [InlineData("ints.transformList(i, v, ints.transformList(j, w, j * i)) == [[0, 0, 0], [0, 1, 2], [0, 2, 4]]")]
    [InlineData("ints.all(i, v, true) && i == 1 && j == 2")]
    public static void PlanWithoutTypeCheck_GivesTheValueOfTheCheckedPlan(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        CelValue withoutCheck = PlanWithoutTypeCheck(expression).Evaluate(TwoVarEnvironment.Default().Values, null, CancellationToken.None);
        CelValue withCheck = PlanChecked(expression).Evaluate(TwoVarEnvironment.Default().Values, null, CancellationToken.None);

        Assert.True(withoutCheck.TryGetBoolean(out bool first) && first, withoutCheck.Kind.ToString());
        Assert.True(withCheck.TryGetBoolean(out bool second) && second, withCheck.Kind.ToString());
    }

    // The plan without type checking charges what the checked plan charges: the macro step, the expressions of the author and the
    // pairs put in. With the limit of the units both pass, and with one unit less both stop.
    [Theory]
    [InlineData("[7, 8, 9].transformMap(i, v, v)", 17)]
    [InlineData("[7, 8].transformMapEntry(i, v, {v: i})", 20)]
    [InlineData("[7, 8, 9].transformList(i, v, v > 7, v)", 18)]
    [InlineData("{'p': 1}.all(k, v, v > 0)", 7)]
    public static void PlanWithoutTypeCheck_ChargesTheUnitsOfTheCheckedPlan(string expression, int units)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ulong cost = (ulong)units;

        foreach (Plan plan in new[] { PlanWithoutTypeCheck(expression), PlanChecked(expression) })
        {
            Assert.False(plan.Evaluate(TwoVarEnvironment.Default().Values, cost, CancellationToken.None).TryGetError(out _), expression);
            Assert.True(plan.Evaluate(TwoVarEnvironment.Default().Values, cost - 1, CancellationToken.None).TryGetError(out _), expression);
        }
    }

    private static Plan PlanWithoutTypeCheck(string expression) =>
        Planner.PlanWithoutTypeCheck(Parse(expression), TwoVarEnvironment.Environment.Declarations);

    private static Plan PlanChecked(string expression)
    {
        CheckResult result = TwoVarEnvironment.Environment.Check(Parse(expression));
        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        return Planner.Plan(result.Expression!, TwoVarEnvironment.Environment.Declarations);
    }

    private static ParsedExpression Parse(string expression)
    {
        ParseResult parsed = TwoVarEnvironment.Environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression + ": " + string.Join("; ", parsed.Errors));
        return parsed.Expression;
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The fused node of the read of a field and the comparison with a literal: the value it gives, and that it gives what the two nodes give
// without the fusion — the same error with the same place, and the same units charged before the end — for the values that take the
// shortcuts of the node and for those that go on to the call.
public sealed class FusedComparisonTests
{
    // The field of the map s: a long, a ulong, a double, a string, a bool or null.
    [Theory]
    [InlineData("s.f == 1", 1L, true)]
    [InlineData("s.f == 1", 2L, false)]
    [InlineData("s.f == 1", 1.0, true)]
    [InlineData("s.f == 1", "1", false)]
    [InlineData("s.f == 1", null, false)]
    [InlineData("s.f != 1", 2L, true)]
    [InlineData("s.f != 1", 1L, false)]
    [InlineData("s.f == 1u", 1UL, true)]
    [InlineData("s.f == 1u", 1L, true)]
    [InlineData("s.f < 2", 1L, true)]
    [InlineData("s.f < 2", 2L, false)]
    [InlineData("s.f <= 2", 2L, true)]
    [InlineData("s.f > 2", 3L, true)]
    [InlineData("s.f >= 2", 1L, false)]
    [InlineData("s.f < 2", 1.5, true)]
    [InlineData("s.f < 2", long.MaxValue, false)]
    [InlineData("s.f < 2u", 1UL, true)]
    [InlineData("s.f >= 2u", ulong.MaxValue, true)]
    [InlineData("s.f == 1.5", 1.5, true)]
    [InlineData("s.f == 1.5", double.NaN, false)]
    [InlineData("s.f != 1.5", double.NaN, true)]
    [InlineData("s.f <= 1.5", double.NaN, false)]
    [InlineData("s.f >= 1.5", double.NaN, false)]
    [InlineData("s.f < 1.5", 1L, true)]
    [InlineData("s.f == 0.0", -0.0, true)]
    [InlineData("s.f == 'ab'", "ab", true)]
    [InlineData("s.f == 'ab'", "a", false)]
    [InlineData("s.f < 'b'", "a", true)]
    [InlineData("s.f >= 'b'", "a", false)]
    [InlineData("s.f == true", true, true)]
    [InlineData("s.f != true", true, false)]
    [InlineData("s.f > false", true, true)]
    [InlineData("s.f < false", true, false)]
    [InlineData("s.f == null", null, true)]
    [InlineData("s.f == null", 0L, false)]
    [InlineData("s.f != null", null, false)]
    [InlineData("s.f != null", "x", true)]
    public static void Comparison_GivesTheValueOfThePair(string expression, object? field, bool expected)
    {
        Plan plan = Plans.Checked(expression);

        Assert.IsType<FusedComparisonPlanNode>(plan.Root);
        Assert.Equal(CelValue.FromBoolean(expected), plan.EvaluateOn(Bind(0, field)));
    }

    // The rows are those of the table above that give an error or charge; the shape of s is 0, a map with the field f; 1, a map without
    // it; 2, no binding of s; 3, an int. For each the fused plan and the plan without the fusion are evaluated without a limit and with the
    // limits of the sum, one less and half of it.
    [Theory]
    [InlineData("s.f == 1", 1L, 0)]
    [InlineData("s.f == 1", 1L, 1)]
    [InlineData("s.f == 1", 1L, 2)]
    [InlineData("s.f == 1", 1L, 3)]
    [InlineData("s.f != 1", "a", 0)]
    [InlineData("s.f == 1", "a", 0)]
    [InlineData("s.f == 'abcdefghijklmnop'", "abcdefghijklmnoq", 0)]
    [InlineData("s.f < 1", 1L, 0)]
    [InlineData("s.f < 1", 1L, 1)]
    [InlineData("s.f < 1", 1L, 2)]
    [InlineData("s.f < 1", 1L, 3)]
    [InlineData("s.f < 1", "a", 0)]
    [InlineData("s.f < 1", null, 0)]
    [InlineData("s.f < 1", 0.5, 0)]
    [InlineData("s.f < 1u", -1L, 0)]
    [InlineData("s.f > 1.5", 2L, 0)]
    [InlineData("s.f >= 'abcdefghijklmnop'", "abcdefghijklmnoq", 0)]
    [InlineData("s.f >= 'abcdefghijklmnop'", 1L, 0)]
    [InlineData("s.f <= true", false, 0)]
    [InlineData("s.f <= true", "a", 0)]
    [InlineData("s.f == null", null, 0)]
    [InlineData("s.f != null", null, 1)]
    [InlineData("s.f == null", null, 3)]
    [InlineData("s.f >= 2", double.NaN, 0)]
    public static void Comparison_IsTheSameAsTheNodesOfThePair(string expression, object? field, int shape)
    {
        Plan fused = Plans.Checked(expression);
        Plan plain = Unfused(expression);
        Dictionary<string, CelValue> bindings = Bind(shape, field);

        Assert.IsType<FusedComparisonPlanNode>(fused.Root);
        Assert.IsNotType<FusedComparisonPlanNode>(plain.Root);
        (_, ulong units) = Run(plain, bindings, ulong.MaxValue);
        foreach (ulong? limit in new ulong?[] { null, ulong.MaxValue, units, units - 1, units / 2 })
        {
            (CelValue Value, ulong Charged) expected = Run(plain, bindings, limit);
            (CelValue Value, ulong Charged) actual = Run(fused, bindings, limit);

            Assert.Equal(expected.Charged, actual.Charged);
            AssertSame(expected.Value, actual.Value);
        }
    }

    // The planner fuses only the form v.f op c: a literal that is not on the right, a presence test, an operand that is not a variable,
    // a second operand that is not a literal of the kinds, and an expression of any other form stay the nodes of the call.
    [Theory]
    [InlineData("1 == s.f")]
    [InlineData("has(s.f) == true")]
    [InlineData("s.f.g == 1")]
    [InlineData("s.f == n")]
    [InlineData("s.f == s.g")]
    [InlineData("s.f == b'x'")]
    [InlineData("s['f'] == 1")]
    [InlineData("s.f + 1 == 2")]
    [InlineData("size(s.f) == 1")]
    [InlineData("n == 1")]
    [InlineData("n < 1")]
    public static void OtherForms_AreNotFused(string expression)
    {
        Assert.IsNotType<FusedComparisonPlanNode>(Plans.Checked(expression).Root);
    }

    [Theory]
    [InlineData("s.f == 1")]
    [InlineData("s.f != 'a'")]
    [InlineData("s.f < 2u")]
    [InlineData("s.f <= 1.5")]
    [InlineData("s.f > 0")]
    [InlineData("s.f >= 1")]
    [InlineData("any.x == true")]
    [InlineData("s.f != null")]
    [InlineData("(s.f) == 1")]
    public static void FieldOfVariableWithLiteral_IsFused(string expression)
    {
        Assert.IsType<FusedComparisonPlanNode>(Plans.Checked(expression).Root);
    }

    [Fact]
    public static void FusedNodeInAChain_IsAnOperand()
    {
        var and = Assert.IsType<AndPlanNode>(Plans.Checked("a && s.f == 1 && s.g < 2").Root);

        Assert.IsType<VariablePlanNode>(and.Operands[0]);
        Assert.IsType<FusedComparisonPlanNode>(and.Operands[1]);
        Assert.IsType<FusedComparisonPlanNode>(and.Operands[2]);
    }

    [Theory]
    [InlineData("s.f == 1", typeof(CallPlanNode))]
    [InlineData("s.f < 1", typeof(DispatchPlanNode))]
    public static void WithTheOptionOff_TheNodesAreThoseOfTheCall(string expression, Type node)
    {
        Assert.IsType(node, Unfused(expression).Root);
    }

    // A plan for partial evaluation is not fused whatever the option says.
    [Theory]
    [InlineData("s.f == 1", typeof(CallPlanNode))]
    [InlineData("s.f < 1", typeof(DispatchPlanNode))]
    public static void PartialPlan_IsNotFused(string expression, Type node)
    {
        CelParseResult parsed = Plans.Environment.Parse(expression);
        CheckResult checkedResult = Plans.Environment.Check(parsed.ParsedExpr!);
        Assert.True(checkedResult.IsSuccess, expression);

        PlanNode root = Planner.For(checkedResult.Expression, Plans.Environment.Declarations, null, partial: true, fuse: true).Run().Root;

        Assert.IsType(node, Assert.IsType<RecordPlanNode>(root).Node);
    }

    private static Plan Unfused(string expression)
    {
        CelParseResult parsed = Plans.Environment.Parse(expression);
        CheckResult checkedResult = Plans.Environment.Check(parsed.ParsedExpr!);
        Assert.True(checkedResult.IsSuccess, expression);
        return Planner.Plan(checkedResult.Expression, Plans.Environment.Declarations, fuse: false);
    }

    private static (CelValue Value, ulong Charged) Run(Plan plan, Dictionary<string, CelValue> bindings, ulong? limit)
    {
        Frame frame = plan.CreateFrame(bindings, limit, CancellationToken.None);
        CelValue value = plan.Run(frame);
        return (value, frame.ChargedUnits);
    }

    // Two values are the same: equal, or both the same error with the same code, message and place.
    private static void AssertSame(CelValue expected, CelValue actual)
    {
        if (expected.TryGetError(out CelError? expectedError))
        {
            Assert.True(actual.TryGetError(out CelError? actualError), $"expected the error {expectedError.Code}, got {actual.Kind}");
            Assert.Equal(expectedError.Code, actualError!.Code);
            Assert.Equal(expectedError.Message, actualError.Message);
            Assert.Equal(expectedError.Span, actualError.Span);
            return;
        }

        Assert.Equal(expected, actual);
    }

    // The bindings of s by shape, with the field f: the value f in a map, a map without f, nothing, or an int.
    private static Dictionary<string, CelValue> Bind(int shape, object? field)
    {
        var bindings = new Dictionary<string, CelValue>();
        switch (shape)
        {
            case 0:
                bindings["s"] = CelValue.FromMap(CelMap.Create(new List<KeyValuePair<CelValue, CelValue>> { new(CelValue.FromString("f"), Of(field)) }));
                break;
            case 1:
                bindings["s"] = CelValue.FromMap(CelMap.Create(new List<KeyValuePair<CelValue, CelValue>>()));
                break;
            case 3:
                bindings["s"] = CelValue.FromInt64(5);
                break;
            default:
                break;
        }

        return bindings;
    }

    private static CelValue Of(object? value) => value switch
    {
        null => CelValue.Null,
        long integer => CelValue.FromInt64(integer),
        ulong unsigned => CelValue.FromUInt64(unsigned),
        double real => CelValue.FromDouble(real),
        string text => CelValue.FromString(text),
        bool flag => CelValue.FromBoolean(flag),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}

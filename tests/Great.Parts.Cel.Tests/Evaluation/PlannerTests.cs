using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The shape of plans: the nodes of calls and chains of ||, the slots of variables and the costs.
public sealed class PlannerTests
{
    // The parser builds a chain as a balanced tree; its root is the outermost call.
    [Theory]
    [InlineData("a || b || c || d", 7, "a b c d")]
    [InlineData("a || b || c", 7, "a b c")]
    [InlineData("a || (b || c)", 2, "a b c")]
    [InlineData("(a || b) || (c || d)", 9, "a b c d")]
    public static void OrChain_IsOneNodeWithTheOperandsInTheOrderOfTheText(string expression, int offset, string operands)
    {
        ArgumentNullException.ThrowIfNull(operands);

        var or = Assert.IsType<OrPlanNode>(Plans.Checked(expression).Root);

        Assert.Equal(offset, or.Offset);
        Assert.Equal(operands.Split(' '), or.Operands.Select(operand => Assert.IsType<VariablePlanNode>(operand).Name));
    }

    [Fact]
    public static void OrInsideAnotherNode_IsItsOwnChain()
    {
        var list = Assert.IsType<ListPlanNode>(Plans.Checked("[a || b, c || d || a]").Root);

        Assert.Equal([2, 3], list.Elements.Select(element => Assert.IsType<OrPlanNode>(element).Operands.Length));
    }

    [Fact]
    public static void CallWithOneCandidate_IsBoundToIt()
    {
        var call = Assert.IsType<CallPlanNode>(Plans.Checked("n + 1").Root);

        Assert.Equal("add_int64", call.Overload.Declaration.OverloadId);
        Assert.Equal(2, call.Offset);
    }

    [Fact]
    public static void CallWithSeveralCandidates_DispatchesAmongAllOfThem()
    {
        var dispatch = Assert.IsType<DispatchPlanNode>(Plans.CheckedWith(
            "h(any)",
            Plans.Overload("h", "h_int", CelType.Int, Plans.Constant),
            Plans.Overload("h", "h_string", CelType.String, Plans.Constant)).Root);

        Assert.Equal(["h_int", "h_string"], dispatch.Candidates.Select(candidate => candidate.Declaration.OverloadId));
    }

    [Theory]
    [InlineData("n + n + 0", 1)]
    [InlineData("[n, -n, n / n]", 1)]
    [InlineData("[n, any, flag || flag, any]", 3)]
    [InlineData("7", 0)]
    public static void IdentifiersOfOneVariable_ShareOneSlot(string expression, int slots)
    {
        Assert.Equal(slots, Plans.Checked(expression).SlotCount);
    }

    // The costs of the nodes, charged before their operations: a literal 0, a variable 1, a call the
    // fixed cost of its overload in the table of the library, a chain of || nothing beyond the operands,
    // a list of n elements and a map of n entries 1 + n.
    [Theory]
    [InlineData("7", 0UL)]
    [InlineData("n", 1UL)]
    [InlineData("n + 1", 1UL)]
    [InlineData("-n", 1UL)]
    [InlineData("n / 2", 1UL)]
    [InlineData("flag || flag", 0UL)]
    [InlineData("[]", 1UL)]
    [InlineData("[1, 2, 3]", 4UL)]
    [InlineData("{}", 1UL)]
    [InlineData("{1: 2, 3: 4}", 3UL)]
    public static void Node_HasTheCostOfItsKind(string expression, ulong cost)
    {
        Assert.Equal(cost, Plans.Checked(expression).Root.Cost);
    }

    // One plan evaluated on different bindings, alternately: nothing of one evaluation stays in the plan.
    [Fact]
    public static void Plan_GivesEachEvaluationItsOwnResult()
    {
        Plan plan = Plans.Checked("n + n");

        CelValue[] results = [.. new long[] { 1, 20, 1, 300 }.Select(n => plan.Evaluate(new Dictionary<string, CelValue> { ["n"] = CelValue.FromInt64(n) }))];

        Assert.Equal([CelValue.FromInt64(2), CelValue.FromInt64(40), CelValue.FromInt64(2), CelValue.FromInt64(600)], results);
    }
}

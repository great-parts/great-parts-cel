using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// A list literal whose elements are all literals is one constant of the plan, made once at planning: the evaluation builds no list,
// every evaluation returns the same list, and the charge is what the literal charges, 1 + n for n elements (docs/cost.md, "List").
public sealed class ListLiteralFoldTests
{
    private static readonly IReadOnlyDictionary<string, CelValue> NoBindings = new Dictionary<string, CelValue>();

    [Theory]
    [InlineData("['RUB', 'USD', 'EUR']", 3)]
    [InlineData("[1, 2]", 2)]
    [InlineData("[true]", 1)]
    public static void ListOfLiterals_IsOneConstantThatChargesWhatTheLiteralCharges(string expression, int count)
    {
        Plan plan = Plans.Checked(expression);
        Frame frame = plan.CreateFrame(NoBindings, 100, CancellationToken.None);

        var node = Assert.IsType<ConstantPlanNode>(plan.Root);
        CelValue value = plan.Run(frame);

        Assert.Equal(1UL + (ulong)count, node.Cost);
        Assert.Equal(1UL + (ulong)count, frame.ChargedUnits);
        Assert.True(value.TryGetList(out CelList? list));
        Assert.Equal(count, list.Count);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[n, 1]")]
    [InlineData("[1, n + 1]")]
    [InlineData("[[1], [2]]")]
    public static void ListWithoutElementsOrWithAnElementThatIsNotALiteral_IsBuiltByTheNode(string expression) =>
        Assert.IsType<ListPlanNode>(Plans.Checked(expression).Root);

    [Fact]
    public static void FoldedList_IsTheSameListInEveryEvaluation()
    {
        Plan plan = Plans.Checked("['RUB', 'USD', 'EUR']");

        Assert.True(plan.EvaluateOn(NoBindings).TryGetList(out CelList? first));
        Assert.True(plan.EvaluateOn(NoBindings).TryGetList(out CelList? second));

        Assert.Same(first, second);
        Assert.Equal(CelValue.FromString("RUB"), first[0]);
        Assert.Equal(CelValue.FromString("USD"), first[1]);
        Assert.Equal(CelValue.FromString("EUR"), first[2]);
    }

    // The literals of the call are charged 1 + n each and the calls 1 each: 3 + 1 + 4 + 1 + 1.
    [Fact]
    public static void FoldedLists_InCalls_GiveTheValueAndTheCostOfTheLiterals()
    {
        Plan plan = Plans.Checked("size(['a', 'b']) + size([1, 2, 3])");
        Frame frame = plan.CreateFrame(NoBindings, 100, CancellationToken.None);

        Assert.Equal(CelValue.FromInt64(5), plan.Run(frame));
        Assert.Equal(10UL, frame.ChargedUnits);
    }

    // The charge of the literal is before the evaluation of the call that uses it, as it was for the node that built the list.
    [Fact]
    public static void FoldedList_AboveTheCostLimit_IsCostLimitExceededAtTheLiteral()
    {
        Plan plan = Plans.Checked("['RUB', 'USD', 'EUR']");

        CelError error = Plans.Error(plan.Evaluate(NoBindings, 3, CancellationToken.None));

        Assert.Equal((CelErrorCode.CostLimitExceeded, 0), (error.Code, error.Location.Offset));
    }
}

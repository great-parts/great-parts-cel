using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// cel.@block(slots, result): no text can write it, so the trees are built with NodeFactory, as the composer of expressions that will
// use it builds them, and planned without type checking. A slot is evaluated at its first read of an entry of the block and not more
// than once, a slot nobody reads is not evaluated, an error is a value, an inner block shadows an outer one, a block in the body of a
// comprehension is fresh at each element, and the cost is the slots that are read plus 1 for each read.
public sealed class BlockTests
{
    private static readonly IReadOnlyDictionary<string, CelValue> NoBindings = new Dictionary<string, CelValue>();

    // The text that the trees stand for: long enough for every offset the tests give, which only positions the errors.
    private static readonly string Text = new(' ', 64);

    [Fact]
    public static void Slot_IsEvaluatedOnceHoweverOftenItIsRead()
    {
        int[] calls = [0];
        var nodes = new NodeFactory();
        ExprNode result = Add(nodes, Add(nodes, Slot(nodes, 0), Slot(nodes, 0)), Slot(nodes, 0));
        ExprNode root = Block(nodes, [Call(nodes, "count", Int(nodes, 1))], result);

        CelValue value = Evaluate(root, nodes, calls);

        Assert.Equal(CelValue.FromInt64(126), value);
        Assert.Equal(1, calls[0]);
    }

    [Fact]
    public static void SlotNobodyReads_IsNotEvaluated_AndItsErrorDoesNotHappen()
    {
        int[] calls = [0];
        var nodes = new NodeFactory();
        ExprNode counted = Call(nodes, "count", Int(nodes, 1));
        ExprNode failing = nodes.Call("_/_", null, [Int(nodes, 1), Int(nodes, 0)], 0);
        ExprNode root = Block(nodes, [counted, failing], Int(nodes, 7));

        CelValue value = Evaluate(root, nodes, calls);

        Assert.Equal(CelValue.FromInt64(7), value);
        Assert.Equal(0, calls[0]);
    }

    // A slot may read a slot that comes after it: the order is that of the reads, not of the list.
    [Fact]
    public static void Slot_MayReadALaterSlot()
    {
        var nodes = new NodeFactory();
        ExprNode first = Add(nodes, Slot(nodes, 1), Int(nodes, 1));
        ExprNode root = Block(nodes, [first, Int(nodes, 10)], Slot(nodes, 0));

        Assert.Equal(CelValue.FromInt64(11), Evaluate(root, nodes));
    }

    // A slot that reads itself, directly or through other slots, finds its cell in progress: Unbound for its own name, at the identifier
    // that reads it.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public static void SlotThatReadsItself_IsUnboundForItsOwnName(int slots)
    {
        var nodes = new NodeFactory();
        ExprNode reading = slots == 1 ? nodes.Ident("@index0", 5) : nodes.Ident("@index1", 5);
        ExprNode[] expressions = slots == 1 ? [reading] : [reading, nodes.Ident("@index0", 9)];
        ExprNode root = Block(nodes, expressions, nodes.Ident("@index0", 20));

        CelError error = Plans.Error(Evaluate(root, nodes));

        Assert.Equal(CelErrorCode.Unbound, error.Code);
        Assert.Equal(slots == 1 ? 5 : 9, error.Location.Offset);
        Assert.Equal("no value is bound to variable '@index0'", error.Message);
    }

    // The error of a slot is its value: it is kept, so the slot is not evaluated again, and an operator that absorbs an error takes it.
    [Fact]
    public static void ErrorOfASlot_IsItsValueAndIsKept()
    {
        int[] calls = [0];
        var nodes = new NodeFactory();
        ExprNode failing = nodes.Call("_/_", null, [Call(nodes, "count", Int(nodes, 1)), Int(nodes, 0)], 0);
        ExprNode absorbed = nodes.Call(
            "_||_",
            null,
            [nodes.Call("_||_", null, [Slot(nodes, 0), Slot(nodes, 0)], 0), nodes.Constant(CelValue.FromBoolean(true), 0)],
            0);
        ExprNode root = Block(nodes, [failing], absorbed);

        Assert.Equal(CelValue.FromBoolean(true), Evaluate(root, nodes, calls));
        Assert.Equal(1, calls[0]);
    }

    [Fact]
    public static void ErrorOfASlot_IsTheErrorOfItsReads()
    {
        var nodes = new NodeFactory();
        ExprNode failing = nodes.Call("_/_", null, [Int(nodes, 1), Int(nodes, 0)], 6);
        ExprNode root = Block(nodes, [failing], Add(nodes, Slot(nodes, 0), Slot(nodes, 0)));

        CelError error = Plans.Error(Evaluate(root, nodes));

        Assert.Equal((CelErrorCode.DivisionByZero, 6), (error.Code, error.Location.Offset));
    }

    // The innermost block with more than N slots has @indexN: an inner block of one slot has @index0, and @index1 is the outer block's.
    [Fact]
    public static void InnerBlock_ShadowsTheOuterOneForTheNumbersItHas()
    {
        var nodes = new NodeFactory();
        ExprNode inner = Block(nodes, [Int(nodes, 10)], Add(nodes, Slot(nodes, 0), Slot(nodes, 1)));
        ExprNode root = Block(nodes, [Int(nodes, 1), Int(nodes, 2)], inner);

        Assert.Equal(CelValue.FromInt64(12), Evaluate(root, nodes));
    }

    // A slot expression of the inner block reads the outer block by a number the inner one does not have.
    [Fact]
    public static void SlotOfTheInnerBlock_ReadsTheOuterBlock()
    {
        var nodes = new NodeFactory();
        ExprNode inner = Block(nodes, [Add(nodes, Slot(nodes, 1), Int(nodes, 1))], Slot(nodes, 0));
        ExprNode root = Block(nodes, [Int(nodes, 7), Int(nodes, 8)], inner);

        Assert.Equal(CelValue.FromInt64(9), Evaluate(root, nodes));
    }

    // Each entry of a block starts with its slots unread: a block in the body of a comprehension has the value of each element, not the
    // value of the first.
    [Fact]
    public static void BlockInTheBodyOfAComprehension_IsFreshAtEachElement()
    {
        var nodes = new NodeFactory();
        ExprNode range = nodes.List([Int(nodes, 1), Int(nodes, 2), Int(nodes, 3)], [], 0);
        ExprNode init = nodes.List([], [], 0);
        ExprNode condition = nodes.Constant(CelValue.FromBoolean(true), 0);
        ExprNode slot = nodes.Call("_*_", null, [nodes.Ident("x", 0), Int(nodes, 10)], 0);
        ExprNode body = Block(nodes, [slot], Add(nodes, Slot(nodes, 0), Slot(nodes, 0)));
        ExprNode step = nodes.Call("_+_", null, [nodes.Ident("acc", 0), nodes.List([body], [], 0)], 0);
        ExprNode root = nodes.Comprehension("x", range, "acc", init, condition, step, nodes.Ident("acc", 0), 0);

        CelValue value = Evaluate(root, nodes);

        Assert.True(value.TryGetList(out CelList? list), value.Kind.ToString());
        Assert.Equal([20L, 40L, 60L], Integers(list));
    }

    [Fact]
    public static void EmptyBlock_IsItsResult()
    {
        var nodes = new NodeFactory();
        ExprNode root = Block(nodes, [], Int(nodes, 5));

        Plan plan = PlanOf(root, nodes, []);

        Assert.IsType<ConstantPlanNode>(plan.Root);
        Assert.Equal(CelValue.FromInt64(5), plan.EvaluateOn(NoBindings));
        Assert.Equal((0, 0), (plan.SlotCount, plan.BlockCount));
    }

    // A slot read costs 1 and the first read of a slot also the cost of its expression; the block charges nothing; an unread slot
    // charges nothing.
    [Fact]
    public static void Cost_IsTheSlotOnceAndEachRead()
    {
        const ulong units = 4;
        var nodes = new NodeFactory();
        ExprNode slot = Add(nodes, Int(nodes, 1), Int(nodes, 2));
        ExprNode root = Block(nodes, [slot], Add(nodes, Slot(nodes, 0), Slot(nodes, 0)));
        Plan plan = PlanOf(root, nodes, []);

        Assert.Equal(units, ChargedUnits(plan));
        Assert.Equal(CelValue.FromInt64(6), plan.Evaluate(NoBindings, units, CancellationToken.None));
        Assert.Equal(CelErrorCode.CostLimitExceeded, Plans.Error(plan.Evaluate(NoBindings, units - 1, CancellationToken.None)).Code);
    }

    [Fact]
    public static void Cost_OfASlotNobodyReadsIsZero()
    {
        var nodes = new NodeFactory();
        ExprNode root = Block(nodes, [Add(nodes, Int(nodes, 1), Int(nodes, 2)), Add(nodes, Int(nodes, 3), Int(nodes, 4))], Int(nodes, 5));

        Assert.Equal(0UL, ChargedUnits(PlanOf(root, nodes, [])));
    }

    // The slot that a slot reads is charged at its own first read, inside the first: 1 for the read of slot 0, then slot 0 is the read
    // of slot 1 (1) and the addition (1), and slot 1 is a literal.
    [Fact]
    public static void Cost_OfASlotThatReadsAnotherIsTheReadsAndItsOwnExpression()
    {
        var nodes = new NodeFactory();
        ExprNode first = Add(nodes, Slot(nodes, 1), Int(nodes, 1));
        ExprNode root = Block(nodes, [first, Int(nodes, 10)], Slot(nodes, 0));

        Assert.Equal(3UL, ChargedUnits(PlanOf(root, nodes, [])));
    }

    // Three reads and two additions of a slot that is a literal: 5.
    [Fact]
    public static void Cost_OfEachReadIsOne()
    {
        var nodes = new NodeFactory();
        ExprNode root = Block(nodes, [Int(nodes, 1)], Add(nodes, Add(nodes, Slot(nodes, 0), Slot(nodes, 0)), Slot(nodes, 0)));

        Assert.Equal(5UL, ChargedUnits(PlanOf(root, nodes, [])));
    }

    // A first argument that is not a list literal is no block: the call has no candidate, and its arguments are evaluated first.
    [Fact]
    public static void FirstArgumentThatIsNotAListLiteral_IsEvalNoMatchingOverloadAtTheCall()
    {
        var nodes = new NodeFactory();
        ExprNode root = nodes.Call("cel.@block", null, [Int(nodes, 5), Int(nodes, 6)], 3);

        CelError error = Plans.Error(Evaluate(root, nodes));

        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, 3, "no such overload: cel.@block(int, int)"), (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public static void FirstArgumentThatIsAVariableList_IsEvalNoMatchingOverload()
    {
        var nodes = new NodeFactory();
        ExprNode root = nodes.Call("cel.@block", null, [nodes.Ident("xs", 4), Int(nodes, 6)], 3);
        Dictionary<string, CelValue> bindings = new() { ["xs"] = CelValue.FromList(CelList.Create(CelValue.FromInt64(1))) };

        CelError error = Plans.Error(PlanOf(root, nodes, []).EvaluateOn(bindings));

        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, "no such overload: cel.@block(list, int)"), (error.Code, error.Message));
    }

    // Only @index followed by digits without a sign or a leading zero, with a number below the slot count, is a slot; every other name is an
    // identifier read from the bindings.
    [Theory]
    [InlineData("@index1")]
    [InlineData("@index01")]
    [InlineData("@index-1")]
    [InlineData("@index")]
    [InlineData("@indexa")]
    [InlineData("@index00")]
    [InlineData("@index99999999999")]
    public static void OtherNames_AreOrdinaryIdentifiers(string name)
    {
        var nodes = new NodeFactory();
        ExprNode root = Block(nodes, [Int(nodes, 5)], nodes.Ident(name, 0));
        Dictionary<string, CelValue> bindings = new() { [name] = CelValue.FromInt64(77) };

        Assert.Equal(CelValue.FromInt64(77), PlanOf(root, nodes, []).EvaluateOn(bindings));
    }

    // A slot expression that is read while values wait on the stack of a list literal gets room on the stack: the stack size counts
    // the slots that the reads may evaluate.
    [Fact]
    public static void SlotReadInsideAListLiteral_HasRoomOnTheStack()
    {
        var nodes = new NodeFactory();
        ExprNode slot = nodes.List([Int(nodes, 1), Int(nodes, 2), Int(nodes, 3)], [], 0);
        ExprNode result = nodes.List([Int(nodes, 10), Int(nodes, 20), Int(nodes, 30), Slot(nodes, 0)], [], 0);
        ExprNode root = Block(nodes, [slot], result);

        CelValue value = Evaluate(root, nodes);

        Assert.True(value.TryGetList(out CelList? list), value.Kind.ToString());
        Assert.Equal(4, list.Count);
        Assert.True(list[3].TryGetList(out CelList? inner));
        Assert.Equal([1L, 2L, 3L], Integers(inner));
    }

    // The checker types a block: a list literal as the first argument and the type of the second as the type of the block.
    [Fact]
    public static void CheckedBlock_HasTheTypeOfItsResult_AndIsPlannedAsABlock()
    {
        var nodes = new NodeFactory();
        ExprNode root = Block(nodes, [Add(nodes, Int(nodes, 1), Int(nodes, 2))], nodes.Constant(CelValue.FromString("a"), 0));
        CelEnvironment environment = BlockEnvironment();

        CheckResult checkedResult = environment.Check(nodes.Build(root, new LineTable(Text)));

        Assert.True(checkedResult.IsSuccess, string.Join("; ", checkedResult.Errors));
        Assert.Equal(CelType.String, checkedResult.Expression!.ResultType);
        Plan plan = Planner.Plan(checkedResult.Expression, environment.Declarations);
        Assert.IsType<BlockPlanNode>(plan.Root);
        Assert.Equal(CelValue.FromString("a"), plan.EvaluateOn(NoBindings));
    }

    // A first argument of the type list that is not a list literal passes the types and fails the validator of the overload, with the
    // usual text of the checker and the place of the call.
    [Fact]
    public static void CheckedBlock_WithAListThatIsNotALiteral_IsNoMatchingOverloadAtTheCall()
    {
        var nodes = new NodeFactory();
        ExprNode root = nodes.Call("cel.@block", null, [nodes.Ident("xs", 4), Int(nodes, 1)], 3);

        CheckResult checkedResult = BlockEnvironment().Check(nodes.Build(root, new LineTable(Text)));

        Assert.False(checkedResult.IsSuccess);
        CelError error = Assert.Single(checkedResult.Errors);
        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
        Assert.Equal("found no matching overload for 'cel.@block' applied to '(list(dyn), int)'", error.Message);
        Assert.Equal(3, error.Location.Offset);
    }

    // Names @index0 and the like are not declared: a tree with a slot reference does not pass the checker, which has no rule for them.
    [Fact]
    public static void CheckedBlock_WithASlotReference_IsAnUndeclaredReference()
    {
        var nodes = new NodeFactory();
        ExprNode root = Block(nodes, [Int(nodes, 1)], nodes.Ident("@index0", 12));

        CheckResult checkedResult = BlockEnvironment().Check(nodes.Build(root, new LineTable(Text)));

        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(checkedResult.Errors).Code);
    }

    private static CelEnvironment BlockEnvironment() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Bindings(1))
        .AddVariable("xs", CelType.ListOf(CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    private static CelValue Evaluate(ExprNode root, NodeFactory nodes, int[]? calls = null) =>
        PlanOf(root, nodes, calls ?? [0]).EvaluateOn(NoBindings);

    // The plan of the tree without type checking, with the overload count(x) that counts its calls into the first element of the array.
    private static Plan PlanOf(ExprNode root, NodeFactory nodes, int[] calls)
    {
        OverloadBinding counter = new(
            "count",
            new CelOverloadDecl("count_dyn", false, [CelType.Dyn], CelType.Dyn),
            (ReadOnlySpan<CelValue> _, in CallSite _) =>
            {
                calls[0]++;
                return CelValue.FromInt64(42);
            },
            1,
            null,
            "test");
        DeclarationIndex index = DeclarationIndex.Create([.. StandardLibraryV1.Overloads, counter], []);
        return Planner.PlanWithoutTypeCheck(nodes.Build(root, new LineTable(Text)), index);
    }

    // The units that an evaluation without bindings charged, on a limit that no tree here reaches.
    private static ulong ChargedUnits(Plan plan)
    {
        Frame frame = plan.CreateFrame(NoBindings, 1000, CancellationToken.None);
        CelValue value = plan.Run(frame);
        Assert.NotEqual(CelValueKind.Error, value.Kind);
        return frame.ChargedUnits;
    }

    private static long[] Integers(CelList list)
    {
        long[] items = new long[list.Count];
        for (int i = 0; i < items.Length; i++)
        {
            Assert.True(list[i].TryGetInt64(out items[i]));
        }

        return items;
    }

    private static ConstantNode Int(NodeFactory nodes, long value) => nodes.Constant(CelValue.FromInt64(value), 0);

    private static IdentNode Slot(NodeFactory nodes, int number) => nodes.Ident("@index" + number, 0);

    private static CallNode Add(NodeFactory nodes, ExprNode left, ExprNode right) => nodes.Call("_+_", null, [left, right], 0);

    private static CallNode Call(NodeFactory nodes, string function, ExprNode argument) => nodes.Call(function, null, [argument], 0);

    private static CallNode Block(NodeFactory nodes, ExprNode[] slots, ExprNode result) =>
        nodes.Call("cel.@block", null, [nodes.List([.. slots], [], 0), result], 0);
}

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The slots of cel.@block in type checking: in a call whose first argument is a list literal, @index<N> is a local variable of the
// elements after the N-th and of the second argument, with the type of the N-th element, dyn when that element has an error; the
// slot is not visible in its own element, nor outside the block; an environment that declares @index<N> itself, as the internal set
// block of the runner does, keeps its declaration and the types of version 1. The trees come from the node factory: the text cannot
// write the name of the function.
[Trait("Requirement", "F-31")]
public sealed class BlockSlotTests
{
    private const string Block = "cel.@block";

    private static readonly CelEnvironment Env = TreeEnvironments.Env;

    private static readonly CelEnvironment EnvRunner = BlockExample.Builder()
        .AddVariable("xs", CelType.ListOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    private static CelConstantNode One(CelExprFactory f) => f.Constant(CelValue.FromInt64(1));

    private static CelCallNode Plus(CelExprFactory f, CelExprNode left, CelExprNode right) => f.Call("_+_", left, right);

    private static CelListNode Slots(CelExprFactory f, params CelExprNode[] elements) => f.List(elements);

    private static ImmutableArray<CelError> Errors(CelEnvironment environment, CelParsedExpr tree) => environment.ValidateParsed(tree).Errors;

    [Fact]
    public void SlotsHaveTheTypesOfTheirElements_AndTheBlockEvaluates()
    {
        var f = new CelExprFactory();
        CelIdentNode index0 = f.Ident("@index0");
        CelIdentNode index1 = f.Ident("@index1");
        CelParsedExpr tree = f.Build(f.Call(
            Block,
            Slots(f, One(f), Plus(f, index0, One(f))),
            f.Call("_*_", index1, f.Constant(CelValue.FromInt64(2)))));

        CheckResult checkedResult = Env.Check(tree);

        Assert.True(checkedResult.IsSuccess);
        CheckedExpression checkedExpression = checkedResult.Expression!;
        Assert.Equal(CelType.Int, checkedExpression.Types[index0.Id]);
        Assert.Equal(CelType.Int, checkedExpression.Types[index1.Id]);
        Assert.Equal(CelType.Int, Env.ValidateParsed(tree).ResultType);
        Assert.Equal(4L, Env.CompileParsed<long>(tree).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void SlotOfAString_IsNotDyn()
    {
        var f = new CelExprFactory();
        CelParsedExpr tree = f.Build(f.Call(Block, Slots(f, f.Constant(CelValue.FromString("a"))), Plus(f, f.Ident("@index0"), One(f))));

        CelError error = Assert.Single(Errors(Env, tree));

        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
        Assert.Contains("_+_", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SlotIsVisibleOnlyAfterItsElement()
    {
        var f = new CelExprFactory();
        CelParsedExpr own = f.Build(f.Call(Block, Slots(f, f.Ident("@index0")), One(f)));
        CelParsedExpr beyond = f.Build(f.Call(Block, Slots(f, One(f)), f.Ident("@index1")));

        CelError ownError = Assert.Single(Errors(Env, own));
        CelError beyondError = Assert.Single(Errors(Env, beyond));

        Assert.Equal((CelErrorCode.UndeclaredReference, "@index0"), (ownError.Code, ownError.Subject));
        Assert.Equal((CelErrorCode.UndeclaredReference, "@index1"), (beyondError.Code, beyondError.Subject));
    }

    [Fact]
    public void SlotIsNotVisibleOutsideItsBlock()
    {
        var f = new CelExprFactory();
        CelParsedExpr tree = f.Build(Plus(f, f.Call(Block, Slots(f, One(f)), f.Ident("@index0")), f.Ident("@index0")));

        CelError error = Assert.Single(Errors(Env, tree));

        Assert.Equal((CelErrorCode.UndeclaredReference, "@index0"), (error.Code, error.Subject));
    }

    [Fact]
    public void ElementWithAnError_GivesASlotOfTheTypeDyn_AndNoFurtherError()
    {
        var f = new CelExprFactory();
        CelParsedExpr tree = f.Build(f.Call(Block, Slots(f, f.Ident("nope")), Plus(f, f.Ident("@index0"), One(f))));

        CelError error = Assert.Single(Errors(Env, tree));

        Assert.Equal((CelErrorCode.UndeclaredReference, "nope"), (error.Code, error.Subject));
    }

    [Fact]
    public void InnerBlock_SeesTheSlotsOfTheOuterOne_AndItsOwnSlotShadowsThem()
    {
        var f = new CelExprFactory();
        CelParsedExpr tree = f.Build(f.Call(
            Block,
            Slots(f, One(f)),
            f.Call(Block, Slots(f, Plus(f, f.Ident("@index0"), One(f))), f.Ident("@index0"))));

        Assert.Empty(Errors(Env, tree));
        Assert.Equal(2L, Env.CompileParsed<long>(tree).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The environment of the runner of the cel-spec suite declares @index0 to @index29 of the type dyn: its declaration wins, so the
    // check passes where the typed slot would fail, and the evaluation reports the error, as in 1.0.
    [Fact]
    public void EnvironmentThatDeclaresTheSlots_KeepsItsDeclarations()
    {
        var f = new CelExprFactory();
        CelParsedExpr tree = f.Build(f.Call(Block, Slots(f, f.Constant(CelValue.FromString("a"))), Plus(f, f.Ident("@index0"), One(f))));

        CelCompileResult<CelValue> compiled = EnvRunner.CompileParsed<CelValue>(tree);

        Assert.True(compiled.IsSuccess);
        CelEvalResult<CelValue> result = compiled.GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorStage.Evaluate, result.Errors.Single().Stage);
    }

    // cel-go checks each variable of a block alone and gives it the type of its expression with the free type variables as dyn: the
    // empty list of the first slot does not take the element type of the second slot, so the slot is list(dyn) and @index0 + [1] passes.
    [Fact]
    public void SlotOfAnEmptyList_IsAListOfDyn_AndDoesNotTakeTheTypeOfTheNextSlot()
    {
        var f = new CelExprFactory();
        CelListNode empty = Slots(f);
        CelIdentNode index0 = f.Ident("@index0");
        CelParsedExpr tree = f.Build(f.Call(Block, Slots(f, empty, Slots(f, f.Constant(CelValue.FromString("foo")))), Plus(f, index0, Slots(f, One(f)))));

        CheckResult checkedResult = Env.Check(tree);

        Assert.True(checkedResult.IsSuccess, string.Join("; ", checkedResult.Errors.Select(error => error.Message)));
        CheckedExpression checkedExpression = checkedResult.Expression!;
        Assert.Equal(CelType.ListOf(CelType.Dyn), checkedExpression.Types[index0.Id]);
        Assert.Equal(CelType.ListOf(CelType.Dyn), checkedExpression.Types[empty.Id]);
    }

    [Fact]
    public void SlotOfAListOfAString_KeepsItsType_ForTheSlotThatFollowsAnEmptyList()
    {
        var f = new CelExprFactory();
        CelIdentNode index1 = f.Ident("@index1");
        CelParsedExpr tree = f.Build(f.Call(Block, Slots(f, Slots(f), Slots(f, f.Constant(CelValue.FromString("foo")))), Plus(f, index1, Slots(f, f.Constant(CelValue.FromString("bar"))))));

        CheckResult checkedResult = Env.Check(tree);

        Assert.True(checkedResult.IsSuccess);
        Assert.Equal(CelType.ListOf(CelType.String), checkedResult.Expression!.Types[index1.Id]);
    }

    // The second argument of a block that is a list literal is checked like the list of the slots: the outputs [], ["a"] and [1] of a
    // policy are not joined by their type variables.
    [Fact]
    public void SecondListOfTheBlock_JoinsItsElementsWithoutBindingTheirTypeVariables()
    {
        var f = new CelExprFactory();
        CelListNode empty = Slots(f);
        CelParsedExpr tree = f.Build(f.Call(
            Block,
            Slots(f, f.Constant(CelValue.FromInt64(0))),
            Slots(f, empty, Slots(f, f.Constant(CelValue.FromString("a"))), Slots(f, One(f)))));

        CheckResult checkedResult = Env.Check(tree);

        Assert.True(checkedResult.IsSuccess, string.Join("; ", checkedResult.Errors.Select(error => error.Message)));
        Assert.Equal(CelType.ListOf(CelType.Dyn), checkedResult.Expression!.Types[empty.Id]);
    }

    // An environment that declares the slots keeps the types of version 1: there the empty list of the second argument takes the type of
    // the element that follows it.
    [Fact]
    public void EnvironmentThatDeclaresTheSlots_KeepsTheJoinOfTheTypeVariablesOfTheElements()
    {
        var f = new CelExprFactory();
        CelListNode empty = Slots(f);
        CelParsedExpr tree = f.Build(f.Call(Block, Slots(f, One(f)), Slots(f, empty, Slots(f, f.Constant(CelValue.FromString("a"))))));

        CheckResult checkedResult = EnvRunner.Check(tree);

        Assert.True(checkedResult.IsSuccess);
        Assert.Equal(CelType.ListOf(CelType.String), checkedResult.Expression!.Types[empty.Id]);
    }

    [Fact]
    public void FirstArgumentThatIsNotAListLiteral_IsNoMatchingOverload_AsInVersionOne()
    {
        var f = new CelExprFactory();
        CelParsedExpr withSlot = f.Build(f.Call(Block, f.Ident("xs"), f.Ident("@index0")));
        CelParsedExpr withoutSlot = f.Build(f.Call(Block, f.Ident("xs"), One(f)));

        CelError inRunner = Assert.Single(Errors(EnvRunner, withSlot));
        CelError plain = Assert.Single(Errors(EnvRunner, withoutSlot));
        CelError undeclared = Assert.Single(EnvRunner.ValidateParsed(f.Build(f.Call(Block, f.Ident("nope"), One(f)))).Errors);

        Assert.Equal(CelErrorCode.NoMatchingOverload, inRunner.Code);
        Assert.Equal(CelErrorCode.NoMatchingOverload, plain.Code);
        Assert.Equal(CelErrorCode.UndeclaredReference, undeclared.Code);
    }
}

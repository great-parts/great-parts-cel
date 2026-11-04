using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// A static type is no guarantee of the value: gradual type checking lets a dyn value into a position whose
// type a type parameter, bound by another argument, gives. x + y, with x of type list(int) bound to [1] and
// y of type dyn bound to ['a'], has the type list(int) and the value [1, 'a'], and ([1] + y)[1] the type int
// and the value 'a', as in cel-go v0.32.0. So:
// - every argument of an application function is checked against the declared parameter type at any
//   depth, whatever its static type; a mismatch is EvalNoMatchingOverload, and the code is not called;
// - an implementation of the library checks the kind it reads, and a kind it does not take is
//   EvalNoMatchingOverload, as the run-time guards of the cel-go overloads give;
// - the result of an application function is checked at any depth against the declared result type with
//   its type parameters as dyn, not against the type that type checking gave the call.
// The places are the calls: the "(" after a name, the operator of "+".
public sealed class DeepArgumentAndResultTests
{
    private static readonly CelFunctionImplementation CountOfTheList =
        arguments => CelValue.FromInt64(arguments[0].TryGetList(out CelList? list) ? list.Count : -1);

    private static readonly CelFunctionImplementation FirstElement =
        arguments => arguments[0].TryGetList(out CelList? list) ? list[0] : CelValue.Null;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("x", CelType.ListOf(CelType.Int))
        .AddVariable("y", CelType.Dyn)
        .AddVariable("ld", CelType.ListOf(CelType.Dyn))
        .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)))
        .AddFunctionImplementation<IReadOnlyList<long>, long>("f_list_int", list => list.Count)
        .AddFunction(new CelFunctionDecl("h", CelOverloadDecl.Global("h_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)))
        .AddFunctionImplementation("h_list_int", CountOfTheList)
        .AddFunction(new CelFunctionDecl("g", CelOverloadDecl.Global("g_list_int", [], CelType.ListOf(CelType.Int))))
        .AddFunctionImplementation("g_list_int", _ => CelValue.FromList(CelList.Create(CelValue.FromString("a"))))
        .AddFunction(new CelFunctionDecl("three", CelOverloadDecl.Global("three_list_int", [], CelType.ListOf(CelType.Int))))
        .AddFunctionImplementation("three_list_int", _ => CelValue.FromList(Ints(1, 2, 3)))
        .AddFunction(new CelFunctionDecl(
            "first",
            CelOverloadDecl.Global("first_list", [CelType.ListOf(CelType.TypeParameter("A"))], CelType.TypeParameter("A"))))
        .AddFunctionImplementation("first_list", FirstElement)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("x", Ints(1))
        .Add("y", CelList.Create(CelValue.FromString("a")))
        .Add("ld", CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a")));

    // f has a typed delegate on IReadOnlyList<long>, h a CelFunctionImplementation; both take list(int)
    // and return int. A list with a string fits neither: ld of the static type list(dyn), and x + y of
    // the static type list(int). cel-go v0.32.0 calls the function here: it checks an argument against a
    // list parameter by the first element only. These examples do not rely on it.
    [Theory]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    [InlineData("f(ld)", "no such overload: f(list)", 1)]
    [InlineData("f(x + y)", "no such overload: f(list)", 1)]
    [InlineData("h(ld)", "no such overload: h(list)", 1)]
    [InlineData("h(x + y)", "no such overload: h(list)", 1)]
    public void ArgumentThatDoesNotFitTheParameterAtDepth_IsEvalNoMatchingOverload(string expression, string message, int offset)
    {
        AssertEvalError(expression, CelErrorCode.EvalNoMatchingOverload, message, offset);
    }

    [Fact]
    [Trait("Requirement", "+R-9")]
    public void ArgumentThatFits_ReachesTheCode()
    {
        Assert.Equal(2L, Evaluate<long>("f(x + [2])"));
        Assert.Equal(2L, Evaluate<long>("h(x + [2])"));
    }

    // ([1] + y)[1] holds 'a' under the static type int. cel-go v0.32.0 gives an error for both.
    [Theory]
    [Trait("Requirement", "+R-6")]
    [Trait("Requirement", "F-4")]
    [InlineData("([1] + y)[1] + 1", "no such overload: _+_(string, int)", 13)]
    [InlineData("int(([1] + y)[1])", "no such overload: int(string)", 3)]
    public void ImplementationOfTheLibrary_ChecksTheKindItReads(string expression, string message, int offset)
    {
        AssertEvalError(expression, CelErrorCode.EvalNoMatchingOverload, message, offset);
    }

    [Fact]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void ResultThatDoesNotFitTheDeclaredTypeAtDepth_IsImplementationResultMismatch()
    {
        AssertEvalError(
            "g()",
            CelErrorCode.ImplementationResultMismatch,
            "implementation of overload 'g_list_int' of function 'g' returned a value of type 'list(string)', expected 'list(int)'",
            1);
    }

    // first(list(A)) -> A: the result is checked against A as dyn. The call first(dyn(['a']) + [1]) has the
    // type int and rightly returns 'a', as in cel-go v0.32.0.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void ResultOfATypeParameter_IsCheckedAsDyn()
    {
        Assert.True(Evaluate<CelValue>("first([1])").TryGetInt64(out long number));
        Assert.Equal(1L, number);
        Assert.True(Evaluate<CelValue>("first(dyn(['a']) + [1])").TryGetString(out string? text));
        Assert.Equal("a", text);
    }

    // With a cost limit each check charges n before it descends into n elements, once: the result of
    // three(), [1, 2, 3], costs the call 1 and the check 3; f([1, 2, 3]) and h([1, 2, 3]) the literal 4,
    // the check of the argument 3 and the call 1. The lazy view of f's argument is not charged: int to long
    // cannot fail.
    [Theory]
    [Trait("Requirement", "R-3")]
    [InlineData("three()", 4UL)]
    [InlineData("f([1, 2, 3])", 8UL)]
    [InlineData("h([1, 2, 3])", 8UL)]
    public void CheckOfAnArgumentOrAResult_ChargesTheElementsItDescendsInto(string expression, ulong cost)
    {
        Assert.True(Run<CelValue>(expression, cost).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Run<CelValue>(expression, cost - 1).Errors).Code);
    }

    private static CelList Ints(params long[] values) => CelList.Create(Array.ConvertAll(values, CelValue.FromInt64));

    private CelEvalResult<T> Run<T>(string expression, ulong? limit) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);

    private T Evaluate<T>(string expression) => Run<T>(expression, null).GetValueOrThrow();

    private void AssertEvalError(string expression, CelErrorCode code, string message, int offset)
    {
        CelError error = Assert.Single(Run<CelValue>(expression, null).Errors);

        Assert.Equal((code, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal(message, error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }
}

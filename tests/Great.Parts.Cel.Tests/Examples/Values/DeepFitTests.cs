using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// Whether a value fits a declared type below the top level, seen through bindings. list(E) takes a list
// whose every element fits E, and map(K, V) a map whose every key fits K and every value fits V; the check
// descends only into a parameter that is neither dyn nor a type parameter. On a mismatch the message of
// BindingTypeMismatch names the checked type with the parameter at the path of the first element, key or
// value that does not fit replaced by the name of its type. An error value fits no type, dyn included, and
// no list or map holds one. With a cost limit the check charges n units before it descends into a list of
// n elements or a map of n entries. cel-go does not check bindings against declarations, so these examples
// do not rely on it.
public sealed class DeepFitTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("li", CelType.ListOf(CelType.Int))
        .AddVariable("ld", CelType.ListOf(CelType.Dyn))
        .AddVariable("lli", CelType.ListOf(CelType.ListOf(CelType.Int)))
        .AddVariable("mk", CelType.MapOf(CelType.Int, CelType.Dyn))
        .AddVariable("ms", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddVariable("mll", CelType.MapOf(CelType.String, CelType.ListOf(CelType.Int)))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // li = [1, 'a'], mk = {'a': 1}, mll = {'a': ['b']}. The first read fails whatever the expression does
    // with the value: li[0] reads an element that fits.
    [Theory]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    [InlineData("size(li)", "li", 5, "list(string)", "list(int)")]
    [InlineData("li[0]", "li", 0, "list(string)", "list(int)")]
    [InlineData("size(mk)", "mk", 5, "map(string, dyn)", "map(int, dyn)")]
    [InlineData("size(mll)", "mll", 5, "map(string, list(string))", "map(string, list(int))")]
    public void CollectionWithAPartThatDoesNotFit_IsBindingTypeMismatchNamingThatPart(
        string expression, string name, int offset, string actual, string declared)
    {
        var bindings = new CelBindings()
            .Add("li", List(CelValue.FromInt64(1), CelValue.FromString("a")))
            .Add("mk", Map(CelValue.FromString("a"), CelValue.FromInt64(1)))
            .Add("mll", Map(CelValue.FromString("a"), CelValue.FromList(List(CelValue.FromString("b")))));

        CelError error = Assert.Single(Run(expression, bindings, null).Errors);

        Assert.Equal((CelErrorCode.BindingTypeMismatch, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal(
            "value of variable '" + name + "' has type '" + actual + "', which does not match the declared type '" + declared + "'",
            error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void CollectionWhosePartsFit_IsRead()
    {
        var bindings = new CelBindings()
            .Add("li", Ints(1, 2, 3))
            .Add("lli", List(CelValue.FromList(Ints(1)), CelValue.FromList(Ints(2, 3))))
            .Add("mll", Map(CelValue.FromString("a"), CelValue.FromList(Ints(4))));

        Assert.Equal(10L, environment.Compile<long>("li[2] + lli[1][1] + mll['a'][0]").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The value of 1 / 0 is an error value; bound to a variable of the type dyn it is BindingTypeMismatch,
    // named error as in the messages of EvalNoMatchingOverload, and never the error of the division.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void ErrorValue_FitsNoTypeDynIncluded()
    {
        CelValue division = environment.Compile<CelValue>("1 / 0").GetProgramOrThrow()
            .Plan.Evaluate(new Dictionary<string, CelValue>(), null, CancellationToken.None);
        Assert.True(division.TryGetError(out _));

        CelError error = Assert.Single(Run("d", new CelBindings().Add("d", division), null).Errors);

        Assert.Equal((CelErrorCode.BindingTypeMismatch, 0), (error.Code, error.Location.Offset));
        Assert.Equal("value of variable 'd' has type 'error', which does not match the declared type 'dyn'", error.Message);
    }

    // An error is a result, not a value of a CEL type: a list or a map built by the application does not
    // hold one.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void CreateWithAnErrorValue_ThrowsArgumentException()
    {
        CelValue division = environment.Compile<CelValue>("1 / 0").GetProgramOrThrow()
            .Plan.Evaluate(new Dictionary<string, CelValue>(), null, CancellationToken.None);

        Assert.Throws<ArgumentException>(() => CelList.Create(CelValue.FromInt64(1), division));
        Assert.Throws<ArgumentException>(() => CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), division)]));
    }

    // The read of a variable costs 1, and the check of its binding n before each descent: li = ld = [1, 2, 3],
    // lli = [[1], [2, 3]] (2 + 1 + 2), ms = {'a': 1, 'b': 2} (the keys only). A total equal to the limit
    // is allowed; one unit less is CostLimitExceeded.
    [Theory]
    [Trait("Requirement", "R-3")]
    [InlineData("li", 4UL)]
    [InlineData("ld", 1UL)]
    [InlineData("lli", 6UL)]
    [InlineData("ms", 3UL)]
    public void CheckOfABinding_ChargesTheElementsItDescendsInto(string expression, ulong cost)
    {
        var bindings = new CelBindings()
            .Add("li", Ints(1, 2, 3))
            .Add("ld", Ints(1, 2, 3))
            .Add("lli", List(CelValue.FromList(Ints(1)), CelValue.FromList(Ints(2, 3))))
            .Add("ms", CelMap.Create([
                new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromInt64(1)),
                new KeyValuePair<CelValue, CelValue>(CelValue.FromString("b"), CelValue.FromInt64(2)),
            ]));

        Assert.True(Run(expression, bindings, cost).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Run(expression, bindings, cost - 1).Errors).Code);
    }

    private static CelList List(params CelValue[] items) => CelList.Create(items);

    private static CelList Ints(params long[] values) => CelList.Create(Array.ConvertAll(values, CelValue.FromInt64));

    private static CelMap Map(CelValue key, CelValue value) => CelMap.Create([new KeyValuePair<CelValue, CelValue>(key, value)]);

    private CelEvalResult<CelValue> Run(string expression, CelBindings bindings, ulong? limit) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
}

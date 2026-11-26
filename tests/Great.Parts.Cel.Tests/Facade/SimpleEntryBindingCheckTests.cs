using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The check of a binding at the first read of a variable in the hidden environments of the simple entry. The environment of a
// registered anonymous type is built with the mark of bindings that created code writes and checks the top level only; the
// environment of a dictionary, like an environment that an application builds, checks at any depth and charges the descent.
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryBindingCheckTests
{
    private const string Text = "size(xs) == 2";

    [Fact]
    public void EnvironmentOfAnAnonymousType_HasTheMark_AndTheEnvironmentOfADictionaryHasNot()
    {
        var data = new { markedValues = new List<long> { 1, 2 } };
        Assert.Equal(2L, CelExpression.Evaluate<long>("size(markedValues)", data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(AnonymousRegistry.TryGet(data.GetType(), out AnonymousRegistry.Entry? entry));
        var cache = new SimpleEntryCache();

        Assert.True(cache.GetEnvironment(entry).Environment.Declarations.GeneratedBindings);

        var variables = new Dictionary<string, object?> { ["x"] = 1L };
        Assert.Equal(1L, SimpleEntryCall.Evaluate<long>(cache, "x", variables, CelExpression.DefaultCostLimit, TestContext.Current.CancellationToken));
        ShapeSlot[] slots = [new ShapeSlot { Name = "x", Type = CelType.Int }];
        Assert.True(cache.TryGetEnvironment(new ShapeProbe(slots, 1), out SimpleEntryCache.EnvironmentEntry? dictionary));
        Assert.False(dictionary.Environment.Declarations.GeneratedBindings);
    }

    // Without the mark a list whose element does not fit list(int) is BindingTypeMismatch at the identifier, and a list that fits
    // costs one unit for each element more than with the mark; with the mark only the top level is checked.
    [Fact]
    public void BindingCheck_IsAtAnyDepthWithoutTheMark_AndAtTheTopLevelWithIt()
    {
        CelEnvironment deep = ListEnvironment(generatedBindings: false);
        CelEnvironment top = ListEnvironment(generatedBindings: true);
        CelBindings fitting = new CelBindings().Add("xs", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2)));
        CelBindings mixed = new CelBindings().Add("xs", CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a")));
        CelBindings notAList = new CelBindings().Add("xs", "a");

        CelEvalResult<bool> deepFitting = Run(deep, fitting);
        CelEvalResult<bool> topFitting = Run(top, fitting);
        Assert.True(deepFitting.GetValueOrThrow());
        Assert.True(topFitting.GetValueOrThrow());
        Assert.Equal(topFitting.Cost + 2, deepFitting.Cost);

        CelError mismatch = Assert.Single(Run(deep, mixed).Errors);
        Assert.Equal((CelErrorCode.BindingTypeMismatch, Text.IndexOf("xs", StringComparison.Ordinal)), (mismatch.Code, mismatch.Location.Offset));
        Assert.True(Run(top, mixed).GetValueOrThrow());
        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(Run(top, notAList).Errors).Code);
    }

    private static CelEnvironment ListEnvironment(bool generatedBindings) =>
        new CelEnvironmentBuilder { GeneratedBindings = generatedBindings }
            .AddVariable("xs", CelType.ListOf(CelType.Int))
            .Build()
            .GetEnvironmentOrThrow();

    private static CelEvalResult<bool> Run(CelEnvironment environment, CelBindings bindings) =>
        environment.Compile<bool>(Text).GetProgramOrThrow().Evaluate(bindings, new CelEvalOptions { CostLimit = 1_000 }, TestContext.Current.CancellationToken);
}

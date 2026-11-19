using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// The simple entry with anonymous objects registered by the generator of call sites: the project of these tests runs the
// generator, so each anonymous object passed to CelExpression.Evaluate here is registered by a module initializer before any
// test runs, under the JIT and under Native AOT. The types of the properties are those of docs/type-mapping.md, "Data of the simple entry".
[Trait("Requirement", "U-7")]
public sealed class AnonymousDataTests
{
    [Fact]
    public void AnonymousObject_IsEvaluated()
    {
        Assert.Equal(14L, CelExpression.Evaluate<long>("(x + 2) / 3", new { x = 40L }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PropertiesOfTheList_AreVariablesOfTheirTypes()
    {
        var data = new
        {
            count = 3,
            price = 2.5,
            name = "abc",
            discount = (double?)null,
            when = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            wait = TimeSpan.FromMinutes(90),
            ids = new List<long> { 1, 2, 3 },
            prices = new Dictionary<string, double> { ["a"] = 1.5 },
        };

        Assert.True(CelExpression.Evaluate<bool>(
            "count == 3 && price * 2.0 == 5.0 && name.endsWith('c') && discount == null && when.getFullYear() == 2026"
            + " && wait == duration('90m') && ids.exists(i, i == 2) && prices['a'] == 1.5",
            data,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // A wrapper sees only its type parameter, so the generator warns there (GPCEL010); the call works because the same anonymous
    // type is written at a direct call of this assembly, in AnonymousObject_IsEvaluated.
    [Fact]
    public void Wrapper_WorksWhenTheTypeIsWrittenAtADirectCall()
    {
        Assert.Equal(14L, Wrap(new { x = 40L }));
    }

    // A type that no direct call writes: the name the compiler gave and the advice to pass it directly.
    [Fact]
    public void Wrapper_WithATypeOfNoDirectCall_IsTypeNotDeclared()
    {
        CelException exception = Assert.Throws<CelException>(() => Wrap(new { onlyInTheWrapper = 1L }));

        CelError error = Assert.Single(exception.Errors);
        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.StartsWith("<>f__AnonymousType", error.Subject, StringComparison.Ordinal);
    }

#pragma warning disable GPCEL010 // the wrapper is the subject of the two rows above
    private static long Wrap<TData>(TData data)
        where TData : notnull => CelExpression.Evaluate<long>("(x + 2) / 3", data);
#pragma warning restore GPCEL010
}

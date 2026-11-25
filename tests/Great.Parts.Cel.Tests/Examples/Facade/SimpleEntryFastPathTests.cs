using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The fast paths of the cache of the simple entry: the same text in
// another string, a call after its environment or its program was evicted, a call after a failed call, and a call of the simple
// entry from inside an evaluation of the simple entry on the same thread. What the examples fix is the value: the fast paths are
// not visible to the application, and the cache gives the same value with and without them.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "R-4")]
public sealed class SimpleEntryFastPathTests
{
    // The literal of the call site is one string; a text equal to it in another instance finds the same program by its value.
    [Fact]
    public void SameTextInAnotherString_GivesTheSameValue()
    {
        const string Text = "x * 3 + 1";
        string copy = new(Text.AsSpan());
        Assert.False(ReferenceEquals(Text, copy));

        Assert.Equal(31L, CelExpression.Evaluate<long>(Text, new { x = 10L }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(31L, CelExpression.Evaluate<long>(copy, new { x = 10L }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(31L, CelExpression.Evaluate<long>(Text, new { x = 10L }, cancellationToken: TestContext.Current.CancellationToken));
    }

    // More than 64 shapes of data: the environments of the first ones are evicted, and a call with an evicted shape is evaluated
    // again with the right value.
    [Fact]
    public void CallAfterManyOtherShapes_IsEvaluatedAgain()
    {
        var data = new { first = 7L };
        Assert.Equal(8L, CelExpression.Evaluate<long>("first + 1", data, cancellationToken: TestContext.Current.CancellationToken));

        for (int i = 0; i < 100; i++)
        {
            string name = "v" + i.ToString(CultureInfo.InvariantCulture);
            var shape = new Dictionary<string, object?> { [name] = (long)i };
            Assert.Equal((long)i, CelExpression.Evaluate<long>(name, shape, cancellationToken: TestContext.Current.CancellationToken));
        }

        Assert.Equal(8L, CelExpression.Evaluate<long>("first + 1", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    // More than 1 024 texts for one shape: the programs of the first ones are evicted, and the first text gives its value again.
    [Fact]
    public void CallAfterManyOtherTexts_IsEvaluatedAgain()
    {
        var data = new { n = 5L };
        Assert.Equal(6L, CelExpression.Evaluate<long>("n + 1", data, cancellationToken: TestContext.Current.CancellationToken));

        for (int i = 0; i < 1_100; i++)
        {
            string text = "n + " + i.ToString(CultureInfo.InvariantCulture) + " * 2";
            Assert.Equal(5L + (i * 2), CelExpression.Evaluate<long>(text, data, cancellationToken: TestContext.Current.CancellationToken));
        }

        Assert.Equal(6L, CelExpression.Evaluate<long>("n + 1", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    // A failed evaluation leaves nothing behind: the next call on the thread has its own bindings.
    [Fact]
    public void CallAfterAFailedCall_GivesItsValue()
    {
        Assert.Throws<CelException>(() => CelExpression.Evaluate<long>("x / 0", new { x = 1L }, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(2L, CelExpression.Evaluate<long>("x * 2", new { x = 1L }, cancellationToken: TestContext.Current.CancellationToken));
    }

    // The getter of a member calls the simple entry while the outer evaluation reads that member: the inner call gets bindings of
    // its own, and the outer call goes on with its own.
    [Fact]
    public void CallFromInsideAnEvaluation_GivesBothValues()
    {
        var data = new { row = new ReentrantRow { Base = 21 }, extra = 1L };

        Assert.Equal(43L, CelExpression.Evaluate<long>("row.doubled + extra", data, cancellationToken: TestContext.Current.CancellationToken));
    }
}

// A class whose member evaluates an expression of its own through the simple entry.
internal sealed class ReentrantRow
{
    public long Base { get; init; }

    public long Doubled => CelExpression.Evaluate<long>("x * 2", new { x = Base });
}

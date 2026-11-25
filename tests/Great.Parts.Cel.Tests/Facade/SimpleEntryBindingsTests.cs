using System;
using System.Collections.Generic;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The bindings of a call of the simple entry are one instance for each thread: the call takes it out of the field of the thread, fills
// it, evaluates, and puts it back cleared in a finally block. These tests read the field through the internal member of the entry.
// A call that finds the field empty — a call inside a call on the same thread — makes an instance of its own.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "R-4")]
public sealed class SimpleEntryBindingsTests
{
    private static long Run(SimpleEntryCache cache, string text, object data) =>
        SimpleEntryCall.Evaluate<long>(cache, text, data, CelExpression.DefaultCostLimit, TestContext.Current.CancellationToken);

    [Fact]
    public void AfterACall_TheBindingsOfTheThreadAreKeptEmpty()
    {
        SimpleEntryFixtures.Register(
            new { keptValue = default(long) },
            static builder => builder.AddVariable<long>("keptValue"),
            static (data, bindings) => bindings.Add("keptValue", data.keptValue));
        var cache = new SimpleEntryCache();

        Assert.Equal(8L, Run(cache, "keptValue + 1", new { keptValue = 7L }));

        CelBindings kept = Assert.IsType<CelBindings>(SimpleEntryCall.ThreadBindings);
        Assert.Empty(kept.Values);
    }

    [Fact]
    public void DuringTheBinding_TheFieldOfTheThreadIsEmpty()
    {
        CelBindings? observed = null;
        bool bound = false;
        SimpleEntryFixtures.Register(
            new { fieldValue = default(long) },
            static builder => builder.AddVariable<long>("fieldValue"),
            (data, bindings) =>
            {
                observed = SimpleEntryCall.ThreadBindings;
                bound = true;
                bindings.Add("fieldValue", data.fieldValue);
            });
        var cache = new SimpleEntryCache();

        // The first call makes the instance of the thread; the second one takes it out.
        Assert.Equal(2L, Run(cache, "fieldValue + 1", new { fieldValue = 1L }));
        Assert.Equal(3L, Run(cache, "fieldValue + 1", new { fieldValue = 2L }));

        Assert.True(bound);
        Assert.Null(observed);
    }

    [Fact]
    public void CallAfterAFailedEvaluation_FindsTheBindingsClearedAndGivesItsValue()
    {
        SimpleEntryFixtures.Register(
            new { failingValue = default(long) },
            static builder => builder.AddVariable<long>("failingValue"),
            static (data, bindings) => bindings.Add("failingValue", data.failingValue));
        var cache = new SimpleEntryCache();

        Assert.Throws<CelException>(() => Run(cache, "failingValue / 0", new { failingValue = 5L }));

        CelBindings kept = Assert.IsType<CelBindings>(SimpleEntryCall.ThreadBindings);
        Assert.Empty(kept.Values);
        Assert.Equal(10L, Run(cache, "failingValue * 2", new { failingValue = 5L }));
    }

    // The binding adds one value and then throws: the value is cleared with the instance, and the next call is not affected.
    [Fact]
    public void ExceptionOfTheBinding_ClearsTheValuesAlreadyBound()
    {
        bool fail = true;
        SimpleEntryFixtures.Register(
            new { halfBound = default(long), otherHalf = default(long) },
            static builder => builder.AddVariable<long>("halfBound").AddVariable<long>("otherHalf"),
            (data, bindings) =>
            {
                bindings.Add("halfBound", data.halfBound);
                if (fail)
                {
                    throw new InvalidOperationException("half");
                }

                bindings.Add("otherHalf", data.otherHalf);
            });
        var cache = new SimpleEntryCache();

        Assert.Equal("half", Assert.Throws<InvalidOperationException>(() => Run(cache, "halfBound + otherHalf", new { halfBound = 1L, otherHalf = 2L })).Message);
        Assert.Empty(Assert.IsType<CelBindings>(SimpleEntryCall.ThreadBindings).Values);

        fail = false;
        Assert.Equal(3L, Run(cache, "halfBound + otherHalf", new { halfBound = 1L, otherHalf = 2L }));
    }

    // A call inside the binding of another call, on the same thread: the inner call finds the field empty and makes its own instance, the
    // outer call goes on with the instance it took, and the instance that the outer call puts back at the end is its own.
    [Fact]
    public void CallInsideACall_HasBindingsOfItsOwn()
    {
        var cache = new SimpleEntryCache();
        CelBindings? outerBindings = null;
        CelBindings? innerBindings = null;
        long innerResult = 0;
        SimpleEntryFixtures.Register(
            new { innerValue = default(long) },
            static builder => builder.AddVariable<long>("innerValue"),
            (data, bindings) =>
            {
                innerBindings = bindings;
                bindings.Add("innerValue", data.innerValue);
            });
        SimpleEntryFixtures.Register(
            new { outerValue = default(long) },
            static builder => builder.AddVariable<long>("outerValue"),
            (data, bindings) =>
            {
                outerBindings = bindings;
                innerResult = Run(cache, "innerValue * 2", new { innerValue = 20L });
                bindings.Add("outerValue", data.outerValue);
            });

        Assert.Equal(41L, Run(cache, "outerValue + 1", new { outerValue = 40L }));

        Assert.Equal(40L, innerResult);
        Assert.NotNull(outerBindings);
        Assert.NotNull(innerBindings);
        Assert.NotSame(outerBindings, innerBindings);
        Assert.Same(outerBindings, SimpleEntryCall.ThreadBindings);
        Assert.Empty(outerBindings.Values);
    }

    [Fact]
    public void DictionaryCall_LeavesTheBindingsOfTheThreadEmpty()
    {
        var cache = new SimpleEntryCache();
        var variables = new Dictionary<string, object?> { ["a"] = 1L, ["b"] = 2L };

        Assert.Equal(3L, SimpleEntryCall.Evaluate<long>(cache, "a + b", variables, CelExpression.DefaultCostLimit, TestContext.Current.CancellationToken));

        Assert.Empty(Assert.IsType<CelBindings>(SimpleEntryCall.ThreadBindings).Values);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The two levels of the cache of the simple entry: the capacities 64, 1 024 and 100 000, eviction by the clock at insertion, an
// environment that takes its programs with it, failed compilations kept, a long text not kept, and calls from 64 threads on different
// shapes of data. A cache of its own gives each test its own counts; the cache of the process is used only by the thread test and the
// registry rows.
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryCacheTests
{
    private static long Eval(SimpleEntryCache cache, string text, IReadOnlyDictionary<string, object?> variables) =>
        SimpleEntryCall.Evaluate<long>(cache, text, variables, CelExpression.DefaultCostLimit, CancellationToken.None);

    // The shape number `shape`: one variable whose name is the number, so that every number is a shape of its own.
    private static Dictionary<string, object?> Shape(int shape) =>
        new() { ["v" + shape.ToString(CultureInfo.InvariantCulture)] = 1L };

    [Fact]
    public void Environments_AreKeptUpTo64()
    {
        var cache = new SimpleEntryCache();

        for (int shape = 0; shape < 64; shape++)
        {
            Assert.Equal(1L, Eval(cache, "v" + shape.ToString(CultureInfo.InvariantCulture), Shape(shape)));
        }

        Assert.Equal(64, cache.Kept.Environments);
        Assert.Equal(1L, Eval(cache, "v64", Shape(64)));
        Assert.Equal(64, cache.Kept.Environments);
        for (int shape = 65; shape < 300; shape++)
        {
            Assert.Equal(1L, Eval(cache, "v" + shape.ToString(CultureInfo.InvariantCulture), Shape(shape)));
        }

        Assert.Equal(64, cache.Kept.Environments);
        Assert.Equal(64, cache.Kept.Programs);
    }

    [Fact]
    public void Programs_AreKeptUpTo1024()
    {
        var cache = new SimpleEntryCache();
        var variables = new Dictionary<string, object?> { ["x"] = 1L };

        for (int text = 0; text < 1_024; text++)
        {
            Assert.Equal(1L + text, Eval(cache, "x + " + text.ToString(CultureInfo.InvariantCulture), variables));
        }

        Assert.Equal((1, 1_024), (cache.Kept.Environments, cache.Kept.Programs));
        for (int text = 1_024; text < 1_500; text++)
        {
            Assert.Equal(1L + text, Eval(cache, "x + " + text.ToString(CultureInfo.InvariantCulture), variables));
        }

        Assert.Equal(1_024, cache.Kept.Programs);
        Assert.True(cache.Kept.TotalTextLength <= 100_000);
    }

    [Fact]
    public void TotalTextLength_IsKeptUpTo100000()
    {
        var cache = new SimpleEntryCache();
        var variables = new Dictionary<string, object?> { ["x"] = 1L };

        // 30 distinct texts of 5 000 code units are 150 000: the cache holds the 20 that fit.
        for (int text = 0; text < 30; text++)
        {
            string padded = "x + " + text.ToString(CultureInfo.InvariantCulture) + new string(' ', 5_000 - 4 - text.ToString(CultureInfo.InvariantCulture).Length);
            Assert.Equal(5_000, padded.Length);
            Assert.Equal(1L + text, Eval(cache, padded, variables));
        }

        Assert.Equal(20, cache.Kept.Programs);
        Assert.Equal(100_000, cache.Kept.TotalTextLength);
    }

    [Fact]
    public void TextLongerThanTheCapacity_IsEvaluatedAndNotKept()
    {
        var cache = new SimpleEntryCache(64, 1_024, 10);
        var variables = new Dictionary<string, object?> { ["x"] = 1L };

        Assert.Equal(2L, Eval(cache, "x + 1     ", variables));
        Assert.Equal((1, 10L), (cache.Kept.Programs, cache.Kept.TotalTextLength));
        Assert.Equal(2L, Eval(cache, "x + 1      ", variables));
        Assert.Equal((1, 10L), (cache.Kept.Programs, cache.Kept.TotalTextLength));
        Assert.Equal(3L, Eval(cache, "x + 2", variables));
        Assert.Equal((1, 5L), (cache.Kept.Programs, cache.Kept.TotalTextLength));

        // At the limit of the length of a text of the parser, a text over the capacity of the cache is already an error.
        var tooLong = new string(' ', 100_001) + "1";
        var shared = new SimpleEntryCache();
        CelException exception = Assert.Throws<CelException>(() => Eval(shared, tooLong, variables));
        Assert.Equal(CelErrorCode.ExpressionTooLong, Assert.Single(exception.Errors).Code);
        Assert.Equal(0, shared.Kept.Programs);
    }

    [Fact]
    public void FailedCompilation_IsKept()
    {
        var cache = new SimpleEntryCache();
        var variables = new Dictionary<string, object?> { ["x"] = 1L };

        CelException first = Assert.Throws<CelException>(() => Eval(cache, "x +", variables));
        CelException second = Assert.Throws<CelException>(() => Eval(cache, "x +", variables));

        Assert.Equal(1, cache.Kept.Programs);
        Assert.Equal(first.Errors.Length, second.Errors.Length);
        Assert.Equal(first.Errors[0].Message, second.Errors[0].Message);
    }

    [Fact]
    public void ResultType_IsPartOfTheKey()
    {
        var cache = new SimpleEntryCache();
        var variables = new Dictionary<string, object?> { ["x"] = 1L };

        Assert.Equal(2L, Eval(cache, "x + 1", variables));
        Assert.Equal((object)2L, SimpleEntryCall.Evaluate<object>(cache, "x + 1", variables, CelExpression.DefaultCostLimit, CancellationToken.None));
        Assert.Equal(2, cache.Kept.Programs);
    }

    [Fact]
    public void ShapeOfADictionary_DoesNotDependOnTheOrderOfItsKeys()
    {
        var cache = new SimpleEntryCache();
        var forward = new Dictionary<string, object?> { ["a"] = 1L, ["b"] = 2L };
        var backward = new Dictionary<string, object?> { ["b"] = 2L, ["a"] = 1L };

        Assert.Equal(3L, Eval(cache, "a + b", forward));
        Assert.Equal(3L, Eval(cache, "a + b", backward));

        Assert.Equal((1, 1), (cache.Kept.Environments, cache.Kept.Programs));
    }

    [Fact]
    public void ShapeOfADictionary_IsTheNamesAndTheTypesOfItsValues()
    {
        var cache = new SimpleEntryCache();

        Assert.Equal(3L, Eval(cache, "a + b", new Dictionary<string, object?> { ["a"] = 1L, ["b"] = 2L }));
        Assert.Equal(3L, Eval(cache, "a + b", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }));
        Assert.Equal(1, cache.Kept.Environments);

        CelException wrongType = Assert.Throws<CelException>(() => Eval(cache, "a + b", new Dictionary<string, object?> { ["a"] = 1L, ["b"] = 2.0 }));
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(wrongType.Errors).Code);
        Assert.Equal(2, cache.Kept.Environments);
    }

    [Fact]
    public void ShapeOfADictionary_FailedBuild_IsNotKept()
    {
        var cache = new SimpleEntryCache();

        Assert.Throws<CelException>(() => Eval(cache, "1", new Dictionary<string, object?> { ["a-b"] = 1L }));
        Assert.Equal(0, cache.Kept.Environments);
    }

    [Fact]
    public void EvictedEnvironment_TakesItsProgramsWithIt()
    {
        var cache = new SimpleEntryCache(2, 1_024, 100_000);
        Dictionary<string, object?> a = Shape(1);
        Dictionary<string, object?> b = Shape(2);
        Dictionary<string, object?> c = Shape(3);

        // The hits mark a: the hand clears the mark and passes it, and removes b, which nobody asked for again.
        Eval(cache, "v1", a);
        Eval(cache, "v1 + 1", a);
        Eval(cache, "v1 + 2", a);
        Eval(cache, "v2", b);
        Assert.Equal((2, 4), (cache.Kept.Environments, cache.Kept.Programs));

        Eval(cache, "v3", c);

        // The programs of b went with it: 3 of a and 1 of c, not 5.
        Assert.Equal((2, 4), (cache.Kept.Environments, cache.Kept.Programs));
        Eval(cache, "v2", b);
        Assert.Equal(2, cache.Kept.Environments);
    }

    [Fact]
    public void RegisteredAnonymousType_HasOneEnvironment()
    {
        CelAnonymousData.Register(
            new { cacheTestValue = default(long) },
            static builder => builder.AddVariable<long>("cacheTestValue"),
            static (data, bindings) => bindings.Add("cacheTestValue", data.cacheTestValue));
        var cache = new SimpleEntryCache();

        for (int call = 0; call < 5; call++)
        {
            object data = new { cacheTestValue = (long)call };
            Assert.Equal(call + 1L, SimpleEntryCall.Evaluate<long>(cache, "cacheTestValue + 1", data, CelExpression.DefaultCostLimit, CancellationToken.None));
        }

        Assert.Equal((1, 1), (cache.Kept.Environments, cache.Kept.Programs));
    }

    [Fact]
    public void RegistrationOfTheSameTypeTwice_KeepsTheFirst()
    {
        CelAnonymousData.Register(
            new { twiceValue = default(long) },
            static builder => builder.AddVariable<long>("twiceValue"),
            static (data, bindings) => bindings.Add("twiceValue", data.twiceValue));
        CelAnonymousData.Register(
            new { twiceValue = default(long) },
            static builder => builder.AddVariable<string>("other"),
            static (data, bindings) => bindings.Add("other", "x"));

        Assert.Equal(8L, CelExpression.Evaluate<long>("twiceValue + 1", new { twiceValue = 7L }, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void RegistrationArguments_AreChecked()
    {
        Assert.Throws<ArgumentNullException>("example", () => CelAnonymousData.Register<object>(null!, static _ => { }, static (_, _) => { }));
        Assert.Throws<ArgumentNullException>("declare", () => CelAnonymousData.Register(new { argValue = 1 }, null!, static (_, _) => { }));
        Assert.Throws<ArgumentNullException>("bind", () => CelAnonymousData.Register(new { argValue = 1 }, static _ => { }, null!));
    }

    [Fact]
    public void ExceptionOfARegistrationDelegate_ReachesTheCaller()
    {
        CelAnonymousData.Register(
            new { throwingValue = default(long) },
            static builder => builder.AddVariable<long>("throwingValue"),
            static (_, _) => throw new InvalidOperationException("bind"));

        // The internal call is no call site of CelExpression.Evaluate, so the generator does not register this type before the test does.
        object data = new { throwingValue = 1L };
        var cache = new SimpleEntryCache();

        Assert.Equal("bind", Assert.Throws<InvalidOperationException>(() => SimpleEntryCall.Evaluate<long>(cache, "throwingValue", data, CelExpression.DefaultCostLimit, CancellationToken.None)).Message);
    }

    // 64 threads over 16 shapes of data and 8 texts, on a cache of 4 environments and 8 programs that evicts all the time: every call
    // gives the value that a sequential call gives for its shape and text.
    [Fact]
    public void SixtyFourThreads_OnDifferentShapes_GiveTheSequentialResults()
    {
        var cache = new SimpleEntryCache(4, 8, 400);
        long[] failures = new long[64];

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 64 }, thread =>
        {
            for (int call = 0; call < 60; call++)
            {
                int shape = (thread + call) % 16;
                int text = (thread * 7 + call) % 8;
                string name = "s" + shape.ToString(CultureInfo.InvariantCulture);
                string expression = name + " * 10 + " + text.ToString(CultureInfo.InvariantCulture);
                var variables = new Dictionary<string, object?> { [name] = (long)shape, ["common"] = 5L };

                long expected = (shape * 10L) + text;
                if (Eval(cache, expression, variables) != expected)
                {
                    failures[thread]++;
                }
            }
        });

        Assert.All(failures, count => Assert.Equal(0L, count));
        (int environments, int programs, long totalTextLength) = cache.Kept;
        Assert.True(environments <= 4);
        Assert.True(programs <= 8);
        Assert.True(totalTextLength <= 400);
    }

    // The same through the cache of the process and the public method, on the shapes of the two kinds of data.
    [Fact]
    public void SixtyFourThreads_ThroughThePublicMethod_GiveTheSequentialResults()
    {
        long[] failures = new long[64];

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 64 }, thread =>
        {
            for (int call = 0; call < 100; call++)
            {
                int shape = (thread + call) % 16;
                string name = "t" + shape.ToString(CultureInfo.InvariantCulture);
                var variables = new Dictionary<string, object?> { [name] = (long)shape };
                if (CelExpression.Evaluate<long>(name + " + 1", variables, cancellationToken: CancellationToken.None) != shape + 1L)
                {
                    failures[thread]++;
                }

                if (CelExpression.Evaluate<long>("(x + 2) / 3", new { x = (long)(shape * 3) + 1 }, cancellationToken: CancellationToken.None) != shape + 1L)
                {
                    failures[thread]++;
                }
            }
        });

        Assert.All(failures, count => Assert.Equal(0L, count));
    }

    static SimpleEntryCacheTests()
    {
        CelAnonymousData.Register(
            new { x = default(long) },
            static builder => builder.AddVariable<long>("x"),
            static (data, bindings) => bindings.Add("x", data.x));
    }
}

using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The race of an eviction and the store of a place or of a reference, in both orders, made one after the other so that each order is
// the same on every run. The writer found an entry by value, the evicting thread marks the entry and clears its place, and the writer
// stores the entry in the place and reads the mark: either the store comes before the clearing and the eviction clears it, or after
// it and the mark is already set, so the writer takes the entry back. In both orders the place does not hold the evicted entry, and the
// entries that a fast path could still give out are none.
[Trait("Requirement", "R-4")]
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryCacheRaceTests
{
    private const string First = "x + 1";
    private const string Second = "x + 2";

    [Fact]
    public void ProgramWriterAfterTheEviction_TakesItsEntryBack()
    {
        var cache = new SimpleEntryCache(8, 1, 100_000);
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));
        _ = cache.GetOrCompile<long>(environment, First);
        SimpleEntryCache.ProgramEntry found = Assert.IsType<SimpleEntryCache.ProgramEntry>(cache.FindProgram(environment, First, typeof(long)));

        // One program fits, so the second evicts the first: the mark is set and the place is cleared.
        _ = cache.GetOrCompile<long>(environment, Second);
        Assert.True(found.Evicted);
        Assert.NotSame(found, SimpleEntryCache.RecentProgram(environment, First));

        // The writer comes late with the entry that it found before the eviction.
        SimpleEntryCache.PublishProgram(environment, found);

        Assert.NotSame(found, SimpleEntryCache.RecentProgram(environment, First));
        Assert.Equal(0, cache.EvictedReachable());
        Assert.Equal(1, cache.Kept.Programs);
    }

    [Fact]
    public void ProgramWriterBeforeTheEviction_IsClearedByTheEviction()
    {
        var cache = new SimpleEntryCache(8, 1, 100_000);
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));
        _ = cache.GetOrCompile<long>(environment, First);
        SimpleEntryCache.ProgramEntry found = Assert.IsType<SimpleEntryCache.ProgramEntry>(cache.FindProgram(environment, First, typeof(long)));

        // The writer stores first; the entry is in the place when the eviction starts.
        SimpleEntryCache.PublishProgram(environment, found);
        Assert.Same(found, SimpleEntryCache.RecentProgram(environment, First));
        Assert.False(found.Evicted);

        _ = cache.GetOrCompile<long>(environment, Second);

        Assert.True(found.Evicted);
        Assert.NotSame(found, SimpleEntryCache.RecentProgram(environment, First));
        Assert.Equal(0, cache.EvictedReachable());
    }

    [Fact]
    public void EvictedEnvironment_TakesItsProgramsOutOfTheirPlaces()
    {
        var cache = new SimpleEntryCache(1, 1_024, 100_000);
        SimpleEntryCache.EnvironmentEntry first = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));
        _ = cache.GetOrCompile<long>(first, First);
        SimpleEntryCache.ProgramEntry program = Assert.IsType<SimpleEntryCache.ProgramEntry>(cache.FindProgram(first, First, typeof(long)));
        Assert.Same(program, SimpleEntryCache.RecentProgram(first, First));

        // One environment fits: the second evicts the first with its programs.
        _ = SimpleEntryFixtures.AddEnvironment(cache, typeof(int));
        Assert.True(first.Evicted);
        Assert.True(program.Evicted);
        Assert.Null(SimpleEntryCache.RecentProgram(first, First));

        // The late writer of a program of the evicted environment takes it back.
        SimpleEntryCache.PublishProgram(first, program);
        Assert.Null(SimpleEntryCache.RecentProgram(first, First));

        // A program of an evicted environment is compiled and neither kept nor put in a place.
        _ = cache.GetOrCompile<long>(first, "x + 3");
        Assert.Null(SimpleEntryCache.RecentProgram(first, "x + 3"));
        Assert.Equal(0, cache.Kept.Programs);
        Assert.Equal(0, cache.EvictedReachable());
    }

    [Fact]
    public void EnvironmentWriterAfterTheEviction_TakesItsReferenceBack()
    {
        AnonymousRegistry.Entry registered = SimpleEntryFixtures.Register(
            new { lateWriterValue = default(long) },
            static builder => builder.AddVariable<long>("lateWriterValue"),
            static (data, bindings) => bindings.Add("lateWriterValue", data.lateWriterValue));
        var cache = new SimpleEntryCache(1, 1_024, 100_000);
        SimpleEntryCache.EnvironmentEntry environment = cache.GetEnvironment(registered);
        Assert.Same(environment, registered.Environment);

        // One environment fits: the next one evicts this one, marks it and clears the reference of the entry.
        _ = SimpleEntryFixtures.AddEnvironment(cache, typeof(int));
        Assert.True(environment.Evicted);
        Assert.Null(registered.Environment);

        // The writer comes late with the environment that it found before the eviction.
        registered.SetEnvironment(environment);

        Assert.Null(registered.Environment);
        Assert.Equal(0, cache.EvictedReachable());
    }

    [Fact]
    public void EnvironmentWriterBeforeTheEviction_IsClearedByTheEviction()
    {
        AnonymousRegistry.Entry registered = SimpleEntryFixtures.Register(
            new { earlyWriterValue = default(long) },
            static builder => builder.AddVariable<long>("earlyWriterValue"),
            static (data, bindings) => bindings.Add("earlyWriterValue", data.earlyWriterValue));
        var cache = new SimpleEntryCache(1, 1_024, 100_000);
        SimpleEntryCache.EnvironmentEntry environment = cache.GetEnvironment(registered);

        // The writer stores first; the reference is set when the eviction starts.
        registered.SetEnvironment(environment);
        Assert.Same(environment, registered.Environment);

        _ = SimpleEntryFixtures.AddEnvironment(cache, typeof(int));

        Assert.True(environment.Evicted);
        Assert.Null(registered.Environment);
        Assert.Equal(0, cache.EvictedReachable());
    }

    // The same ends through calls: after the environment of a registered type is evicted, a call finds no reference and builds it again.
    [Fact]
    public void CallAfterTheEvictionOfItsEnvironment_GivesTheValueAndBuildsAgain()
    {
        AnonymousRegistry.Entry registered = SimpleEntryFixtures.Register(
            new { afterEvictionValue = default(long) },
            static builder => builder.AddVariable<long>("afterEvictionValue"),
            static (data, bindings) => bindings.Add("afterEvictionValue", data.afterEvictionValue));
        var cache = new SimpleEntryCache(1, 1_024, 100_000);
        object data = new { afterEvictionValue = 4L };

        Assert.Equal(5L, SimpleEntryCall.Evaluate<long>(cache, "afterEvictionValue + 1", data, CelExpression.DefaultCostLimit, TestContext.Current.CancellationToken));
        SimpleEntryCache.EnvironmentEntry before = Assert.IsType<SimpleEntryCache.EnvironmentEntry>(registered.Environment);

        _ = SimpleEntryFixtures.AddEnvironment(cache, typeof(int));
        Assert.Null(registered.Environment);

        Assert.Equal(5L, SimpleEntryCall.Evaluate<long>(cache, "afterEvictionValue + 1", data, CelExpression.DefaultCostLimit, TestContext.Current.CancellationToken));
        Assert.NotSame(before, registered.Environment);
        Assert.Equal(0, cache.EvictedReachable());
    }
}

using System;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The fast paths of the cache of the simple entry, seen through its internal members: the table of the recent programs of an
// environment, read by the instance of the text before level 2, and the reference of an entry of the registry to the environment of its
// type, read before level 1. A test plants an entry of its own in a place or a reference: what a call then returns shows which
// way it took.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "R-4")]
public sealed class SimpleEntryCacheFastPathTests
{
    private const string Text = "x + 1";

    [Fact]
    public void SameInstanceOfTheText_IsFoundInThePlaceOfTheTable()
    {
        var cache = new SimpleEntryCache();
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));

        CelCompileResult<long> compiled = cache.GetOrCompile<long>(environment, Text);

        SimpleEntryCache.ProgramEntry? placed = SimpleEntryCache.RecentProgram(environment, Text);
        Assert.NotNull(placed);
        Assert.Same(compiled, placed.Result);
        Assert.Same(compiled, cache.GetOrCompile<long>(environment, Text));
        Assert.Equal(1, cache.Kept.Programs);
    }

    // The place of the same instance and the same T is read before level 2: a program planted there is what the call returns.
    [Fact]
    public void TheTable_IsReadBeforeLevel2()
    {
        var cache = new SimpleEntryCache();
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));
        CelCompileResult<long> compiled = cache.GetOrCompile<long>(environment, Text);
        CelCompileResult<long> other = environment.Environment.Compile<long>("x + 2");
        SimpleEntryCache.PublishProgram(environment, Plant(environment, other));

        Assert.NotSame(compiled, other);
        Assert.Same(other, cache.GetOrCompile<long>(environment, Text));
    }

    [Fact]
    public void EqualTextInAnotherInstance_FindsTheSameProgramByValue()
    {
        var cache = new SimpleEntryCache();
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));
        CelCompileResult<long> compiled = cache.GetOrCompile<long>(environment, Text);
        CelCompileResult<long> other = environment.Environment.Compile<long>("x + 2");
        SimpleEntryCache.PublishProgram(environment, Plant(environment, other));
        string copy = new(Text.AsSpan());
        Assert.NotSame(Text, copy);

        Assert.Same(compiled, cache.GetOrCompile<long>(environment, copy));
        Assert.Equal(1, cache.Kept.Programs);
    }

    // The mark of eviction is set before the place is cleared: an entry that carries it is a miss, and the program of level 2 is found.
    [Fact]
    public void EvictedEntryInThePlace_IsAMiss()
    {
        var cache = new SimpleEntryCache();
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));
        CelCompileResult<long> compiled = cache.GetOrCompile<long>(environment, Text);
        CelCompileResult<long> other = environment.Environment.Compile<long>("x + 2");
        SimpleEntryCache.ProgramEntry planted = Plant(environment, other);
        SimpleEntryCache.PublishProgram(environment, planted);
        planted.MarkEvicted();

        Assert.Same(compiled, cache.GetOrCompile<long>(environment, Text));
    }

    [Fact]
    public void ResultTypeOfTheCall_MustBeThatOfThePlace()
    {
        var cache = new SimpleEntryCache();
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));

        CelCompileResult<long> asLong = cache.GetOrCompile<long>(environment, Text);
        CelCompileResult<object> asObject = cache.GetOrCompile<object>(environment, Text);

        Assert.Equal(2, cache.Kept.Programs);
        Assert.Same(asLong, cache.GetOrCompile<long>(environment, Text));
        Assert.Same(asObject, cache.GetOrCompile<object>(environment, Text));
    }

    // A hit of the table marks the program for the clock, so a program that is used again and again through the table stays.
    [Fact]
    public void HitOfTheTable_MarksTheProgramForTheClock()
    {
        var cache = new SimpleEntryCache(8, 2, 100_000);
        SimpleEntryCache.EnvironmentEntry environment = SimpleEntryFixtures.AddEnvironment(cache, typeof(long));

        _ = cache.GetOrCompile<long>(environment, "x + 1");
        _ = cache.GetOrCompile<long>(environment, "x + 1");
        _ = cache.GetOrCompile<long>(environment, "x + 2");
        _ = cache.GetOrCompile<long>(environment, "x + 3");

        Assert.NotNull(cache.FindProgram(environment, "x + 1", typeof(long)));
        Assert.Null(cache.FindProgram(environment, "x + 2", typeof(long)));
        Assert.NotNull(cache.FindProgram(environment, "x + 3", typeof(long)));
    }

    [Fact]
    public void EnvironmentOfARegisteredType_IsFoundByTheReferenceOfItsEntry()
    {
        AnonymousRegistry.Entry registered = SimpleEntryFixtures.Register(
            new { fastEnvironment = default(long) },
            static builder => builder.AddVariable<long>("fastEnvironment"),
            static (data, bindings) => bindings.Add("fastEnvironment", data.fastEnvironment));
        var cache = new SimpleEntryCache();

        SimpleEntryCache.EnvironmentEntry built = cache.GetEnvironment(registered);
        Assert.Same(built, registered.Environment);
        Assert.Same(built, cache.GetEnvironment(registered));
        Assert.Equal(1, cache.Kept.Environments);

        // The reference is read before level 1: an environment of this cache that is planted in it is what the call returns.
        SimpleEntryCache.EnvironmentEntry planted = SimpleEntryFixtures.AddEnvironment(cache, typeof(int));
        registered.SetEnvironment(planted);
        Assert.Same(planted, cache.GetEnvironment(registered));

        // An evicted one is a miss: level 1 gives the environment of the type, and the reference is set to it again.
        planted.MarkEvicted();
        Assert.Same(built, cache.GetEnvironment(registered));
        Assert.Same(built, registered.Environment);
    }

    [Fact]
    public void ReferenceToAnEnvironmentOfAnotherCache_IsAMiss()
    {
        AnonymousRegistry.Entry registered = SimpleEntryFixtures.Register(
            new { otherCacheValue = default(long) },
            static builder => builder.AddVariable<long>("otherCacheValue"),
            static (data, bindings) => bindings.Add("otherCacheValue", data.otherCacheValue));
        var first = new SimpleEntryCache();
        var second = new SimpleEntryCache();

        SimpleEntryCache.EnvironmentEntry inFirst = first.GetEnvironment(registered);
        SimpleEntryCache.EnvironmentEntry inSecond = second.GetEnvironment(registered);

        Assert.NotSame(inFirst, inSecond);
        Assert.Equal(1, second.Kept.Environments);
        Assert.Same(inSecond, registered.Environment);
    }

    // A hit of the reference marks the environment for the clock, so an environment that is used through its entry stays.
    [Fact]
    public void HitOfTheReference_MarksTheEnvironmentForTheClock()
    {
        AnonymousRegistry.Entry registered = SimpleEntryFixtures.Register(
            new { clockValue = default(long) },
            static builder => builder.AddVariable<long>("clockValue"),
            static (data, bindings) => bindings.Add("clockValue", data.clockValue));
        var cache = new SimpleEntryCache(2, 1_024, 100_000);

        SimpleEntryCache.EnvironmentEntry used = cache.GetEnvironment(registered);
        _ = cache.GetEnvironment(registered);
        SimpleEntryCache.EnvironmentEntry unused = SimpleEntryFixtures.AddEnvironment(cache, typeof(int));
        _ = SimpleEntryFixtures.AddEnvironment(cache, typeof(short));

        Assert.False(used.Evicted);
        Assert.True(unused.Evicted);
    }

    // An entry of level 2 for the text, with a result of its own, that a test puts in a place of the table.
    private static SimpleEntryCache.ProgramEntry Plant(SimpleEntryCache.EnvironmentEntry environment, CelCompileResult<long> result) =>
        new(new SimpleEntryCache.ProgramKey(environment, Text, typeof(long)), result);
}

using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// TryRemove and Clear while other threads insert: no call fails, and the cache stays consistent: after every text has been
// removed, the cache holds no entry and no text length.
[Trait("Requirement", "+R-13")]
public sealed class CompilationCacheRemovalTests
{
    private const int Texts = 200;
    private const int Rounds = 20;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void TryRemoveAndClear_WhileOtherThreadsInsert_LeaveAConsistentCache()
    {
        var cache = new CelCompilationCache(Environment, 64, 100_000);
        var bindings = new CelBindings().Add("x", 1L);

        // Four bodies at once, each on a thread of its own; a failure of a body is thrown here.
        Parallel.Invoke(
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            () => Insert(cache, bindings, 0),
            () => Insert(cache, bindings, 1),
            () => Remove(cache),
            () => ClearOften(cache));

        for (int i = 0; i < Texts; i++)
        {
            _ = cache.TryRemove(Text(i));
        }

        Assert.Equal(0, cache.Count);
        Assert.Equal((0, 0L), cache.Kept);
    }

    [Fact]
    public void TryRemove_OfAnEntryAmongOthers_KeepsTheTextLengthOfTheRest()
    {
        var cache = new CelCompilationCache(Environment);
        _ = cache.GetOrCompile<long>("x + 1");
        _ = cache.GetOrCompile<long>("x + 22");
        _ = cache.GetOrCompile<double>("x + 1");

        Assert.True(cache.TryRemove("x + 1"));

        Assert.Equal((1, (long)"x + 22".Length), cache.Kept);
    }

    private static string Text(int i) => "x + " + i;

    private static void Insert(CelCompilationCache cache, CelBindings bindings, int offset)
    {
        for (int round = 0; round < Rounds; round++)
        {
            for (int i = offset; i < Texts; i++)
            {
                _ = cache.Evaluate<long>(Text(i), bindings, cancellationToken: CancellationToken.None);
            }
        }
    }

    private static void Remove(CelCompilationCache cache)
    {
        for (int round = 0; round < Rounds; round++)
        {
            for (int i = 0; i < Texts; i++)
            {
                _ = cache.TryRemove(Text(i));
            }
        }
    }

    private static void ClearOften(CelCompilationCache cache)
    {
        for (int round = 0; round < Rounds * 5; round++)
        {
            cache.Clear();
            _ = cache.Count;
            Thread.Yield();
        }
    }
}

using BenchmarkDotNet.Attributes;
using Great.Parts.Cel;
using Great.Parts.Cel.Values;

namespace ThresholdBench;

/// <summary>
/// The limit of the index of the values of a list: an index of at most 24 values scans the hashes in order, a longer one uses
/// buckets (<c>ValueIndex</c>), and both are the hashed path of <c>distinct</c> and <c>sets</c>. The class measures the index
/// directly on a list of N integers, on both sides of the limit, in four ways:
/// <list type="bullet">
/// <item>the index as the library makes it for N values (the scan up to 24, the buckets above);</item>
/// <item>the buckets for any N: the capacity of the index is set above the limit, so a short list also gets buckets;</item>
/// <item>every value with one hash, the collisions all there (the parameter that the tests use): the scan, or one chain of
/// buckets;</item>
/// <item>the pairwise group: integers of 2^53 or more are compared with each other in order, as the pairs were.</item>
/// </list>
/// The operations are the building of <c>distinct</c> (add each value if absent) and the lookup of <c>sets.contains</c> (append
/// all, seal, look up each). The limit 24 stands when the scan is not slower than the buckets at N up to 24.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class HashingBenchmarks
{
    // The index builds buckets for a capacity above this value (ValueIndex.ScanLimit); the forced capacity is one above it.
    private const int ForcedBucketsCapacity = 25;

    private CelValue[] _values = null!;
    private CelValue[] _large = null!;

    [Params(8, 16, 24, 32, 48)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _values = new CelValue[N];
        _large = new CelValue[N];
        for (int i = 0; i < N; i++)
        {
            _values[i] = CelValue.FromInt64(i);
            _large[i] = CelValue.FromInt64((1L << 53) + i);
        }

        if (DistinctCount(_values, N, constantHash: false) != N
            || DistinctCount(_values, Math.Max(N, ForcedBucketsCapacity), constantHash: false) != N
            || DistinctCount(_values, N, constantHash: true) != N
            || DistinctCount(_large, N, constantHash: false) != N
            || !ContainsAll(_values, N, constantHash: false)
            || !ContainsAll(_values, Math.Max(N, ForcedBucketsCapacity), constantHash: false)
            || !ContainsAll(_values, N, constantHash: true)
            || !ContainsAll(_large, N, constantHash: false))
        {
            throw new InvalidOperationException("an index gives a wrong result at N = " + N);
        }
    }

    [Benchmark(Baseline = true)]
    public int DistinctDefault() => DistinctCount(_values, N, constantHash: false);

    [Benchmark]
    public int DistinctBuckets() => DistinctCount(_values, Math.Max(N, ForcedBucketsCapacity), constantHash: false);

    [Benchmark]
    public int DistinctConstantHash() => DistinctCount(_values, N, constantHash: true);

    [Benchmark]
    public int DistinctPairwise() => DistinctCount(_large, N, constantHash: false);

    [Benchmark]
    public bool ContainsDefault() => ContainsAll(_values, N, constantHash: false);

    [Benchmark]
    public bool ContainsBuckets() => ContainsAll(_values, Math.Max(N, ForcedBucketsCapacity), constantHash: false);

    [Benchmark]
    public bool ContainsConstantHash() => ContainsAll(_values, N, constantHash: true);

    [Benchmark]
    public bool ContainsPairwise() => ContainsAll(_large, N, constantHash: false);

    private static int DistinctCount(CelValue[] values, int capacity, bool constantHash)
    {
        var index = new ValueIndex(capacity, null, null, null, constantHash);
        foreach (CelValue value in values)
        {
            index.AddIfAbsent(value, out _);
        }

        return index.Count;
    }

    private static bool ContainsAll(CelValue[] values, int capacity, bool constantHash)
    {
        var index = new ValueIndex(capacity, null, null, null, constantHash);
        foreach (CelValue value in values)
        {
            index.Append(value);
        }

        index.Seal();
        foreach (CelValue value in values)
        {
            if (index.Contains(value, out _) != ValueEquality.Equal)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// <c>distinct</c> and <c>sets.contains</c> through the public API on a list of N integers, around the limit 24 of the index: the
/// time of the whole evaluation of a program, with the read of the list, the frame and the result.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class HashingEvaluationBenchmarks
{
    private CelProgram<CelValue> _distinct = null!;
    private CelProgram<CelValue> _contains = null!;
    private CelBindings _bindings = null!;

    [Params(16, 24, 25, 32, 48)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Sets(0))
            .AddVariable<List<long>>("xs")
            .Build()
            .GetEnvironmentOrThrow();
        _distinct = environment.Compile<CelValue>("xs.distinct()").GetProgramOrThrow();
        _contains = environment.Compile<CelValue>("sets.contains(xs, xs)").GetProgramOrThrow();
        var items = new List<long>(N);
        for (int i = 0; i < N; i++)
        {
            items.Add(i);
        }

        _bindings = new CelBindings().Add("xs", items);
        if (!_contains.Evaluate(_bindings).GetValueOrThrow().TryGetBoolean(out bool all)
            || !all
            || !_distinct.Evaluate(_bindings).GetValueOrThrow().TryGetList(out CelList? list)
            || list.Count != N)
        {
            throw new InvalidOperationException("a program gives a wrong result at N = " + N);
        }
    }

    [Benchmark(Baseline = true)]
    public CelValue Distinct() => _distinct.Evaluate(_bindings).GetValueOrThrow();

    [Benchmark]
    public CelValue SetsContains() => _contains.Evaluate(_bindings).GetValueOrThrow();
}

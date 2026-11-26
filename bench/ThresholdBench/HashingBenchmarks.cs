using BenchmarkDotNet.Attributes;
using Great.Parts.Cel;
using Great.Parts.Cel.Values;

namespace ThresholdBench;

/// <summary>
/// The limit of the index of the values of a list: an index of at most 64 values (<c>ValueIndex.ScanLimit</c>) scans the hashes in
/// order, a longer one uses buckets, and both are the hashed path of <c>distinct</c> and <c>sets</c>. The class measures the index
/// directly on a list of N integers, up to the limit, in five ways:
/// <list type="bullet">
/// <item>the index as the library makes it for N values (the scan up to the limit, the buckets above);</item>
/// <item>the scan for any N (<c>ValueIndexMode.Scan</c>);</item>
/// <item>the buckets for any N (<c>ValueIndexMode.Buckets</c>);</item>
/// <item>every value with one hash, the collisions all there (the parameter that the tests use): the scan, or one chain of
/// buckets;</item>
/// <item>the pairwise group: integers of 2^53 or more are compared with each other in order, as the pairs were.</item>
/// </list>
/// The operations are the building of <c>distinct</c> (add each value if absent) and the lookup of <c>sets.contains</c> (append
/// all, seal, look up each); the arrays of the index are rented from the shared pool and given back, as a call of the library does.
/// The measurement states no verdict: <c>run.mjs</c> prints the largest N at which the scan is not slower than the buckets at every
/// N up to it, for both operations, and the limit is set by a decision on that table.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class HashingBenchmarks
{
    private CelValue[] _values = null!;
    private CelValue[] _large = null!;

    [Params(8, 16, 24, 32, 48, 64)]
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

        if (DistinctCount(_values, N, constantHash: false, ValueIndexMode.Automatic) != N
            || DistinctCount(_values, N, constantHash: false, ValueIndexMode.Scan) != N
            || DistinctCount(_values, N, constantHash: false, ValueIndexMode.Buckets) != N
            || DistinctCount(_values, N, constantHash: true, ValueIndexMode.Automatic) != N
            || DistinctCount(_large, N, constantHash: false, ValueIndexMode.Automatic) != N
            || !ContainsAll(_values, N, constantHash: false, ValueIndexMode.Automatic)
            || !ContainsAll(_values, N, constantHash: false, ValueIndexMode.Scan)
            || !ContainsAll(_values, N, constantHash: false, ValueIndexMode.Buckets)
            || !ContainsAll(_values, N, constantHash: true, ValueIndexMode.Automatic)
            || !ContainsAll(_large, N, constantHash: false, ValueIndexMode.Automatic))
        {
            throw new InvalidOperationException("an index gives a wrong result at N = " + N);
        }
    }

    [Benchmark(Baseline = true)]
    public int DistinctDefault() => DistinctCount(_values, N, constantHash: false, ValueIndexMode.Automatic);

    [Benchmark]
    public int DistinctScan() => DistinctCount(_values, N, constantHash: false, ValueIndexMode.Scan);

    [Benchmark]
    public int DistinctBuckets() => DistinctCount(_values, N, constantHash: false, ValueIndexMode.Buckets);

    [Benchmark]
    public int DistinctConstantHash() => DistinctCount(_values, N, constantHash: true, ValueIndexMode.Automatic);

    [Benchmark]
    public int DistinctPairwise() => DistinctCount(_large, N, constantHash: false, ValueIndexMode.Automatic);

    [Benchmark]
    public bool ContainsDefault() => ContainsAll(_values, N, constantHash: false, ValueIndexMode.Automatic);

    [Benchmark]
    public bool ContainsScan() => ContainsAll(_values, N, constantHash: false, ValueIndexMode.Scan);

    [Benchmark]
    public bool ContainsBuckets() => ContainsAll(_values, N, constantHash: false, ValueIndexMode.Buckets);

    [Benchmark]
    public bool ContainsConstantHash() => ContainsAll(_values, N, constantHash: true, ValueIndexMode.Automatic);

    [Benchmark]
    public bool ContainsPairwise() => ContainsAll(_large, N, constantHash: false, ValueIndexMode.Automatic);

    private static int DistinctCount(CelValue[] values, int capacity, bool constantHash, ValueIndexMode mode)
    {
        var index = new ValueIndex(capacity, null, null, null, constantHash, mode);
        try
        {
            foreach (CelValue value in values)
            {
                index.AddIfAbsent(value, out _);
            }

            return index.Count;
        }
        finally
        {
            index.Release();
        }
    }

    private static bool ContainsAll(CelValue[] values, int capacity, bool constantHash, ValueIndexMode mode)
    {
        var index = new ValueIndex(capacity, null, null, null, constantHash, mode);
        try
        {
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
        finally
        {
            index.Release();
        }
    }
}

/// <summary>
/// <c>distinct</c> and <c>sets.contains</c> through the public API on a list of N integers, around the limit 32 of the index: the
/// time of the whole evaluation of a program, with the read of the list, the frame and the result.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class HashingEvaluationBenchmarks
{
    private CelProgram<CelValue> _distinct = null!;
    private CelProgram<CelValue> _contains = null!;
    private CelBindings _bindings = null!;

    [Params(16, 32, 33, 48, 64)]
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

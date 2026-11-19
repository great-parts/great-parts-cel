using BenchmarkDotNet.Attributes;
using Great.Parts.Cel.Library.Text;

namespace ThresholdBench;

/// <summary>
/// The limit of the hybrid search of a substring: a pattern of at most 64 code units is searched by the ordinal search of .NET, a
/// longer one by Knuth–Morris–Pratt. The texts are the slowest for the search of .NET: the text (ab)^5000 and a pattern of the
/// length M that is the prefix (ab)^k followed by "bb" (or "b" for an odd length), which is absent but matches almost to its end at
/// every second place. The limit 64 stands when the search of .NET at M = 64 is not slower than Knuth–Morris–Pratt; the limit is 32
/// otherwise.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class SubstringBenchmarks
{
    private string _text = string.Empty;
    private string _pattern = string.Empty;

    [Params(48, 64, 65, 80)]
    public int M { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat("ab", 5_000));
        // For an even M the prefix is (ab)^((M - 2) / 2) and the tail "bb"; for an odd M the prefix is (ab)^((M - 1) / 2) and the tail "b".
        _pattern = string.Concat(Enumerable.Repeat("ab", M % 2 == 0 ? (M - 2) / 2 : (M - 1) / 2)) + (M % 2 == 0 ? "bb" : "b");
        if (_pattern.Length != M)
        {
            throw new InvalidOperationException("the pattern has " + _pattern.Length + " code units, not " + M);
        }

        int expected = Kmp();
        if (expected != Net() || expected != Hybrid())
        {
            throw new InvalidOperationException("the searches disagree at M = " + M);
        }
    }

    /// <summary>The search of the library for a pattern of any length.</summary>
    [Benchmark(Baseline = true)]
    public int Kmp()
    {
        long steps = 0;
        return SubstringSearch.IndexOfKmp(_text, _pattern, ref steps);
    }

    /// <summary>The ordinal search of .NET.</summary>
    [Benchmark]
    public int Net() => _text.AsSpan().IndexOf(_pattern.AsSpan(), StringComparison.Ordinal);

    /// <summary>The search of the library as it runs: .NET up to its limit, Knuth–Morris–Pratt above.</summary>
    [Benchmark]
    public int Hybrid()
    {
        long steps = 0;
        return SubstringSearch.IndexOf(_text, _pattern, ref steps);
    }
}

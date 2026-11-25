using BenchmarkDotNet.Attributes;
using Great.Parts.Cel;
using Great.Parts.Cel.Library.Text;
using Great.Parts.Cel.Values;

namespace ThresholdBench;

/// <summary>
/// The limit of the hybrid search for the literal prefix of a pattern of <c>matches</c>: a pattern of at most 64 code units is
/// searched by the ordinal search of .NET, a longer one by Knuth–Morris–Pratt. The functions of strings do not use this search. The
/// texts are the slowest for the search of .NET: the text (ab)^5000 and a pattern of the
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
        _pattern = SubstringPattern.Make(M);
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

/// <summary>
/// A diagnostic without a verdict: the time of <c>contains</c> on the text (ab)^5000 per unit of its cost, for a pattern of M code
/// units that is a literal of the expression, searched by the <c>SearchValues&lt;string&gt;</c> that compilation prepared, and for the
/// same pattern read from a variable, searched by the ordinal search. The cost is 1 + ceil((n + M) / 10) · q with
/// q = max(1, ceil(M / 64)); <c>run.mjs</c> divides the median by it. The time is that of the whole evaluation. A rise of the time per
/// unit above two times from M = 64 to M = 1 024 is the input of a decision on the divisor 64 of q.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class SubstringCostBenchmarks
{
    private CelProgram<CelValue> _literal = null!;
    private CelProgram<CelValue> _fromData = null!;
    private CelBindings _bindings = null!;

    [Params(64, 256, 1_024)]
    public int M { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string pattern = SubstringPattern.Make(M);
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<string>("text")
            .AddVariable<string>("pattern")
            .Build()
            .GetEnvironmentOrThrow();
        _fromData = environment.Compile<CelValue>("text.contains(pattern)").GetProgramOrThrow();
        _literal = environment.Compile<CelValue>("text.contains('" + pattern + "')").GetProgramOrThrow();
        _bindings = new CelBindings()
            .Add("text", string.Concat(Enumerable.Repeat("ab", 5_000)))
            .Add("pattern", pattern);

        // The pattern is absent from the text, so both searches pass the whole text.
        if (Absent(_fromData) is not true || Absent(_literal) is not true)
        {
            throw new InvalidOperationException("a program gives a wrong result at M = " + M);
        }
    }

    /// <summary>A pattern from a variable: the ordinal search of .NET.</summary>
    [Benchmark(Baseline = true)]
    public CelValue FromData() => _fromData.Evaluate(_bindings).GetValueOrThrow();

    /// <summary>A pattern that is a literal: the values that compilation prepared.</summary>
    [Benchmark]
    public CelValue Literal() => _literal.Evaluate(_bindings).GetValueOrThrow();

    private bool? Absent(CelProgram<CelValue> program) =>
        program.Evaluate(_bindings).GetValueOrThrow().TryGetBoolean(out bool found) ? !found : null;
}

// The pattern of the length M that is the prefix (ab)^k followed by "bb" for an even M and "b" for an odd one.
internal static class SubstringPattern
{
    internal static string Make(int m) =>
        string.Concat(Enumerable.Repeat("ab", m % 2 == 0 ? (m - 2) / 2 : (m - 1) / 2)) + (m % 2 == 0 ? "bb" : "b");
}

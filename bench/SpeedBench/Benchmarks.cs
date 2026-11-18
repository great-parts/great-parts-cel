using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using SpeedBench.Adapters;

namespace SpeedBench;

// No job here: BenchmarkDotNet runs Job.Default unless the command line names another job (--job Dry).
public sealed class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.Median);
        AddExporter(JsonExporter.Full);
    }
}

/// <summary>The three operations (compilation, evaluation, full cycle) for every verified pair "library:case".</summary>
[Config(typeof(BenchConfig))]
public class OperationBench
{
    private ILibraryAdapter _library = null!;
    private string _expression = "";
    private object _compiled = null!;
    private Order _order = null!;
    private DateTimeOffset _now;

    [ParamsSource(nameof(Targets))]
    public string Target { get; set; } = "";

    public static IEnumerable<string> Targets() => Verification.MeasuredTargets();

    [GlobalSetup]
    public void Setup()
    {
        (_library, var scenario) = Verification.ParseTarget(Target);
        _expression = scenario.Expression;
        _order = Scenarios.Order(scenario.Lines);
        _now = Scenarios.Now;
        _compiled = _library.Compile(_expression);
    }

    [Benchmark]
    public object Compile() => _library.Compile(ExpressionText.Next(_expression));

    [Benchmark]
    public object? Evaluate() => _library.Evaluate(_compiled, _order, _now);

    [Benchmark]
    public object? FullCycle() => _library.FullCycle(ExpressionText.Next(_expression), _order, _now);
}

/// <summary>Cost of creating a new text, subtracted from compilation and full cycle by run.mjs.</summary>
[Config(typeof(BenchConfig))]
public class TextBench
{
    private string _expression = "";

    [ParamsSource(nameof(Cases))]
    public string Case { get; set; } = "";

    public static IEnumerable<string> Cases() => Scenarios.Selected().Select(s => s.Id);

    [GlobalSetup]
    public void Setup() => _expression = Scenarios.Get(Case).Expression;

    [Benchmark]
    public string CreateText() => ExpressionText.Next(_expression);
}

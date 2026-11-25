using BenchmarkDotNet.Attributes;
using Great.Parts.Cel.Library.Regex;

namespace ThresholdBench;

/// <summary>
/// The cost of building the automaton of a pattern against the parse and the build of its program, which every pattern pays. The
/// criterion for the pattern E5 of the rig, <c>^[^@]+@[^@]+[.][a-z]+$</c>, is a build of the automaton in at most 2 microseconds on
/// the standard GitHub Linux runner (the median of <see cref="BuildAutomaton"/>).
/// </summary>
[Config(typeof(ThresholdConfig))]
public class AutomatonBenchmarks
{
    private CompiledPattern _program = null!;
    private string _pattern = string.Empty;

    /// <summary>E5 is the pattern of the rig; UUID is a longer one for comparison.</summary>
    [Params("E5", "UUID")]
    public string Name { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _pattern = Name == "E5"
            ? "^[^@]+@[^@]+[.][a-z]+$"
            : "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
        if (!PatternParser.TryParse(_pattern, out PatternTree? tree, out _))
        {
            throw new InvalidOperationException("the pattern is refused: " + Name);
        }

        _program = ProgramBuilder.Build(tree!);
        tree!.Dispose();
        if (DfaBuilder.TryBuild(_program) is null)
        {
            throw new InvalidOperationException("the pattern has no automaton: " + Name);
        }
    }

    /// <summary>The parse of the pattern and the build of its program.</summary>
    [Benchmark(Baseline = true)]
    public int ParseAndBuildProgram()
    {
        PatternParser.TryParse(_pattern, out PatternTree? tree, out _);
        CompiledPattern program = ProgramBuilder.Build(tree!);
        tree!.Dispose();
        return program.Size;
    }

    /// <summary>The build of the automaton of the program that is already built.</summary>
    [Benchmark]
    public int BuildAutomaton() => DfaBuilder.TryBuild(_program)?.StateCount ?? -1;
}

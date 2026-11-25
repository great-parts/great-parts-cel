using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;
using SpeedBench;

namespace ThresholdBench;

// No job of its own here: BenchmarkDotNet runs Job.Default unless the command line names another job (--job Dry); the only job
// below sets the toolchain of every job (Net11Toolchain). run.mjs reads the full JSON reports and writes the verdicts of the limits.
public sealed class ThresholdConfig : ManualConfig
{
    public ThresholdConfig()
    {
        AddJob(Net11Toolchain.Mutator);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.Median);
        AddExporter(JsonExporter.Full);
    }
}

public static class Program
{
    // A failed benchmark does not change the exit code of BenchmarkDotNet; this method does.
    public static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args).ToList();
        var failed = summaries.Where(s => s.HasCriticalValidationErrors).Select(s => s.Title)
            .Concat(summaries.SelectMany(s => s.Reports).Where(r => !r.Success).Select(r => r.BenchmarkCase.DisplayInfo))
            .ToList();
        foreach (var f in failed)
        {
            Console.Error.WriteLine("failed: " + f);
        }

        return summaries.Count == 0 || failed.Count > 0 ? 1 : 0;
    }
}

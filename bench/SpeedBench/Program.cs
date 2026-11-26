#if !BENCH_AOT
using BenchmarkDotNet.Running;
#endif
using SpeedBench;
using SpeedBench.Adapters;

// Modes (run.mjs starts them):
//   verify <out.json> [library]       - verify every library (or one) on every case against the expected value
//   libraries                         - the names of the libraries, one per line
//   exclude <out.json> <library> <reason> - the rows of a library that could not be run, every case excluded with the reason
//   bench [BDN args]                  - BenchmarkDotNet over the verified pairs (BENCH_CASES selects cases); not in the Native AOT build
//
// The Native AOT build (SpeedBench published with BenchAot=true, the constant BENCH_AOT) has no mode bench: BenchmarkDotNet is not
// compiled into it, and the measurement under Native AOT is made by the processes that BenchmarkDotNet publishes itself.
switch (args.FirstOrDefault())
{
    case "verify":
    {
        var rows = args.Length > 2 ? Verification.RunLibrary(args[2]) : Verification.RunAll();
        foreach (var r in rows)
        {
            var wrong = r.Variants.FirstOrDefault(v => !v.Correct);
            var detail = wrong is null ? r.Expected : $"{wrong.Variant}: {wrong.Result}";
            if (detail.Length > 100) detail = detail[..100] + "...";
            Console.WriteLine($"{r.Library,-10} {r.Scenario,-7} {(r.Correct ? "ok" : "WRONG"),-5} {detail}");
        }

        if (args.Length > 1) Verification.WriteJson(args[1], rows);
        return 0;
    }

    case "libraries":
        foreach (var name in Libraries.Names) Console.WriteLine(name);
        return 0;

    case "exclude" when args.Length == 4:
        Verification.WriteJson(args[1], Verification.ExcludeLibrary(args[2], args[3]));
        return 0;

#if !BENCH_AOT
    case "bench":
    {
        // A failed benchmark does not change the exit code of BenchmarkDotNet; the bench does.
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(OperationBench).Assembly).Run(args[1..]).ToList();
        var failed = summaries.Where(s => s.HasCriticalValidationErrors).Select(s => s.Title)
            .Concat(summaries.SelectMany(s => s.Reports).Where(r => !r.Success).Select(r => r.BenchmarkCase.DisplayInfo))
            .ToList();
        foreach (var f in failed) Console.Error.WriteLine("failed: " + f);
        return summaries.Count == 0 || failed.Count > 0 ? 1 : 0;
    }
#endif

    default:
        Console.Error.WriteLine("usage: SpeedBench verify <out.json> [library] | libraries | exclude <out.json> <library> <reason> | bench [BenchmarkDotNet args]");
        return 2;
}

using BenchmarkDotNet.Running;
using SpeedBench;

// Modes (run.mjs starts them):
//   verify <out.json>   - verify every library on every case against the expected value
//   bench [BDN args]    - BenchmarkDotNet over the verified pairs (BENCH_CASES selects cases)
switch (args.FirstOrDefault())
{
    case "verify":
    {
        var rows = Verification.RunAll();
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

    default:
        Console.Error.WriteLine("usage: SpeedBench verify <out.json> | bench [BenchmarkDotNet args]");
        return 2;
}

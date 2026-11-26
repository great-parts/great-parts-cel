# Benchmark rig

The benchmark rig in `bench/` compares the speed of Great.Parts.Cel with other CEL libraries for .NET. It measures three operations on twelve scenarios and writes a table of times, memory and ratios. It is built and run only for a measurement, on a GitHub-hosted runner, by the workflow `.github/workflows/bench.yml`. The measurement is not a condition of a release: a row whose ratio is above the target is the input of a task to speed the library up.

The rig is not part of the library, of its packages or of `Great.Parts.Cel.slnx`. No command of `eng/` runs it, and `ci.yml` does not build it. The libraries that are compared, their versions and the way each one receives data are listed in `bench/README.md`; this document describes the rig and its run.

## Layout

| Path | Contents |
| :- | :- |
| `bench/SpeedBench/SpeedBench.csproj` | the console application, `net11.0`: BenchmarkDotNet, the compared libraries, Great.Parts.Cel from the sources of the commit (a project reference, with the source generator as an analyzer), Grpc.Tools (build only) |
| `bench/ThresholdBench/` | the console application of the limits and diagnostics, `net11.0`: the benchmarks of [Limits and diagnostics](#limits-and-diagnostics); it sees the internals of the library |
| `bench/SpeedBench/Scenarios.cs` | the texts of the expressions E1 to E9, the twelve cases, the data, the expected value of each case |
| `bench/SpeedBench/Model.cs` | the classes of the data: `Order`, `Line`, `Customer` |
| `bench/SpeedBench/Adapters/` | `ILibraryAdapter` with the three operations, the list of the libraries, one adapter for each library; `ProtoData.cs` turns the order into protobuf messages for the libraries that read protobuf |
| `bench/SpeedBench/Proto/order.proto` | the same data as protobuf messages |
| `bench/SpeedBench/ExpressionText.cs` | a new text of the expression for every call |
| `bench/SpeedBench/Verification.cs` | the check of every library on every case before the measurement |
| `bench/SpeedBench/Benchmarks.cs` | `OperationBench` (the three operations) and `TextBench` (the creation of a text) |
| `bench/SpeedBench/Canon.cs` | the canonical text of a result, for the comparison with the expected value |
| `bench/run.mjs` | the runner: build, verification, several measurement runs, the tables and the summary; Node.js without npm dependencies |
| `bench/Directory.Build.props`, `bench/Directory.Packages.props`, `bench/.editorconfig` | the settings and the pinned versions of the rig, see [Packages](#packages) |

## Scenarios and operations

The scenarios are typical rules on objects of .NET classes: comparisons and logic, macros over 10 and 100 elements, strings, `matches`, time. E1 and E5 to E9 run on an order of 10 lines, E2 to E4 on orders of 10 and 100 lines, 12 cases in all.

An adapter has three operations. The environment of the library (declarations, providers, settings) is created once per process and is not part of any of them.

| Operation | What it includes |
| :- | :- |
| compile | every call of the library from the text to an object that is ready for evaluation; for Great.Parts.Cel the call `Compile` (parse, check, plan) |
| evaluate | the evaluation of a compiled object on the data of the application: the creation of the activation of the library and, for a library that does not read .NET objects, the translation of the whole order graph into its own structure; the order object is created once, before the measurement |
| full cycle | every call from the text to the value; for Great.Parts.Cel the one-step `Evaluate` of the environment |

Each library gets the data in the way its documentation recommends. Great.Parts.Cel reads the classes through a type context that the source generator of the package writes at build time. The variant of the simple entry passes the scalar fields of the order, the customer (`order.Customer`) and the lines (`order.Lines`) as properties of an anonymous object, without turning them into `CelMap` values; the texts are those without the prefix `order.`, as before.

### A new text for every compilation

In compile and in full cycle a library gets, on every call, a text of the expression that it has not compiled in this process. The text is the expression followed by a tail of 16 white space characters of CEL (space, tab, line feed, carriage return, 2 bits each) that encodes a counter. The counter only grows and is never reset in the process, so the 2^32 tails do not repeat, and all the calls of a case do the same work: the length and the meaning of the text do not change. A cache of compilations by text, if a library has one, misses every time. Verification checks that a text with a tail gives the same result as the original.

The cost of making the text is measured by `TextBench.CreateText`, which calls the same method as the operations. The decisive column of the tables is the median of the operation minus the median of the creation of the text, for time and for allocated memory; the column without the subtraction is next to it. The summary over the runs has the column Allocated: the median over the runs of the allocated bytes per operation that BenchmarkDotNet reports (`MemoryDiagnoser`), less the allocation of making the text, as for the time.

## Verification

Before the measurement every library runs every case in three ways: the original text (compile and evaluate), a text with a tail (compile and evaluate), a text with a tail (full cycle). The result is turned into a canonical text (type and value, a `double` in the format "R") and compared with the expected value of the case, which the rig holds. A library that gives another value or an error in one of the ways does not take part in that case: `OperationBench` gets only the pairs "library:case" that are correct, and the exclusions are listed in the verification table of the summary.

## Measurement and tables

BenchmarkDotNet runs with the default job, `MemoryDiagnoser`, the median column and the JSON report; every case runs in a process of its own. The runtime is .NET 11 under JIT: the projects of the rig target `net11.0`, and the SDK and the runtime are those of `global.json` on the runner. The release 0.15.8 of BenchmarkDotNet cannot name .NET 11, so the rig (both projects) takes the preview 0.16.0-preview.2, which knows it (`CoreRuntime.Core11_0`, the moniker `net11.0`); the preview is used by the rig only, never by the packages of the library.

A run is one measurement of all the rows. `run.mjs --runs N` makes N runs one after another. In every run, for each scenario and operation, the ratio of a library is its decisive time divided by the decisive time of the fastest library with a correct result in the same run. For every row "scenario, operation, library" the summary shows the ratios of the runs, R (their median), the spread (the largest ratio divided by the smallest) and the fastest library of each run. The target is R not above 1.1 in every row of Great.Parts.Cel; a row with R above 1.1 is listed in the closing block. A row has a result when it has at least three runs; a row whose ratios lie on both sides of 1.1 is marked, and at five runs it is marked as at the threshold.

The files of a run are in `bench/results/` (not in git): `verify.json`, `runs/<k>/` with the report of BenchmarkDotNet, `run.json` and the table of the run, and `summary.md` over all the runs. `node bench/run.mjs --summary-only` builds the summaries again from the saved runs.

## Limits and diagnostics

`bench/ThresholdBench/` measures, on the code of the commit, the limits of the speed-ups of the library; `run.mjs` runs it after the runs of the rig and writes the section "Limits of the speed-ups" of the summary. A limit has a verdict; a diagnostic has none.

- **The hybrid search of a substring** is now the search for the literal prefix of `matches` only: a pattern of at most 64 code units by the ordinal search of .NET, a longer one by Knuth–Morris–Pratt ([library](library.md#substring-search)). The functions of strings do not use it.
- **Substring search: time per cost unit**, a diagnostic without a verdict (`bench/ThresholdBench/SubstringBenchmarks.cs`, the class `SubstringCostBenchmarks`, and its rows in `bench/run.mjs`). `contains` on the text `(ab)^5000` with a pattern of M = 64, 256 and 1 024 code units — the prefix `(ab)^k` and the tail `bb`, as in the measurement of the hybrid — for a pattern that is a literal of the expression (the `SearchValues<string>` that compilation prepared) and for the same pattern read from a variable (the ordinal search). The table shows the median time of the evaluation, the cost from the formula of the page [Cost](../cost.md#the-search-for-a-substring) without the reads of the variables, and the time per unit. A rise of the time per unit from M = 64 to M = 1 024 above two times is the input of a decision on the divisor 64 of the factor q of the cost; the rig decides nothing.
- **The build of the automaton of a literal pattern**, a limit with a verdict: the median of the build of the automaton of the pattern E5 of the rig is at most 2 µs on the standard GitHub Linux runner (`AutomatonBenchmarks`); the pattern UUID and the parse and the build of the program are shown for comparison.
- **The index of the values of a list** (`distinct`, `sets.contains`), a diagnostic without a verdict (`HashingBenchmarks`): the index scans the hashes in order or uses buckets, and the measurement forces each way (`ValueIndexMode.Scan` and `ValueIndexMode.Buckets`) at N = 8, 16, 24, 32, 48 and 64 integers, next to the index as the library makes it (the scan up to 64 values, the buckets above), the index with one hash for every value and the pairwise group. The arrays are rented and returned as in a call of the library. The summary prints the threshold by the rule: the largest N at which, and at every smaller N measured, scan / buckets is at most 1.0 for both operations, and 8 when the scan is slower already at N = 8. The rig sets no limit: the constant of the library is set by a decision on the table of the first run with the scan. The evaluation of `distinct` and `sets.contains` through the public API at N = 16, 32, 33, 48 and 64 shows a jump of the time per element at the limit, if there is one.
- **Simple entry: evaluate** and **simple entry: data**, diagnostics without a verdict (`SimpleEntryBenchmarks`). "Evaluate" is the call of `CelExpression.Evaluate` on an anonymous object that was built before the measurement, with the text of the variant with the input of the environment (the scalars, and a macro over the 10 lines of the order); "data" is the building of that object for one call, as the variant of the rig builds it (the scalar fields, the customer and the lines as properties, the classes as they are). Time and allocated bytes of both are in the table.
- **`CelMap.Create` of the row of an order**, a diagnostic without a verdict (`CelMapBenchmarks`): a map of three entries (`sku`, `qty`, `price`) made with a collection expression, the form that the variant of the simple entry used to turn a line into data. Time and allocated bytes.

## Running on GitHub

Start the workflow `bench` from the Actions page of the repository or with the command line of GitHub:

```text
gh workflow run bench.yml --ref main -f runs=3
```

| Input | Meaning |
| :- | :- |
| `runs` | the number of runs, one after another in one job, on one virtual machine; at least 3 for a result, 3 by default |
| `job` | `Default`: the measurement. `Dry`: one call per case, a check of the rig and the workflow without a measurement |
| `cases` | the ids of the cases separated by commas, for example `E1,E5`; without it every case |

The job restores in locked mode, then `run.mjs` builds, verifies and measures. The runner is `ubuntu-latest`, the SDK and the runtime (.NET 11) are those of `global.json`. The job `bench` has no secret and no write permission, and no time limit of its own: the limit of a job on GitHub, 6 hours, applies. The full measurement has 147 cases per run; the estimate of the time is about an hour per run on a laptop, so three runs fit in the limit with a margin. The result is the artifact `bench-results` (the summary, the verification, the table and the reports of every run) and the step summary of the job.

Check of the first run. Start `job=Dry` first: it builds the rig, verifies all libraries and calls every case once, which shows an error of the build or of an adapter in minutes. Then start the measurement and read the summary: the verification table (which pairs are excluded), the section "Conditions" (commit, runner, versions of the packages), the section "Measurement environment" (processor, runtime), and the number of runs of every row.

The result is published to the branch `reports` ([eng-release.md](eng-release.md#reports-of-the-runs)). After the measurement `.github/scripts/bench-report.mjs` makes the report from `bench/results/` — `report.json` (the date and the time in UTC, the commit of this repository, the run, the inputs and the runs), `report.md` (the summary of the rig), `verify.json` and the `run.json` and `summary.md` of each run — and the job `publish`, the only one with `contents: write`, commits it to `bench/<yyyy-MM-ddTHHmmssZ>/` of the branch. A measurement of the job `Default` with at least 3 runs, started on `main`, is published; a `Dry` run is a check of the rig, not a result, and it is not. The full reports of BenchmarkDotNet are not published: they stay in the artifact `bench-results` for 90 days.

## Packages

- The rig has its own `Directory.Build.props` and `Directory.Packages.props`, so the rules written for the library (warnings as errors, all analyzers, XML documentation, the architecture analyzer) and the versions of the packages of the repository do not apply to it, and the versions of the compared libraries and of the tool Grpc.Tools do not mix with the versions of the library.
- Central package management is on: every version is pinned in `bench/Directory.Packages.props`. `bench/SpeedBench/packages.lock.json` is the lock file; the workflow restores with `--locked-mode`. A result names these versions.
- A new version of a compared library is taken by a change of `bench/Directory.Packages.props` and of the lock file (`dotnet restore bench/SpeedBench/SpeedBench.csproj --force-evaluate`). Dependabot does not propose versions for `bench/`: they are pinned for a comparison.
- The version of Great.Parts.Cel in the rig is the published package from nuget.org. To measure the code of a release draft, change the version in `bench/Directory.Packages.props` to the version of the packages of the draft Release and add the directory of the packages as a source of packages in a `NuGet.config` of `bench/`.
- Protobuf messages of the rig are made at build time by Grpc.Tools 2.85.0, a build tool only; the runtime of the messages is the Google.Protobuf that the compared libraries bring.

## Checks of the repository

The checks of `eng/checks` that read C# files (`suppressions`, `duplicates`, `ratchet`) read the files of `bench/` too. The rig contains no suppression; its pairs of repeated code are counted in `duplicates` of `eng/ratchet.json`.

# Benchmark rig

The rig compares the speed of Great.Parts.Cel with other CEL libraries for .NET: compilation, evaluation and the full cycle on twelve scenarios (E1 to E9, three of them on 10 and 100 elements). Great.Parts.Cel is built from the sources of the commit that the rig runs on, in three variants of passing the data (below). The same run measures the limits of the speed-ups of the library (`ThresholdBench`, below). The rig, its logs and the results of its runs are published; the other libraries are named here by the names of their packages. The rig states numbers, versions and conditions of the measurement and nothing else about the libraries.

The projects of the rig target `net11.0`: the measurement runs on .NET 11 under JIT. It runs on a GitHub-hosted runner, by the workflow `.github/workflows/bench.yml` (manual start). The rig is not part of the packages of Great.Parts.Cel and is not built by `ci.yml`. The description of the rig, of the operations and of the run is in [docs/design/eng-bench.md](../docs/design/eng-bench.md).

## Packages

The versions are pinned in `Directory.Packages.props`; the lock file is `SpeedBench/packages.lock.json`. Great.Parts.Cel is not a package here: `SpeedBench` references the project `src/Great.Parts.Cel` and the project of its source generator as an analyzer.

| Package | Version | Role |
| :- | :- | :- |
| Great.Parts.Cel | sources of the commit | the library of this repository, by a project reference |
| Celly | 1.2.0 | compared library |
| DotnetCel | 1.0.7 | compared library |
| Cel.NET | 2.3.1 | compared library |
| Cel (TELUS) | 0.3.3 | compared library; under the assembly alias `TelusCel`, because its namespace `Cel` is also the namespace of Cel.NET |
| BenchmarkDotNet | 0.16.0-preview.2 | the benchmark runner (a preview: the release 0.15.8 cannot name .NET 11); both projects of the rig use it |
| Grpc.Tools | 2.85.0 | build only: protoc for `SpeedBench/Proto/order.proto` |

## How each library receives the data

The data are objects of the classes `Order`, `Line` and `Customer` (`SpeedBench/Model.cs`). Each adapter uses the way of passing data that the documentation of its library describes for application classes.

| Library | Data | Activation |
| :- | :- | :- |
| Great.Parts.Cel | the classes, read by the type context that the source generator writes at build time | `CelBindings` (`order` and `now` bound by name) |
| GPCel-Input | the same classes and type context; `UseInput<Order>()` makes the fields of the order variables of the top level, so the expressions are those of the scenarios without `order.` | `EvaluateInput` on the instance, only `now` in `CelBindings` |
| GPCel-Simple | the simple entry `CelExpression.Evaluate` with an anonymous object: the scalars of the order, the customer (`order.Customer`) and the lines (`order.Lines`) as properties, the classes as they are, without turning them into `CelMap` values; the expressions as for the input | no environment; no compilation of its own, so the row compile is absent, evaluate is the call on a text that is in the hidden cache, full cycle is the call on a new text |
| Celly | the classes, through `NativeTypeProvider`; `order` is a struct type | dictionary |
| DotnetCel | the classes, through the default POCO adapter with the camelCase naming convention | dictionary |
| Cel.NET | protobuf messages (`SpeedBench/Proto/order.proto`), built from the whole order graph on every evaluation | dictionary |
| Cel (TELUS) | protobuf messages, built from the whole order graph on every evaluation; `now` is a `DateTimeOffset` | dictionary |

A library whose result in a case differs from the expected value, or is an error, is excluded from that case: the verification table of every summary lists the exclusions with the text of the result. Every variant of Great.Parts.Cel is verified like a library.

The ratio of a variant of Great.Parts.Cel is to the fastest other library or to itself, not to its own other variants.

## Limits of the speed-ups

`ThresholdBench/` measures, on the code of the commit (it sees the internals of the library, `InternalsVisibleTo`), two limits and five diagnostics; `run.mjs` runs it after the runs of the rig, with the same BenchmarkDotNet job, and writes the section "Limits of the speed-ups" with a verdict for each limit and the rows of each diagnostic. The summary of the rig has the column Allocated (the median over the runs of the allocated bytes per operation, less the allocation of making the text).

| Limit | Measurement | Rule of the verdict |
| :- | :- | :- |
| the search for the literal prefix of `matches`: .NET up to 64 code units, Knuth-Morris-Pratt above | the text `(ab)^5000` and the patterns of 48, 64, 65 and 80 code units: KMP, the search of .NET, the search of the library | .NET at 64 is not slower than KMP; otherwise the limit is 32 |
| the build of the automaton of a pattern | the pattern E5 (and a longer one) against the parse and the build of the program | the median of the build of the automaton of E5 is at most 2 microseconds on the standard GitHub Linux runner |

The diagnostics have no verdict:

- the time of `contains` per unit of its cost, on the text `(ab)^5000` with patterns of 64, 256 and 1,024 code units, for a pattern that is a literal of the expression (`SearchValues<string>`) and for a pattern from a variable (the ordinal search), with the cost from the formula of the page [Cost](../docs/cost.md). A rise of the time per unit above two times from 64 to 1,024 code units is the input of a decision on the divisor 64 of the factor of the cost of a search;
- the index of `distinct` and `sets` (the scan of the hashes up to 64 values, buckets above): lists of 8, 16, 24, 32, 48 and 64 integers with the scan forced, the buckets forced, the index of the library, one hash for every value and the pairwise group, and the evaluation of the programs through the public API on lists of 16 to 64. The summary prints the threshold by the rule (the largest N at which, and at every smaller N, scan / buckets is at most 1.0 for both operations; 8 if the scan is slower at 8); the limit of the library is set by a decision on the table;
- the simple entry, evaluate: the call `CelExpression.Evaluate` on an anonymous object that was built before the measurement, with the text of E1 and of E2 without the prefix `order.`;
- the simple entry, data: the building of that anonymous object for one call, as the variant `GPCel-Simple` builds it;
- `CelMap.Create` on the row of an order (three entries, a collection expression): time and allocated bytes.

## Run

```text
gh workflow run bench.yml --ref main -f runs=3
```

The input `thresholds` (default true) turns the measurement of the limits on or off. The inputs, the check of a first run and the files of a result are in [docs/design/eng-bench.md](../docs/design/eng-bench.md). The measurement is not run on a developer machine. A dry check (`--job Dry`: one call per case, no measurement) can run `run.mjs` with the SDK of a container: the environment variable `BENCH_DOTNET` replaces `dotnet`, for example `BENCH_DOTNET="docker exec -w /repo/bench <container> dotnet"`.

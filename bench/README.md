# Benchmark rig

The rig compares the speed of Great.Parts.Cel with other CEL libraries for .NET: compilation, evaluation and the full cycle on twelve scenarios (E1 to E9, three of them on 10 and 100 elements). The rig, its logs and the results of its runs are published; the other libraries are named here by the names of their packages. The rig states numbers, versions and conditions of the measurement and nothing else about the libraries.

The measurement runs on a GitHub-hosted runner, by the workflow `.github/workflows/bench.yml` (manual start). The rig is not part of the packages of Great.Parts.Cel and is not built by `ci.yml`. The description of the rig, of the operations and of the run is in [docs/design/eng-bench.md](../docs/design/eng-bench.md).

## Packages

The versions are pinned in `Directory.Packages.props`; the lock file is `SpeedBench/packages.lock.json`.

| Package | Version | Role |
| :- | :- | :- |
| Great.Parts.Cel | 1.0.1 | the library of this repository, from nuget.org |
| Celly | 1.2.0 | compared library |
| DotnetCel | 1.0.7 | compared library |
| Cel.NET | 2.3.1 | compared library |
| Cel (TELUS) | 0.3.3 | compared library; under the assembly alias `TelusCel`, because its namespace `Cel` is also the namespace of Cel.NET |
| BenchmarkDotNet | 0.15.8 | the benchmark runner |
| Grpc.Tools | 2.85.0 | build only: protoc for `SpeedBench/Proto/order.proto` |

## How each library receives the data

The data are objects of the classes `Order`, `Line` and `Customer` (`SpeedBench/Model.cs`). Each adapter uses the way of passing data that the documentation of its library describes for application classes.

| Library | Data | Activation |
| :- | :- | :- |
| Great.Parts.Cel | the classes, read by the type context that the source generator writes at build time | `CelBindings` |
| Celly | the classes, through `NativeTypeProvider`; `order` is a struct type | dictionary |
| DotnetCel | the classes, through the default POCO adapter with the camelCase naming convention | dictionary |
| Cel.NET | protobuf messages (`SpeedBench/Proto/order.proto`), built from the whole order graph on every evaluation | dictionary |
| Cel (TELUS) | protobuf messages, built from the whole order graph on every evaluation; `now` is a `DateTimeOffset` | dictionary |

A library whose result in a case differs from the expected value, or is an error, is excluded from that case: the verification table of every summary lists the exclusions with the text of the result.

## Run

```text
gh workflow run bench.yml --ref main -f runs=3
```

The inputs, the check of a first run and the files of a result are in [docs/design/eng-bench.md](../docs/design/eng-bench.md). The measurement is not run on a developer machine.

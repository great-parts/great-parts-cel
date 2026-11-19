# Coverage of the tests

The workflow `.github/workflows/coverage.yml` measures how much of the code of `src/` the tests run. The measure is the coverage of statements: the share of the lines of code with a sequence point (the points where a debugger can stop) that a test run reaches. The goal is at least 80 % over the three projects of `src/` together. The workflow measures and reports; it does not fail a run for a low value, and it is not a condition of a release.

The measurement is one part of the evidence for the tests of the library; the other parts are the suite runner of cel-spec ([conformance.md](conformance.md)), the property tests ([tests.md](tests.md)), the differential run ([eng-difftest.md](eng-difftest.md)) and the mutation score ([eng-mutate.md](eng-mutate.md)).

## Tool

Coverlet, the package `coverlet.console` 10.1.1 (MIT), is a local tool in `.config/dotnet-tools.json`, like Stryker.NET ([eng-mutate.md](eng-mutate.md#tool-and-settings)); `dotnet tool restore` installs it and `dotnet tool run coverlet` starts it. It is a build tool of the repository only: no project of the repository refers to it, no package depends on it, and no lock file changes.

The way of Coverlet follows from how the tests run. A test module is an executable of xUnit.net v3 that the repository starts as `dotnet <module>.dll` ([tests.md](tests.md)), without `dotnet test`. The ways that Coverlet and Microsoft offer, and why only the first fits:

| Way | What it needs | Fit |
| :- | :- | :- |
| `coverlet.console` (a global or local tool) | nothing in the projects: it instruments the assemblies in a directory and starts any program as its target | the module runs as it does in `ci.yml`, under JIT, with no change of a project, of a lock file or of a Native AOT publication; chosen |
| `coverlet.collector` | a package in every test project and the VSTest platform (`dotnet test --collect`) | the repository does not use VSTest; a package reference in a test project changes its lock file and reaches the Native AOT publication of two projects |
| `coverlet.msbuild` | a package in every test project and the targets of `dotnet test` | the same costs as the collector |
| `coverlet.MTP` | a package in every test project and a module that runs on Microsoft Testing Platform (`--coverlet`) | the modules run in the native mode of xUnit.net for `ci.yml`; the package would reach the Native AOT publication of two projects, which a trimming or AOT warning fails ([ci.yml](../../.github/workflows/ci.yml)) |
| `Microsoft.Testing.Extensions.CodeCoverage` and `dotnet-coverage` (Microsoft) | the extension needs Microsoft Testing Platform and a package in every project; `dotnet-coverage collect` runs any program but is another tool, with the dynamic coverage of the runtime | an alternative; not taken, because Coverlet was asked for and the tool of Coverlet needs no project change |

## The run

The job `coverage`, on `ubuntu-latest`, runs the steps of the JIT cells of [ci.yml](../../.github/workflows/ci.yml) that it needs, then measures:

1. Check out, set up .NET (the SDK of `global.json` and the SDK 10.0.x), `dotnet restore Great.Parts.Cel.slnx --locked-mode`, `dotnet tool restore`.
2. `dotnet build Great.Parts.Cel.slnx -c Release --no-restore -p:ContinuousIntegrationBuild=false`. A deterministic build (`ContinuousIntegrationBuild=true`, which `Directory.Build.props` sets on GitHub) writes `/_/` into the debug information in place of the path of each source file, and Coverlet then finds no sources; so this job builds without it. Nothing built here is released.
3. For each of four modules, `dotnet tool run coverlet <directory of the module> --target dotnet --targetargs "<module>.dll -noColor"`, with `--include` for the assemblies of the module, `--exclude-by-file "**/obj/**"`, `--merge-with artifacts/coverage/coverage.json` from the second run on, `--format json --format cobertura` and `--output artifacts/coverage/`. The runs go one after another, and each one merges its result into the file of the runs before it.
4. `node .github/scripts/coverage-report.mjs` reads `artifacts/coverage/coverage.cobertura.xml`, writes `summary.md`, `uncovered.md` and `summary.json` into `artifacts/coverage/`, and `summary.md` is added to the summary of the run.
5. The directory `artifacts/coverage` is uploaded as the artifact `coverage`.

| Module | Target | Assemblies measured |
| :- | :- | :- |
| `tests/Great.Parts.Cel.Tests` | `net11.0` | `Great.Parts.Cel`, `Great.Parts.Cel.Protobuf` |
| `tests/Great.Parts.Cel.Conformance` | `net11.0` | `Great.Parts.Cel`, `Great.Parts.Cel.Protobuf` |
| `tests/Great.Parts.Cel.Properties` | `net11.0` | `Great.Parts.Cel`, `Great.Parts.Cel.Protobuf` |
| `tests/Great.Parts.Cel.Generator.Tests` | `net10.0` | `Great.Parts.Cel.Generator` |

- The library is measured on `net11.0`, whose code is a superset of the code of `net10.0` (the code of `Decimal128` compiles only for `net11.0`; the code of `System.Decimal` that replaces it on `net10.0` is not measured). `Great.Parts.Cel.Generator.Tests` has the one target `net10.0`, and its build of the library is another build than the one of `net11.0`: the run measures the generator only, a library of `netstandard2.0` that the tests run in process.
- The packages `Great.Parts.Cel.DependencyInjection` and `Great.Parts.Cel.AspNetCore` are not measured yet: their test projects, `tests/Great.Parts.Cel.DependencyInjection.Tests` and `tests/Great.Parts.Cel.AspNetCore.Tests`, are not modules of the job.
- The test projects, `eng/` and `bench/` are not measured: only the code of `src/` is the subject of the criterion. The generated code of `obj/` is excluded.
- The measure of the criterion is the line coverage of Coverlet. The branch coverage is in the summary for information. Native AOT is not measured: it runs the same tests on the same code, and the published executables are not instrumented.

## Workflow

- Triggers: a push to `main`, a pull request and a manual start (`gh workflow run coverage.yml --ref main`). It is a workflow of its own, not a job of `ci.yml`: a run in which a test fails under the instrumentation (the instrumented code is slower and its stacks are deeper) must not turn `ci.yml` red, because the release gate `ENG908` ([eng-release.md](eng-release.md)) asks `ci.yml` to be green on the commit of the tag.
- The step of the tests runs all four modules, whatever the result of each, and ends with a failure when one of them failed. The report and the artifact are made after a failed step too, from what the runs merged.
- The permission is `contents: read`; the actions are pinned by SHA, and Dependabot proposes their updates; the workflow sets no time limit of its own, the limit of a job on GitHub, 6 hours, applies. There is no secret.

## Reading the result

`summary.md` has the verdict (whether the coverage is at least 80 %), the table of the three projects of `src/` (files, statements, covered, coverage) and the thirty files with the most uncovered statements with their ranges of lines. `uncovered.md` lists every file that has an uncovered statement, and `summary.json` has the numbers. The ranges are runs of uncovered lines of code with no covered line between them.

The result is a number of one commit. A task to raise the coverage starts from `uncovered.md` of the last run on `main`.

## Files

| Path | Contents |
| :- | :- |
| `.config/dotnet-tools.json` | the version of Coverlet |
| `.github/workflows/coverage.yml` | the workflow |
| `.github/scripts/coverage-report.mjs` | the report; Node.js without npm dependencies |

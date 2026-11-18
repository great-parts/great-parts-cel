# eng/mutate

`eng/mutate` measures the mutation score of the changed code: Stryker.NET changes the code of the library in small ways (mutants), runs the tests and counts the mutants that the tests detect. The score of the changed files must be at least 80 %. It is the service `mutate` of `eng/compose.yaml` with the image stage `sdk`, see [eng-test.md](eng-test.md).

## Tool and settings

- Stryker.NET 5.1.0, the package `dotnet-stryker`, is a local tool in `.config/dotnet-tools.json`; `dotnet tool restore` installs it. It runs on the .NET 10 runtime of the image. Observations below that name version 5.0.0 were made before the update and are checked again by the first run of the parts on 5.1.0.
- The settings are in `stryker-config.json` in the repository root:

| Setting | Value | Why |
| :- | :- | :- |
| project | `src/Great.Parts.Cel/Great.Parts.Cel.csproj` | the code under test |
| test projects | `../../tests/Great.Parts.Cel.Tests/Great.Parts.Cel.Tests.csproj`, `../../tests/Great.Parts.Cel.Properties/Great.Parts.Cel.Properties.csproj`, `../../tests/Great.Parts.Cel.Conformance/Great.Parts.Cel.Conformance.csproj` | the tests of the library and the suite runner; project files, not directories, see [Test projects and since](#test-projects-and-since); relative to the directory of the project under test, see below |
| mutate | not set | every file of the project; a narrow pattern is not always visible, see [mutation](#mutation) |
| target framework | `net11.0` | its code is a superset: the `decimal` code compiles only for `net11.0` |
| test runner | `mtp` | the test platform of the repository |
| coverage analysis | `perTest` | each mutant runs only the tests that cover it |
| reporters | `json`, `progress` | the report for `eng/checks mutation`; progress in the output |
| mutation level | `Standard` | |
| since | enabled, target `main` | only the files changed against the target are mutated; the command can give another commit as the target ([Steps](#steps)) |
| thresholds | `high` 80, `low` 80, `break` 80 | a score below 80 % makes the exit code of the tool non-zero |

Stryker.NET runs in the directory of the project under test (`src/Great.Parts.Cel/` for the library, `eng/checks/` for the control set), not in the repository root. Since 5.1.0 the tool stops with `Test projects cannot be specified when running Stryker in solution context.` when its working directory holds a solution file and the configuration names `test-projects` or `project`; the repository root holds `Great.Parts.Cel.slnx`, so a run from there is a run in solution context. (The version 5.0.0 ignored the two keys in that context and took every project of the solution, which was not the run that the configuration describes.) The directories of the projects hold no solution file, so the tool runs in project context, with the test projects named in the configuration. `eng/run` takes the directory from the key `project` of the configuration file (`MutateOptions.ProjectDirectory`) and gives the tool the paths of `--config-file` and `--output` relative to it, as `../../stryker-config.json` and `../../artifacts/mutate`; the step is the same for every configuration. So the paths of `test projects` are relative to the directory of the project under test, and `project` stays relative to the repository root: the tool takes it as a part of the path of the project file, and `eng/checks mutation` reads it as a path from the root. The patterns of `mutate` are taken from the directory of the project under test, as before. A test of `eng/tests` keeps the rule: for every configuration of the repository, the directory of its project holds no solution file and every test project exists relative to it.

The verdict of a property test is deterministic ([tests.md](tests.md#property-tests-and-the-seed)), so two runs on the same code give the same score.

## Steps

```text
docker compose -f eng/compose.yaml run --rm mutate [<configuration>] [--since <commit>]
```

The command takes at most one configuration file and at most one `--since <commit>`, in any order. Without arguments it uses `stryker-config.json`, and the changes are counted from the target of its `since`, the branch `main`.

`--since <commit>` is the commit from which the changes are counted: the files changed against it are mutated, whatever the target of the configuration says. It is meant for the commit of the previous run, so that each run covers the changes made since. The command resolves the value to a full SHA (`git rev-parse --verify <commit>^{commit}`, so a short SHA and a branch name work) and passes that SHA on: to the tool as `--since:<SHA>` and to the check of step 4 as `--since <SHA>`. Stryker.NET takes a full SHA or a reference but not a short SHA, and the tool and the check must count the changes from the same commit. `--since:<SHA>` enables `since` in the tool even when the configuration disables it, so the check then lists the changed files too ([mutation](#mutation)): a run of the control set with `--since` mutates only the changed files of its `mutate` list and gets `ENG504` for a changed file of `eng/checks` that it left out. A value that is not a commit of the repository stops the command with `mutate: failed: --since '<value>' is not a commit of this repository` before any step.

1. `dotnet tool restore`.
2. `dotnet build eng/checks -c Release`: `eng/checks` is built once here and run without a build in step 4. A failed build stops the command.
3. `dotnet stryker --config-file <configuration> --output artifacts/mutate`, with `--since:<SHA>` when the command got `--since`, run in the directory of the project under test with the two paths relative to it ([Tool and settings](#tool-and-settings)). The configuration is `stryker-config.json` unless the command gets another path as its argument.
4. `eng/checks mutation --report artifacts/mutate/reports/mutation-report.json --config <configuration>`, with `--since <SHA>` when the command got `--since`, as `dotnet run --project eng/checks -c Release --no-build`.

Before step 1 the command checks its arguments and then `/repo/.git`: when it is a file, the working copy is a linked worktree, and a run with `since` prints `mutate: failed: linked worktree: a run with since needs the main worktree` and returns 1 without running a step ([Linked worktrees](#linked-worktrees)). The value of `--since` is resolved after that check.

Step 4 runs even when step 3 returns a non-zero exit code, so the surviving mutants are printed as findings. The exit code is 0 when every step returned 0, otherwise 1. Output: the progress of the tool and the finding lines on standard output; the report in `artifacts/mutate/`.

- `since` compares the working copy with the commit of `--since`, or without it with the target of the configuration, the local branch `main`; the working copy needs that commit or branch and the history down to the merge base. It takes untracked files as changed too, such as the `.vs/` directory of an IDE; they select mutants only when they are C# files of the project under test.
- A mutant that cannot change behavior (an equivalent mutant) is marked in the code with a Stryker.NET comment that gives the reason; the mark is visible in the change.
- A suspicious surviving mutant is checked again with coverage analysis `off`, as the tool's documentation advises.

## Time limits

`eng/mutate` sets no time limit on a step, the step that runs Stryker.NET included ([eng-test.md](eng-test.md#time-limits)). The tool writes nothing about its progress while it tests the mutants, so a run that hangs shows no sign of it: whoever runs the command stops it. Step 4 reads the report that the tool had written, if there is one.

## Control set

`eng/stryker-control.json` is the second configuration: `eng/mutate eng/stryker-control.json`. It checks the tool and the test runner on code whose tests are known ([eng-tests.md](eng-tests.md)).

| Setting | Value | Why |
| :- | :- | :- |
| project | `eng/checks/Great.Parts.Cel.Eng.Checks.csproj` | the code of the set |
| test projects | `../tests/Great.Parts.Cel.Eng.Tests.csproj` | its tests; relative to `eng/checks`, the directory in which the tool runs ([Tool and settings](#tool-and-settings)) |
| target framework | `net10.0` | `eng/checks` and `eng/tests` build only for `net10.0` |
| mutate | a fixed list of whole files of `eng/checks`, relative to `eng/checks` | a run of minutes; whole files, because a span is a range of character indices and moves with any edit above it |
| since | disabled | the set does not depend on the change |
| thresholds | `high`, `low`, `break` 100 | every mutant of the set must be detected |

The other settings repeat those of `stryker-config.json`. The set runs with its own file and inherits nothing from `stryker-config.json`, so a change of a common setting there is made in `eng/stryker-control.json` too. The files of `mutate` are files whose every mutant the tests of `eng/tests` detect. A mutant of the set that is not detected is a fault of the tool, of the runner or of the tests, and the run fails.

The set has no `since`, so it runs from a linked worktree too ([Linked worktrees](#linked-worktrees)).

### Triggers

`ci.yml` does not run the set. It runs before release 1.0, with the workflow `mutate.yml` ([Run on GitHub](#run-on-github)), together with `eng/mutate` on the whole library in parts ([Parts of the library](#parts-of-the-library)): `eng/mutate eng/stryker-control.json`, which checks the tool and the test runner, and the runs of the parts, whose scores are read. A run before the release needs no filter by paths. By hand, for example after a change of the Stryker.NET version, the set is needed when a change touches a path of the list below; the check is `git diff --name-only main...HEAD -- <paths>`. They are the paths on which the result of the set depends: the set itself — its configuration, the code that it mutates and the code that code uses, the tests that detect its mutants; the steps of `eng/mutate`; the container; the settings of the compiler; the versions of the tool, of the test platform, of the SDK and of the image.

| Path | Why |
| :- | :- |
| `eng/stryker-control.json` | the configuration of the set |
| `eng/checks/` | the mutated files, the code they use, the check `mutation`, the project and its lock file |
| `eng/findings/` | the findings that the mutated commands return |
| `eng/tests/Checks/`, `eng/tests/Support/` | the tests that detect the mutants of the set and their shared helpers |
| `eng/tests/Great.Parts.Cel.Eng.Tests.csproj`, `eng/tests/packages.lock.json` | the test project and its package versions, among them those of the test platform |
| `eng/run/` | the steps of `eng/mutate` |
| `eng/compose.yaml` | the service `mutate` |
| `eng/compose.worktree.yaml` | the mounts of the service `mutate` in a linked worktree, from which the set runs ([Linked worktrees](#linked-worktrees)) |
| `eng/docker/` | the image: the SDK and the runtime |
| `.config/dotnet-tools.json` | the version of Stryker.NET |
| `global.json` | the SDK and the mode of the test platform |
| `Directory.Build.props` | the settings of the compiler that Stryker.NET takes into the compilation of the mutants: `Nullable`, `WarningsAsErrors`, `WarningsNotAsErrors`, `NoWarn`; a change can turn mutants into `CompileError` without failing the build |

Not triggers: `src/`, `tests/`; in `eng/tests/` — `Difftest/`, `Analyzers/`, `EngRun/`, `Findings/`, `CodeRegistryTests.cs`; `eng/difftest/`, `eng/oracle/`, `eng/analyzers/`, `eng/ratchet.json`, `eng/layers.txt`, `eng/BannedSymbols.txt`, `eng/min-sdk/`, `eng/packages/`; in the root — `stryker-config.json`, `Directory.Packages.props`, `NuGet.config`, `.editorconfig`; the documents. Such a change breaks the set only through the build or a failing test, and the jobs of the build and of the tests of `ci.yml` show that. A change of package versions in `Directory.Packages.props` reaches the set through the lock file of `eng/tests` or `eng/checks`, which is a trigger. Stryker.NET 5.0.0 compiles the mutants without the analyzer configuration: the severities of `.editorconfig` do not reach that compilation, which runs no analyzer.

## Run on GitHub

`.github/workflows/mutate.yml` runs `eng/mutate` by hand (`workflow_dispatch`), on `main` only: a run from another branch is skipped. It is the only place where the control set and the run on the whole library are made in CI; `ci.yml` has neither. The workflow runs the Compose service `mutate` on hosted runners, as `difftest-nightly.yml` runs `difftest`, so a run on GitHub is a run on a developer machine, and it needs no tool setup: Stryker.NET is a local tool of the repository. It has the permission `contents: read` only, its actions are pinned by SHA, and it sets no `timeout-minutes`: a job that hangs is stopped by the default limit of GitHub Actions, 6 hours ([eng-test.md](eng-test.md#ci)).

The run of the whole library in one job does not end within that limit (the run of 2026-11-16 was stopped by it with no report), so the library is mutated in [parts](#parts-of-the-library), and each part is a job of its own. The workflow has three jobs:

| Job | What it does |
| :- | :- |
| `plan` | lists the jobs to run: `control` when the run has the control set, and the parts of the library when it has the library; the list is the matrix of the next job |
| `mutate` | a matrix job, one job for the control set and one for each part, each on its own runner; `fail-fast` is off, so a job that fails or is stopped by the limit does not stop the others. The control set runs `eng/mutate eng/stryker-control.json`, a part runs `eng/mutate eng/stryker-parts/<part>.json --since <commit>` |
| `library` | adds up the reports of the parts: the number of mutants by status and the score of the library; it names a part that has no report |

| Input | Value | Meaning |
| :- | :- | :- |
| `runs` | `both` (default), `control`, `library` | `control` is the control set only; `library` is the parts of the library only, the scores whose result is read; `both` is the two |
| `parts` | names of the files of `eng/stryker-parts/` without `.json`, separated by commas; without it, every part | which parts of the library to run, for example to repeat a part that was stopped; a name that is not a part stops the run in `plan` |
| `since` | a SHA or a branch name; without it, the first commit of the history | the commit from which a part counts the changes ([Steps](#steps)); the first commit makes every file of the library a changed file, so the parts together mutate the whole library |

Every job checks out the whole history, as `since` and `eng/run` read git. The output of `eng/mutate` goes to the log of the job as it is written, not at the end of the run, so a job that is stopped has shown in its log how far it got; the same output is in the artifact. `eng/run` deletes `artifacts/mutate` before a run, so the job copies the output of the run away at once.

### Reading the result

- The artifact `mutate-results-<job>` of each job (`control` or a part) has the file `<job>.txt`, the whole output with the finding lines (`ENG501` to `ENG504`, [mutation](#mutation)), and the directory `<job>/` with the report `reports/mutation-report.json` and the other files of Stryker.NET.
- The summary of each job has the last line of `eng/run` (`mutate: passed` or `mutate: failed`, the commit, the duration, the exit code), the number of finding lines and the number of mutants by status of the report. The summary of the job `library` has the number of mutants by status and the score over the reports of all parts. The score is detected mutants (`Killed`, `Timeout`) divided by those and the not detected (`Survived`, `NoCoverage`); the statuses that are not counted are in [mutation](#mutation).
- A job is red when a run ends with a non-zero exit code: a score of the part below the threshold (`ENG503`), a changed file without a score (`ENG504`), a failed step or, in the control set, a mutant that the tests of `eng/tests` did not detect. A surviving mutant alone (`ENG501`, `ENG502`) is a warning while the score is at or above the threshold, as everywhere in `eng/mutate`. The threshold is that of the part: a weak part is not hidden by the others.
- The surviving mutants are read from the finding lines or from the reports; the next step is the same as for a local run: kill the mutant with a test, or mark it equivalent in the code with a Stryker.NET comment that gives the reason ([Steps](#steps)).

## Parts of the library

The configuration of a part is `eng/stryker-parts/<part>.json`: the settings of `stryker-config.json` and the key `mutate` with the patterns of the files of the part. A part is run as any configuration, `eng/mutate eng/stryker-parts/<part>.json [--since <commit>]`, and the check of step 4 reads the same file ([mutation](#mutation)). The parts cut the sources of `src/Great.Parts.Cel/` by directory, and the directory `Values/` by the first letters of the names, by about the same size of the sources (100 to 250 KB):

| Part | Patterns of `mutate`, relative to the project |
| :- | :- |
| `values-cel` | `Values/Cel*.cs` |
| `values-conversion` | `Values/Co*.cs` |
| `values-a-m` | `Values/A*.cs`, `Ca*.cs`, `D*.cs`, `E*.cs`, `F*.cs`, `I*.cs`, `J*.cs`, `K*.cs`, `M*.cs` |
| `values-o-z` | `Values/O*.cs`, `T*.cs`, `V*.cs`, `Z*.cs` |
| `library-extensions` | `Library/Extensions/*.cs`, `Library/Extensions/Network/**/*.cs` |
| `library-regex` | `Library/Regex/**/*.cs` |
| `library-strings` | `Library/Extensions/Strings/`, `Library/Time/`, `Library/Decimals/`, `Library/Unicode/` |
| `library-core` | `Library/*.cs`, `Types/**/*.cs` |
| `syntax` | `Syntax/**/*.cs` |
| `evaluation` | `Evaluation/**/*.cs` |
| `declarations` | `Declarations/**/*.cs` |
| `root-diagnostics` | `*.cs` (the files of the project directory), `Diagnostics/**/*.cs` |
| `checking-configuration` | `Checking/**/*.cs`, `Configuration/**/*.cs` |

- Two tests of `eng/tests` keep the cut of the parts: every `.cs` file of the project is in exactly one part, so a new directory or file that no part names fails `eng/test`, and a part file differs from `stryker-config.json` only in `mutate`, so a change of a common setting is made in every file, as for the control set ([Control set](#control-set)).
- `since` and `mutate` work together in the tool: a part mutates the changed files that its patterns name. Stryker.NET marks the other files' mutants `Removed by mutate filter`; `eng/checks mutation` knows the patterns, so a changed file outside them is no `ENG504` in this part, and one inside them that the report lacks is ([mutation](#mutation)).
- A part that is stopped by the limit of 6 hours is cut again: its patterns are divided between two files. The time of a part was not known when the parts were cut, as the one-job run showed no progress; the log of the first run of the parts gives the time of each part.
- The patterns use `*`, `**` and `?` only, as the check understands only them; the documentation of Stryker.NET names no other syntax for a name, and a span after a file (`File.cs{10..100}`) is left out by the check.

## Test projects and since

Stryker.NET 5.0.0 takes a changed file for a test file when its path starts with the path of a test project followed by a separator. The test projects are given as project files, so no changed file starts with such a path, and every changed file counts as a source file:

- a changed file of a test project selects no mutants: a changed test does not add the mutants of the code it covers;
- a changed non-C# file of a test project, such as a list of skipped tests, does not select every mutant of the library. With directories it would, and the run would mutate the whole library.

So `since` mutates exactly the changed C# files of the project under test, and `since.ignore-changes-in` is not needed. A new version of the tool is checked for this rule again.

## Memory rows under the tool

Stryker.NET 5.0.0, in solution context, built the solution in the configuration `Debug` (`Using solution configuration/platform 'Debug|Any CPU'` in its output) and compiles the project under test again with the code it injects into every branch; the tests of the first run, the initial test run, execute that build. The rows of memory ([tests.md](tests.md#cost-and-memory-of-the-extension-sets)) are calibrated on the optimized build, so a row with a small margin can allocate more than B per unit there.

With the test runner `mtp` one failed test of the initial run stops the run, whatever the number of tests: the tool divides the number of failed tests by the number of tests it counts as executed, which is 0 when the run executed every test, and ends with `Initial testrun has more than 50% failing tests.` and the names of the failed tests, before a single mutant. The run on `main` of 2026-11-16 (the run on the whole library, 12 027 tests) named one test, `ProtobufCostTests.Row_AllocatesWithinCost` of the row `protobuf, map field, traversal`, which is calibrated at 62.7 bytes per unit of B = 64; `eng/checks mutation` then gave `ENG504` for every changed file, as there was no report.

A row that fails in the initial run is added with `optimizedOnly: true`: it is skipped on a library that is not optimized or holds the injected `MutantControl`, and measured as before everywhere else ([tests.md](tests.md#cost-and-memory-of-the-extension-sets), "The build of the calibration"). The mutants that only that row would detect are not detected in a run of the tool; every other row of memory runs in it and detects the mutants of the charges.

## Linked worktrees

`since` works only in a working copy whose `.git` is a directory; a run without it reads no git through the tool (Stryker.NET 5.0.0 opens the repository only for `since` and the baseline), so the control set runs from a linked worktree with the second Compose file, as `eng/test` does ([eng-test.md](eng-test.md#linked-worktrees)):

```text
ENG_GIT_COMMON_DIR="$(git rev-parse --path-format=absolute --git-common-dir)" \
ENG_WORKTREE_NAME="$(basename "$(git rev-parse --show-toplevel)")" \
  docker compose -f eng/compose.yaml -f eng/compose.worktree.yaml run --rm mutate eng/stryker-control.json
```

The mounts give `eng/run` the commit and the state of the tree for its last line and the build its SourceLink lookup. A run with `since` — the configuration enables it, or the command gets `--since` — stops in a linked worktree before any step. Stryker.NET reads git through libgit2. In a linked worktree libgit2 takes the working tree from the file `gitdir` in the worktree's directory under the common git directory, and that file holds a host path; Stryker.NET 5.0.0 takes the repository root from the path of the git directory. In the container neither is `/repo`, so the changed files would not match the files of the project, and a run could pass without a score. The stop makes this visible.

A run with `since` of a change made in a linked worktree is made from the main worktree of the repository: commit the change, run `git switch --detach <branch>` there (git allows a detached checkout of a branch that another worktree holds), run `eng/mutate`, then switch back.

## mutation

A command of [eng/checks](eng-checks.md). It reads the JSON report in the mutation-testing-elements schema: for every file — its mutants with the mutator name, the replacement, the start line and column, and the status.

| Status | Counts as |
| :- | :- |
| `Killed`, `Timeout` | detected |
| `Survived` | not detected: `ENG501` at the place of the mutant |
| `NoCoverage` | not detected: `ENG502` at the place of the mutant |
| `CompileError`, `RuntimeError`, `Ignored`, `Pending` | not counted |

A mutant that a filter of the tool leaves out is `Ignored`, and its `statusReason` is `Removed by <filter>`: `Removed by since filter` in a file that `since` did not select, `Removed by mutate filter` at a place outside the `mutate` patterns. Other reasons come from other filters or from a Stryker.NET comment in the code. A mutant whose code does not compile is `CompileError` with `Mutant caused compile errors`.

- Score: detected mutants divided by detected and not detected mutants. A report without counted mutants has no score and no `ENG503`.
- Changed files are checked only when `since` is enabled: by the configuration — an object `since`, unless its `enabled` is `false`, as Stryker.NET reads the file — or by the option `--since`, which enables it in the tool too ([Steps](#steps)). They are the `.cs` files under the directory of the project under test that `git diff --name-only --no-renames <commit>` lists, deleted files excluded; `<commit>` is the value of the option `--since`, or `main` without it. A changed file is `ENG504` at `(1,1)` of the file when it is missing from the report, or when it has mutants, none of them counted, and at least one of them has the reason `Removed by since filter` or `Removed by mutate filter`: a wrong `since` target or a narrow `mutate` pattern would otherwise pass without a score. A changed file that the patterns of `mutate` of the configuration leave out is not looked at: the run was not to mutate it, as in a part of the library ([Parts of the library](#parts-of-the-library)); the patterns are read from the configuration file by the check, and without `mutate` every file is in. A changed file without any mutant, such as a file of declarations only, is no finding; nor is a file whose mutants are not counted for other reasons, such as a comment that marks them equivalent or code that does not compile.
- Without `since` and without `--since` — the control set — there are no changed files and no `ENG504`: such a run mutates what its configuration names, whatever the change.
- Stryker.NET 5.0.0 mostly writes a file that the `mutate` patterns leave out wholly without any mutant, so `ENG504` sees such a file only when the tool keeps some of its mutants with the reason above. This is why the main configuration sets no `mutate`; the parts of the library set it, and the check then does not ask for a score of a file outside the part.
- A change without a changed `.cs` file of the project under test has no score and no finding, exit code 0. Stryker.NET 5.0.0 still writes a report then: the mutants of the unchanged files are `Removed by since filter` or `CompileError`.
- A missing report, when `since` is enabled, counts as a report without mutants: no score, and every changed file is `ENG504`. Without `since` it is an input that does not exist, exit code 2 ([eng-checks.md](eng-checks.md#finding-and-output)). A failed step 2 fails `eng/mutate` either way.
- Threshold: `break` from the thresholds of the configuration file; the report has only `high` and `low`. A score below `break` is `ENG503` at the line of `break`.
- Level: when the score is below the threshold, `ENG501` and `ENG502` are errors; otherwise they are warnings. The mutants stay visible, and the threshold and `ENG504` decide whether the run fails.
- The message of a mutant names the mutator, the original code — the text of the source file at the mutant's place — and the replacement:

```text
src/Great.Parts.Cel/Library/IntArithmetic.cs(57,24): error ENG501: survived mutant (Arithmetic): 'a + b' -> 'a - b'
```

- The report gives absolute paths of the container, such as `/repo/src/Great.Parts.Cel/Library/IntArithmetic.cs`; they become relative to the repository root.

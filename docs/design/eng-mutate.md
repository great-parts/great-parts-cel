# eng/mutate

`eng/mutate` measures the mutation score of the changed code: Stryker.NET changes the code of the library in small ways (mutants), runs the tests and counts the mutants that the tests detect. The score of the changed files must be at least 80 %. It is the service `mutate` of `eng/compose.yaml` with the image stage `sdk`, see [eng-test.md](eng-test.md).

## Tool and settings

- Stryker.NET 5.0.0, the package `dotnet-stryker`, is a local tool in `.config/dotnet-tools.json`; `dotnet tool restore` installs it. It runs on the .NET 10 runtime of the image.
- The settings are in `stryker-config.json` in the repository root:

| Setting | Value | Why |
| :- | :- | :- |
| project | `src/Great.Parts.Cel/Great.Parts.Cel.csproj` | the code under test |
| test projects | `tests/Great.Parts.Cel.Tests/Great.Parts.Cel.Tests.csproj`, `tests/Great.Parts.Cel.Properties/Great.Parts.Cel.Properties.csproj`, `tests/Great.Parts.Cel.Conformance/Great.Parts.Cel.Conformance.csproj` | the tests of the library and the suite runner; project files, not directories, see [Test projects and since](#test-projects-and-since) |
| mutate | not set | every file of the project; a narrow pattern is not always visible, see [mutation](#mutation) |
| target framework | `net11.0` | its code is a superset: the `decimal` code compiles only for `net11.0` |
| test runner | `mtp` | the test platform of the repository |
| coverage analysis | `perTest` | each mutant runs only the tests that cover it |
| reporters | `json`, `progress` | the report for `eng/checks mutation`; progress in the output |
| mutation level | `Standard` | |
| since | enabled, target `main` | only the files changed against the target are mutated; the command can give another commit as the target ([Steps](#steps)) |
| thresholds | `high` 80, `low` 80, `break` 80 | a score below 80 % makes the exit code of the tool non-zero |

The paths of `project` and `test projects`, of `--config-file` and of `--output` are taken from the working directory, the repository root in the container; the patterns of `mutate` are taken from the directory of the project under test.

The verdict of a property test is deterministic ([tests.md](tests.md#property-tests-and-the-seed)), so two runs on the same code give the same score.

## Steps

```text
docker compose -f eng/compose.yaml run --rm mutate [<configuration>] [--since <commit>]
```

The command takes at most one configuration file and at most one `--since <commit>`, in any order. Without arguments it uses `stryker-config.json`, and the changes are counted from the target of its `since`, the branch `main`.

`--since <commit>` is the commit from which the changes are counted: the files changed against it are mutated, whatever the target of the configuration says. It is meant for the commit of the previous run, so that each run covers the changes made since. The command resolves the value to a full SHA (`git rev-parse --verify <commit>^{commit}`, so a short SHA and a branch name work) and passes that SHA on: to the tool as `--since:<SHA>` and to the check of step 4 as `--since <SHA>`. Stryker.NET takes a full SHA or a reference but not a short SHA, and the tool and the check must count the changes from the same commit. A value that is not a commit of the repository stops the command with `mutate: failed: --since '<value>' is not a commit of this repository` before any step.

1. `dotnet tool restore`.
2. `dotnet build eng/checks -c Release`: `eng/checks` is built once here and run without a build in step 4. A failed build stops the command.
3. `dotnet stryker --config-file <configuration> --output artifacts/mutate`, with `--since:<SHA>` when the command got `--since`. The configuration is `stryker-config.json` unless the command gets another path as its argument.
4. `eng/checks mutation --report artifacts/mutate/reports/mutation-report.json --config <configuration>`, with `--since <SHA>` when the command got `--since`, as `dotnet run --project eng/checks -c Release --no-build`.

Before step 1 the command checks its arguments and then `/repo/.git`: when it is a file, the working copy is a linked worktree, and the command prints `mutate: failed: linked worktree` and returns 1 without running a step ([Linked worktrees](#linked-worktrees)). The value of `--since` is resolved after that check.

Step 4 runs even when step 3 returns a non-zero exit code, so the surviving mutants are printed as findings. The exit code is 0 when every step returned 0, otherwise 1. Output: the progress of the tool and the finding lines on standard output; the report in `artifacts/mutate/`.

- `since` compares the working copy with the commit of `--since`, or without it with the target of the configuration, the local branch `main`; the working copy needs that commit or branch and the history down to the merge base. It takes untracked files as changed too, such as the `.vs/` directory of an IDE; they select mutants only when they are C# files of the project under test.
- A mutant that cannot change behavior (an equivalent mutant) is marked in the code with a Stryker.NET comment that gives the reason; the mark is visible in the change.
- A suspicious surviving mutant is checked again with coverage analysis `off`, as the tool's documentation advises.

## Time limits

Every step has a time limit ([eng-test.md](eng-test.md#time-limits)). The step that runs Stryker.NET has two. The tool writes nothing about its progress while it tests the mutants, so a limit that counts from the last output is not possible; but it prints the number of mutants before it starts to test them:

- the preparation — the build, the discovery of the tests and the run that captures coverage — has 3 minutes from the start of the step; the restore of the tool in step 1 has the same limit;
- the line `<N> total mutants will be tested` replaces that limit with the time until the line plus 0.3 s for each mutant and 30 s for the end of the tool, which writes its report after the last mutant. A run of 812 mutants that took 118 s to prepare and 205 s to test is allowed 3 min and then 274 s; a run of 16 mutants that needed 5 s after the line is allowed 35 s.

A step that runs out of time is killed with its process tree, and the command ends with `mutate: failed: timeout after <N> s in step <step>`, exit code 1. Step 4 still runs, as after any failure of the tool, and reads the report that the tool had written, if there is one.

## Control set

`eng/stryker-control.json` is the second configuration: `eng/mutate eng/stryker-control.json`. It checks the tool and the test runner on code whose tests are known ([eng-tests.md](eng-tests.md)).

| Setting | Value | Why |
| :- | :- | :- |
| project | `eng/checks/Great.Parts.Cel.Eng.Checks.csproj` | the code of the set |
| test projects | `eng/tests/Great.Parts.Cel.Eng.Tests.csproj` | its tests |
| target framework | `net10.0` | `eng/checks` and `eng/tests` build only for `net10.0` |
| mutate | a fixed list of whole files of `eng/checks`, relative to `eng/checks` | a run of minutes; whole files, because a span is a range of character indices and moves with any edit above it |
| since | disabled | the set does not depend on the change |
| thresholds | `high`, `low`, `break` 100 | every mutant of the set must be detected |

The other settings are those of `stryker-config.json`. The files of `mutate` are files whose every mutant the tests of `eng/tests` detect. A mutant of the set that is not detected is a fault of the tool, of the runner or of the tests, and the run fails.

## Test projects and since

Stryker.NET 5.0.0 takes a changed file for a test file when its path starts with the path of a test project followed by a separator. The test projects are given as project files, so no changed file starts with such a path, and every changed file counts as a source file:

- a changed file of a test project selects no mutants: a changed test does not add the mutants of the code it covers;
- a changed non-C# file of a test project, such as a list of skipped tests, does not select every mutant of the library. With directories it would, and the run would mutate the whole library.

So `since` mutates exactly the changed C# files of the project under test, and `since.ignore-changes-in` is not needed. A new version of the tool is checked for this rule again.

## Linked worktrees

`since` works only in a working copy whose `.git` is a directory. Stryker.NET reads git through libgit2. In a linked worktree libgit2 takes the working tree from the file `gitdir` in the worktree's directory under the common git directory, and that file holds a host path; Stryker.NET 5.0.0 takes the repository root from the path of the git directory. In the container neither is `/repo`, so the changed files would not match the files of the project, and a run could pass without a score. The stop makes this visible.

A change made in a linked worktree is checked from the main worktree of the repository: commit the change, run `git switch --detach <branch>` there (git allows a detached checkout of a branch that another worktree holds), run `eng/mutate`, then switch back.

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
- Changed files are checked only when the configuration enables `since`. They are the `.cs` files under the directory of the project under test that `git diff --name-only --no-renames <commit>` lists, deleted files excluded; `<commit>` is the value of the option `--since`, or `main` without it. A changed file is `ENG504` at `(1,1)` of the file when it is missing from the report, or when it has mutants, none of them counted, and at least one of them has the reason `Removed by since filter` or `Removed by mutate filter`: a wrong `since` target or a narrow `mutate` pattern would otherwise pass without a score. A changed file without any mutant, such as a file of declarations only, is no finding; nor is a file whose mutants are not counted for other reasons, such as a comment that marks them equivalent or code that does not compile.
- Without `since` — the control set — there are no changed files and no `ENG504`: such a run mutates what its configuration names, whatever the change.
- Stryker.NET 5.0.0 mostly writes a file that the `mutate` patterns leave out wholly without any mutant, so `ENG504` sees such a file only when the tool keeps some of its mutants with the reason above. This is why the main configuration sets no `mutate`.
- A change without a changed `.cs` file of the project under test has no score and no finding, exit code 0. Stryker.NET 5.0.0 still writes a report then: the mutants of the unchanged files are `Removed by since filter` or `CompileError`.
- A missing report, when `since` is enabled, counts as a report without mutants: no score, and every changed file is `ENG504`. Without `since` it is an input that does not exist, exit code 2 ([eng-checks.md](eng-checks.md#finding-and-output)). A failed step 2 fails `eng/mutate` either way.
- Threshold: `break` from the thresholds of the configuration file; the report has only `high` and `low`. A score below `break` is `ENG503` at the line of `break`.
- Level: when the score is below the threshold, `ENG501` and `ENG502` are errors; otherwise they are warnings. The mutants stay visible, and the threshold and `ENG504` decide whether the run fails.
- The message of a mutant names the mutator, the original code — the text of the source file at the mutant's place — and the replacement:

```text
src/Great.Parts.Cel/Library/IntArithmetic.cs(57,24): error ENG501: survived mutant (Arithmetic): 'a + b' -> 'a - b'
```

- The report gives absolute paths of the container, such as `/repo/src/Great.Parts.Cel/Library/IntArithmetic.cs`; they become relative to the repository root.

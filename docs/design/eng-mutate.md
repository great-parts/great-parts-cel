# eng/mutate

`eng/mutate` measures the mutation score of the changed code: Stryker.NET changes the code of the library in small ways (mutants), runs the tests and counts the mutants that the tests detect. The score of the changed files must be at least 80 %. It is the service `mutate` of `eng/compose.yaml` with the image stage `sdk`, see [eng-test.md](eng-test.md).

## Tool and settings

- Stryker.NET 5.0.0, the package `dotnet-stryker`, is a local tool in `.config/dotnet-tools.json`; `dotnet tool restore` installs it. It runs on the .NET 10 runtime of the image.
- The settings are in `stryker-config.json` in the repository root:

| Setting | Value | Why |
| :- | :- | :- |
| project | `src/Great.Parts.Cel/Great.Parts.Cel.csproj` | the code under test |
| test projects | `tests/Great.Parts.Cel.Tests`, `tests/Great.Parts.Cel.Conformance`, `tests/Great.Parts.Cel.Properties` | all tests of the library |
| target framework | `net11.0` | its code is a superset: the `decimal` code compiles only for `net11.0` |
| test runner | `mtp` | the test platform of the repository |
| coverage analysis | `perTest` | each mutant runs only the tests that cover it |
| reporters | `json`, `progress` | the report for `eng/checks mutation`; progress in the output |
| mutation level | `Standard` | |
| since | enabled, target `main` | only the files changed against `main` are mutated |
| thresholds | `high` 80, `low` 80, `break` 80 | a score below 80 % makes the exit code of the tool non-zero |

The property tests use a fixed seed ([tests.md](tests.md)), so two runs on the same code give the same score.

## Steps

1. `dotnet tool restore`.
2. `dotnet stryker --config-file <configuration> --output artifacts/mutate`. The configuration is `stryker-config.json` unless the command gets another path as its argument.
3. `eng/checks mutation --report artifacts/mutate/reports/mutation-report.json --config <configuration>`.

Before step 1 the command checks `/repo/.git`: when it is a file, the working copy is a linked worktree, and the command prints `mutate: failed: linked worktree` and returns 1 without running a step ([Linked worktrees](#linked-worktrees)).

Step 3 runs even when step 2 returns a non-zero exit code, so the surviving mutants are printed as findings. The exit code is 0 when both steps returned 0, otherwise 1. Output: the progress of the tool and the finding lines on standard output; the report in `artifacts/mutate/`.

- `since` compares the working copy with the local branch `main`, so the working copy needs that branch and the history down to the merge base.
- A mutant that cannot change behavior (an equivalent mutant) is marked in the code with a Stryker.NET comment that gives the reason; the mark is visible in the change.
- A suspicious surviving mutant is checked again with coverage analysis `off`, as the tool's documentation advises.
- A second configuration, given as the argument, mutates `eng/checks` with the tests of `eng/tests`. It checks the tool and the test runner on code whose tests are known.

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
| `CompileError`, `RuntimeError`, `Ignored`, `Pending` | not counted; `since` marks the mutants of unchanged files as ignored |

- Score: detected mutants divided by detected and not detected mutants. A report without counted mutants has no score.
- Changed files: the `.cs` files of the project under test of the configuration that `git diff --name-only --no-renames main` lists, deleted files excluded. A changed file that is missing from the report, or that has mutants but none counted, is `ENG504` at `(1,1)` of the file: otherwise a narrow `mutate` pattern or a wrong `since` target would pass without a score. A changed file without any mutant, such as a file of declarations only, is no finding.
- Known gap: a changed file whose mutants are all marked equivalent in the code has mutants but none counted, so the rule above gives a false `ENG504`, a blocking finding. The report gives such mutants the status `Ignored`, like the mutants that the `mutate` pattern or `since` leave out. Telling the two apart by the reason of the status `Ignored` — a mark in the code on one side, the `mutate` pattern and `since` on the other — is settled when `eng/checks mutation` is built.
- Threshold: `break` from the thresholds of the configuration file. A score below it is `ENG503` at the line of `break`.
- Level: when the score is below the threshold, `ENG501` and `ENG502` are errors; otherwise they are warnings. The mutants stay visible, and the threshold and `ENG504` decide whether the run fails.
- The message of a mutant names the mutator, the original code — the text of the source file at the mutant's place — and the replacement:

```text
src/Great.Parts.Cel/Library/IntArithmetic.cs(57,24): error ENG501: survived mutant (Arithmetic): 'a + b' -> 'a - b'
```

- Paths in the report become relative to the repository root.

The nightly full mutation run reads its reports with the same command.

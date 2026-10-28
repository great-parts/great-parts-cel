# Artifact checks: packages, signoff, test-count

Three commands of [eng/checks](eng-checks.md) that read results of a build, of the test runs and of the git history.

## packages

Checks the packages of the solution against the lists `eng/packages/<package id>.txt`.

1. Packs the solution: `dotnet pack Great.Parts.Cel.slnx -c Release -o artifacts/packages`. With `--no-build` it packs the Release build that already exists; `eng/test` builds in Release first.
2. Reads every `.nupkg` with `System.IO.Compression`: the list of entries and the `.nuspec` as XML. Versions are not compared.
3. Reads every list: `dependencies <target>: <id>, …`, `license <expression>`, `file <path>`. Lines that start with `#` and empty lines are skipped.

| Situation | Code | Place |
| :- | :- | :- |
| a line of a list that does not parse | `ENG200` | the line |
| a dependency of a group that the list does not name | `ENG201` | the group line |
| a group or a dependency of the list missing in the package | `ENG202` | the group line |
| a package entry that the list does not name | `ENG203` | the first line of the list; the message names the entry |
| a listed file missing in the package | `ENG204` | the `file` line |
| a package without a list | `ENG205` | the path of the `.nupkg`, `(1,1)` |
| a list without a package | `ENG206` | the first line of the list |
| no license expression in the `.nuspec`, or another expression | `ENG207` | the `license` line |

- Entries of the package format — `[Content_Types].xml`, `_rels/`, `package/` and `<id>.nuspec` — are not compared as files.
- From the `.nuspec` only the dependency groups and the license expression (`<license type="expression">`) are compared. Groups are matched by the target framework as the `.nuspec` writes it.
- A project with `IsPackable=false` produces no package, so test projects, `eng/` projects and the source generator project are never packed on their own.

```text
eng/packages/Great.Parts.Cel.Protobuf.txt(4,1): error ENG201: unexpected dependency 'System.Memory' in group net10.0
```

## signoff

Checks that every commit of a range has a `Signed-off-by` trailer.

```text
eng/checks signoff <range>
```

- `<range>` is a revision range of `git rev-list`, for example `<before>..<after>` of a push, or the range of the commits of a pull request.
- Every commit of the range counts: merges and commits of bots too. There are no exceptions.
- The trailers are read with `git interpret-trailers --parse` from the full message of the commit.
- A commit without the trailer is `ENG701`. There is no file, so the place is the commit:

```text
commit/1a2b3c4d(1,1): error ENG701: commit 1a2b3c4d 'feat: add strings extension v4' has no Signed-off-by trailer
```

The command runs `git` in the repository root. The working copy has to contain the commits of the range; a shallow checkout fetches them first.

## test-count

Compares the tests that ran under JIT with the tests that ran under Native AOT for the same commit, operating system and target. A test that the source generator of the test framework did not register does not run under Native AOT and fails nothing; this command makes it visible.

```text
eng/checks test-count --jit <report> --aot <report> --where "<runtime identifier>, <target>"
```

- The reports are xUnit XML reports of the test platform (`--report-xunit`), one per test module and run.
- A test is the pair of the `type` and `name` attributes of a `<test>` element. Tests with the result `NotRun` are not counted; every other result counts, so a failed test is still a test that ran.
- A test only in the JIT report is `ENG810`, a test only in the Native AOT report is `ENG811`. The message names the test and `--where`.
- The place is the test method: `source-file` and `source-line` of the report that has the test; the column is the first character of that line that is not white space. When that report has no source, the method is found by its name in the PDB of the JIT build of the same commit, read with `System.Reflection.Metadata`.
- Paths become relative to the repository root. A deterministic build writes the root as `/_/`; both forms are accepted.

```text
tests/Great.Parts.Cel.Tests/Values/CelValueEqualityTests.cs(42,5): error ENG810: test 'CelValueEqualityTests.NaN_NotEqualToItself' ran under JIT but not under Native AOT (linux-x64, net11.0)
```

The command runs in `eng/test` for Native AOT ([eng-test.md](eng-test.md)) and in the Native AOT cells of CI, for each pair of reports. The rules that keep the two sets equal are in [tests.md](tests.md).

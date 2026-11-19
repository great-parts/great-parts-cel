# Artifact checks: packages, package-closure, signoff, test-count

Four commands of [eng/checks](eng-checks.md) that read results of a build, of a restore, of the test runs and of the git history.

## packages

Checks the packages of the solution against the lists `eng/packages/<package id>.txt`.

1. Packs the solution: `dotnet pack Great.Parts.Cel.slnx -c Release -o artifacts/packages`. With `--no-build` it packs the Release build that already exists; `eng/test` builds in Release first.
2. Reads every `.nupkg` with `System.IO.Compression`: the list of entries and the `.nuspec` as XML. Versions are not compared.
3. Reads every list: `dependencies <target>: <id>, …`, `framework <target>: <name>, …`, `closure <target>: <id>, …`, `license <expression>`, `file <path>`. Lines that start with `#` and empty lines are skipped.

| Situation | Code | Place |
| :- | :- | :- |
| a line of a list that does not parse; a second `license` line; a second `dependencies`, `framework` or `closure` line of one target | `ENG200` | the line |
| a target of the `dependencies` lines without a `closure` line | `ENG200` | the first line of the list; the message names the target |
| a dependency of a group that the list does not name; a framework reference of a group that the list does not name | `ENG201` | the group line (`dependencies` or `framework`) |
| a dependency group of the `.nuspec` that the list does not name | `ENG201` | the first line of the list; the message names each dependency, or the empty group |
| a framework reference group of the `.nuspec` that the list does not name, with a reference in it | `ENG201` | the first line of the list; the message names each reference |
| a group or a dependency of the list missing in the package; a framework reference group or a reference of the list missing in the package | `ENG202` | the group line |
| a package entry that the list does not name | `ENG203` | the first line of the list; the message names the entry |
| a listed file missing in the package | `ENG204` | the `file` line |
| a package without a list | `ENG205` | the path of the `.nupkg`, `(1,1)` |
| a list without a package | `ENG206` | the first line of the list |
| no license expression in the `.nuspec`, or another expression | `ENG207` | the `license` line |
| a list without a `license` line | `ENG207` | the first line of the list |
| the `.nuspec` has no `description` or the SDK default `Package Description`; no `authors` or the package ID as the authors (the SDK default); no `repository` `url`; no `readme`, or the file that the `readme` names is not in the package (the entry name is compared with `/`, `\` read as `/`); no `.snupkg` next to the `.nupkg` | `ENG208` | the path of the `.nupkg`, `(1,1)`; one finding per problem |

- Entries of the package format — `[Content_Types].xml`, `_rels/`, `package/` and `<id>.nuspec` — are not compared as files.
- From the `.nuspec` only the dependency groups, the groups of `frameworkReferences` (the shared frameworks of the package, such as `Microsoft.AspNetCore.App`) and the license expression (`<license type="expression">`) are compared. A `framework` line gives the references of the group of its target; a target without a `framework` line must have no references, so a package without such lines is one that references no shared framework. The `closure` lines are not read by this command; `package-closure` reads them. Groups are matched by the target framework as the `.nuspec` writes it. Dependencies of the `.nuspec` outside any `<group>` form one group without a target; a group line of a list always names a target, so such dependencies are always an `ENG201`, and the message calls the group `without a target`.
- The metadata of a package is described in [eng-release.md](eng-release.md#package-metadata); the symbol package is read only by its presence: `<name>.snupkg` next to `<name>.nupkg`.
- A list and a package are paired by the package ID: the name of the list without `.txt` and the `<id>` of the `.nuspec`, compared without regard to case, as NuGet does.
- A repeated `file` line, or a package ID repeated within one group line, is not an error: the list describes sets, and the comparison in both directions still finds every difference.
- A list with an `ENG200` is not compared with its package: the line that did not parse would show again as differences. It still pairs with its package, so `ENG205` and `ENG206` work as for any other list.
- An empty `artifacts/packages` after a successful pack gives `ENG206` for every list; a missing `eng/packages` gives `ENG205` for every package. Both are findings with a place, not a missing input.
- A failed `dotnet pack`, or a `.nupkg` that is not a readable archive with a `.nuspec` that has an `<id>`, is an input that cannot be read: exit code 2, and the message on standard error names the command or the package; for a failed pack it also names its exit code and the first `error` line of its output ([eng-checks.md](eng-checks.md#layout)).
- A project with `IsPackable=false` produces no package, so test projects, `eng/` projects and the source generator project are never packed on their own.

```text
eng/packages/Great.Parts.Cel.Protobuf.txt(4,1): error ENG201: unexpected dependency 'System.Memory' in group net10.0
```

## package-closure

Checks what the restore of each package adds to an application against the `closure` lines of the lists `eng/packages/<package id>.txt`: the packages of the whole closure, not only the direct dependencies that `packages` compares.

```text
eng/checks package-closure
```

1. Reads every `.nupkg` of `artifacts/packages` (the packages that `packages` packed, in the same invocation or before; an empty or missing directory is exit code 2) and every list, as `packages` does. A package without a list is `ENG205` of `packages`, not of this command, and a list with a line that did not parse is not restored; its `ENG200` is reported once when both commands run in one invocation.
2. For each package with a list and for each target of its `closure` lines, writes a console application of that target framework into a new temporary directory outside the repository, so that no `Directory.Build.props` or `Directory.Packages.props` of the repository applies. Its project has one `PackageReference` to the package, at exactly the version of the `.nuspec`. When the list has a `framework` line for the target that names `Microsoft.AspNetCore.App`, the project is a `Microsoft.NET.Sdk.Web` project, as an application that uses the package would be. The `global.json` of the repository is copied next to it, so the SDK is the one of the build.
3. Runs `dotnet restore` with two sources: the directory `artifacts/packages` and nuget.org, and an empty packages folder of its own, so that a package of an earlier run with the same version is never used. The result is the `libraries` object of `obj/project.assets.json`.
4. The packages of type `package` of the `libraries`, without versions, the package itself left out, are compared with the line `closure <target>:`, ignoring case:

| Situation | Code | Place |
| :- | :- | :- |
| the restore adds a package that the line does not name | `ENG209` | the `closure <target>:` line; the message names the package and the target |
| a package of the line is not in the restore | `ENG210` | the `closure <target>:` line; the message names the package and the target |

- The restore of a project needs nuget.org, so the command runs in the cell JIT Linux `net11.0` of `ci.yml` and in the job `build` of `release.yml` ([eng-release.md](eng-release.md)), and not in the container check `eng/test`. A restore that fails (no network, a package that no source has) is an input that cannot be used: exit code 2, and the message names the package, the target and the first `error` line of the output of `dotnet restore`.
- The line is the whole closure: a package that the SDK takes out of the restore because the platform has it (the Microsoft.Extensions packages on `net11.0`, the ASP.NET Core packages of a `Microsoft.NET.Sdk.Web` project) is not in the restore and is not in the line.
- The tests, `eng/tests/Checks/PackageClosureTests.cs`, give the text of `project.assets.json` instead of running `dotnet restore`: an extra package (`ENG209`), a missing package (`ENG210`), versions, the case of the ids, the package itself and projects that are not compared, a line that does not parse (`ENG200`), and the exit code 2 for a missing package or a text that is not an assets file.

```text
eng/packages/Great.Parts.Cel.DependencyInjection.txt(9,1): error ENG209: the restore of Great.Parts.Cel.DependencyInjection in a net10.0 application adds package 'Microsoft.Extensions.Logging.Abstractions', which is not in the closure list
```

## signoff

Checks that every commit of a range has a `Signed-off-by` trailer.

```text
eng/checks signoff <range>
```

- `<range>` is a revision range of `git rev-list`, for example `<before>..<after>` of a push, or the range of the commits of a pull request. It is the argument of the command, see [Invocation](eng-checks.md#invocation). A range that `git rev-list` does not accept is exit code 2.
- Every commit of the range counts: merges and commits of bots too. There are no exceptions.
- The trailers are read with `git interpret-trailers --parse --no-divider` from the full message of the commit, so a line `---` in the body does not end the message.
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

- The reports are xUnit.net v2+ XML reports, one per test module and run, written by the xUnit.net runner inside the test module with its option `-result-xml <file>`: the JIT build runs as `dotnet <module>.dll`, the published executable runs directly. `dotnet test` and its options are not used for these runs.
- A test is the pair of the `type` and `name` attributes of a `<test>` element. Tests with the result `NotRun` are not counted; every other result counts, so a failed test is still a test that ran.
- A test only in the JIT report is `ENG810`, a test only in the Native AOT report is `ENG811`. The message names the test by its `name` attribute, which is the full name with the namespace and, for a theory case, the arguments, and `--where`.
- The place is the test method: `source-file` and `source-line` of the report that has the test; the column is the first character of that line that is not white space. The test framework records these at build time, under JIT and under Native AOT alike; `source-line` is the line of the `[Fact]` or `[Theory]` attribute, not of the method. When that report has no source, the method is found by its name in the PDB of the JIT build of the same commit, read with `System.Reflection.Metadata`: the place is the first visible sequence point of the method — of its state machine for an async method — which in the Release build is the first statement of the body, so it can lie a few lines below the line that the report would give. The JIT build is the file in the `name` attribute of the `<assembly>` element of the JIT report, and its PDB is the file next to it with the extension `.pdb`. The attribute is the absolute path of the built `.dll` as the run saw it, and the command reads it as it is, so it runs where the JIT run ran: in the same container in `eng/test` for Native AOT. In the Native AOT report that attribute is the published executable and is not read.
- A report that cannot be read, or a needed PDB that is missing or cannot be read, is an unread input: exit code 2, and the message names the file ([eng-checks.md](eng-checks.md#layout)). When the method is not in the PDB either, the place is the line of the `<test>` element in the report that has the test, column 1; the finding stays a finding and is not an input error.
- When the JIT report has several `<assembly>` elements and the PDB is needed, the PDBs of all of them are read; for a method found in more than one, the first assembly in report order wins, and any assembly or PDB that cannot be read is an unread input.
- When the place has a source file but that file, or that line in it, cannot be read, the column is 1. The source file is not an input of the command, so this is not an error.
- Paths become relative to the repository root. A deterministic build writes the root as `/_/`; both forms are accepted. A path outside the root that does not start with `/_/` is written as it is, with `/` as the separator.
- A finding whose place is a `<test>` element names the report file by the same rule: relative to the root, or as it is when the report lies outside the root.

```text
tests/Great.Parts.Cel.Tests/Values/CelValueEqualityTests.cs(42,5): error ENG810: test 'Great.Parts.Cel.Tests.Values.CelValueEqualityTests.NaN_NotEqualToItself' ran under JIT but not under Native AOT (linux-x64, net11.0)
```

The command runs in `eng/test` for Native AOT ([eng-test.md](eng-test.md)) and in the Native AOT cells of CI, for each pair of reports. The rules that keep the two sets equal are in [tests.md](tests.md).

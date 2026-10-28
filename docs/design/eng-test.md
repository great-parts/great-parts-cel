# eng/test and the containers of eng/

`eng/test` is the blocking check of a change: it builds the solution, runs every test under JIT and runs the source and package checks. It runs in a Linux container with the .NET 11 SDK and the .NET 10 runtime, without Go. The same container setup serves `eng/docs` ([eng-docs.md](eng-docs.md)) and `eng/mutate` ([eng-mutate.md](eng-mutate.md)); other components add their services to the same Compose file and their commands to the same project.

## Commands

Each command is a service of the Compose file `eng/compose.yaml` and runs with one command from the repository root:

```text
docker compose -f eng/compose.yaml run --rm test
docker compose -f eng/compose.yaml run --rm test-aot
docker compose -f eng/compose.yaml run --rm docs
docker compose -f eng/compose.yaml run --rm mutate
docker compose -f eng/compose.yaml run --rm min-sdk
```

| Service | Name in the documentation | Image stage | What it does |
| :- | :- | :- | :- |
| `test` | `eng/test` | `sdk` | build, all tests under JIT, source and package checks |
| `test-aot` | `eng/test` for Native AOT | `sdk-aot` | tests under JIT and Native AOT on linux-x64, test count |
| `docs` | `eng/docs` | `docs` | examples, error codes page, offline links |
| `mutate` | `eng/mutate` | `sdk` | mutation testing of the changed code |
| `min-sdk` | `min-sdk` | `sdk-min` | builds a sample application on the .NET SDK 10.0.100 from the packed `Great.Parts.Cel`, see [min-sdk](#min-sdk) |

Every service:

- mounts the working copy at `/repo` and works there, so build outputs and reports land in the working copy;
- keeps NuGet packages in the named volume `nuget` at `/root/.nuget/packages`, so restore is fast after the first run;
- takes no environment variables from the host, so a run does not depend on the starting shell; Compose reads the variable of the second Compose file ([Linked worktrees](#linked-worktrees)) on the host, not in the container;
- has the entry point `dotnet run --project eng/run -- <service>`; arguments after the service name of `docker compose run` are passed to that command.

The service `oracle` of the differential run is the exception: it runs the image `golang` without .NET, with its own entry point and volumes, and the service `difftest` depends on it ([eng-oracle.md](eng-oracle.md#container)).

The service `min-sdk` is the second exception: `eng/run` builds with the SDK of the root `global.json`, which its image does not have. Its entry point is a shell command written in the service, its working directory is `/repo/eng/min-sdk`, and it does not use the volume `nuget` ([min-sdk](#min-sdk)).

## Image

`eng/docker/Dockerfile` has four stages that services run, and one helper stage with the .NET 10 SDK image from which `sdk` copies the runtime. The build context is `eng/docker/`, which holds only this file: nothing of the working copy is copied into the image.

| Stage | Base image | Added |
| :- | :- | :- |
| `sdk` | `mcr.microsoft.com/dotnet/sdk` with the SDK version of `global.json` | the .NET 10 runtime, copied from the `mcr.microsoft.com/dotnet/sdk` 10.0 image; `/repo` and `/git` registered as safe directories of `git` |
| `sdk-aot` | the `-aot` variant of the same SDK version: it adds the native toolchain that Native AOT needs | the same as `sdk` |
| `docs` | the `sdk` stage | the `lychee` program, copied from `lycheeverse/lychee` 0.24.2 |
| `sdk-min` | `mcr.microsoft.com/dotnet/sdk:10.0.100`, the first .NET 10 SDK | nothing |

- Tags are exact versions; floating tags such as `latest` are not used. The SDK tag of `sdk` changes together with `global.json`, the tag of `sdk-min` together with `eng/min-sdk/global.json`.
- The .NET 10 runtime runs the tests and examples for `net10.0` and the mutation tool.
- `git` comes with the SDK image; the safe directory entries avoid its ownership error for files of another host user.

## Linked worktrees

In every service that runs `eng/run`, `/repo` must be a git repository with the local `main` and the history down to the merge base, and the host repository must stay unchanged. Two readers need it: `git` for the new places of the ratchet ([eng-checks-source.md](eng-checks-source.md)) and the SourceLink lookup of every build, which warns without it. `eng/mutate` does not run in a linked worktree ([eng-mutate.md](eng-mutate.md#linked-worktrees)).

In a linked worktree (`git worktree add`) `.git` is a file whose line `gitdir: <path>` holds an absolute host path, which does not exist in the container. A run from a linked worktree adds the second Compose file `eng/compose.worktree.yaml` with its variable set for one command:

```text
ENG_GIT_COMMON_DIR="$(git rev-parse --path-format=absolute --git-common-dir)" \
  docker compose -f eng/compose.yaml -f eng/compose.worktree.yaml run --rm test
```

It adds two read-only bind mounts to every service that runs `eng/run` — not to `oracle`, which does not read git ([eng-oracle.md](eng-oracle.md#container)), and not to `min-sdk`, which builds without SourceLink and reads no git: the common git directory `${ENG_GIT_COMMON_DIR}` at `/git`, and the worktree's file `artifacts/worktree/gitdir` over `/repo/.git`. A mount over a file leaves the host file unchanged. Both have `create_host_path: false`: a missing source stops the run instead of creating a host directory.

The file holds the line `gitdir: /git/worktrees/<name>`, with `<name>` from the host's `.git` file. Whoever creates the worktree writes it once, right after `git worktree add`; `artifacts/` is ignored by git, and the file goes away with the worktree. In Git Bash, in the worktree root:

```text
mkdir -p artifacts/worktree
echo "gitdir: /git/worktrees/$(basename "$(git rev-parse --git-dir)")" > artifacts/worktree/gitdir
```

A config with inline `content` is not used: Compose would copy it into the writable `/repo` and replace the host file.

The file `commondir` of the worktree directory holds `../..`, so the readers find `/git`. The name `GIT_COMMON_DIR` is avoided: git itself reads it. Relative worktree paths (`--relative-paths`) are not used.

## eng/run

`eng/run/` is a console project (`net10.0`, no packages, `IsPackable=false`, part of the solution) with the commands `test`, `test-aot`, `docs`, `mutate` and `difftest` ([eng-difftest.md](eng-difftest.md)). It contains no check logic: it runs steps as child processes in the repository root and passes their output through unchanged.

- Before a step it writes a header line `== <step>`; at the end — `<command>: passed` or `<command>: failed: <steps or a reason>`. They do not match the finding pattern.
- Each command, and the launcher of the oracle, first deletes the directories under `artifacts/` that it writes, so every report there comes from the current run.
- The exit code is 0 when every step returned 0, otherwise 1. After a successful build, `difftest` returns the exit code of `eng/difftest` unchanged: 0, 1 or 2 ([eng-difftest.md](eng-difftest.md#findings-and-output)).

## test

1. Write the `CsCheck_*` environment variables, if any are set: they change every property test of the run, see [tests.md](tests.md). In the container none are expected.
2. `dotnet build Great.Parts.Cel.slnx -c Release`. A failed build stops the command; the compiler lines name the places.
3. `dotnet test --solution Great.Parts.Cel.slnx -c Release --no-build`. The test platform is the one that `global.json` selects. It runs every test project of the solution under JIT, for each target of the project: unit tests, property tests, the suite runner with the cel-spec suite and the regression corpus, and the tests of `eng/`.
4. `dotnet run --project eng/checks -c Release --no-build -- suppressions duplicates ratchet packages --no-build`.

Steps 3 and 4 both run even when one fails, so a single run shows all failures.

## test-aot

For each target `net10.0` and `net11.0` and each of the projects `Great.Parts.Cel.Tests` and `Great.Parts.Cel.Conformance`:

1. Build the project for the target, `dotnet build <project> -f <target> -c Release`, and run the built module under JIT: `dotnet <project directory>/bin/Release/<target>/<project>.dll -result-xml artifacts/test-results/jit/<project>/<target>/report.xml`.
2. Publish for linux-x64 under Native AOT with the restore of step 1: `dotnet publish <project> -f <target> -r linux-x64 -c Release --no-restore -p:InvariantGlobalization=true -o artifacts/aot/<project>/<target>/`. Trimming and AOT warnings of the publish fail it, as every warning does, and name the file and the line without a column, with the absolute path as the build saw it: `<path>(<line>): Trim analysis error IL2xxx: …` or `… AOT analysis error IL3xxx: …`. The file and the line come from the PDB of the library next to its assembly; without it the line starts with `ILC :`. A restore with `-r` would add targets for the runtime identifier to the lock files of the referenced projects, and a locked restore in CI would then fail; see [tests.md](tests.md).
3. Run the published executable `artifacts/aot/<project>/<target>/<project>` with `-result-xml artifacts/test-results/aot/<project>/<target>/report.xml`.
4. `eng/checks test-count` on the two reports, with `linux-x64` and the target as the label.

All four steps run for every pair, so one run shows every failing pair. The command takes minutes and is not part of `test`; CI runs the same publish and comparison in its Native AOT cells on linux-x64 and win-x64. Why `InvariantGlobalization` is set only on the publish command line is explained in [tests.md](tests.md).

## min-sdk

Shows that the source generator in the package `Great.Parts.Cel`, built against Roslyn 5.0.0, loads into the compiler of the .NET SDK 10.0.100 — the oldest SDK a user of the `net10.0` package can have — without `CS9057`, the warning with which a compiler skips an analyzer built against a newer compiler.

`eng/min-sdk/` is a small application that inherits nothing from the repository root. It is not part of `Great.Parts.Cel.slnx`: the solution builds on the SDK of the root `global.json`.

| File | Contents |
| :- | :- |
| `global.json` | SDK `10.0.100`, `rollForward: disable` |
| `Directory.Build.props`, `Directory.Packages.props` | empty `<Project />`: the props of the root, central package versions and the architecture analyzer do not apply |
| `.editorconfig` | `root = true` and nothing else: the rules of the root `.editorconfig` do not apply |
| `NuGet.config` | `<clear />` and the one package source `../../artifacts/packages`; the packages folder `../../artifacts/min-sdk/packages` |
| `MinSdk.csproj` | `net10.0`, `TreatWarningsAsErrors`, `EnableSourceLink=false`, a package reference to `Great.Parts.Cel` `0.0.0-dev`; no lock file |
| `Program.cs` | one statement that uses a public type of the package |

Every local build packs version `0.0.0-dev`, and NuGet does not look at the source again for a version that its packages folder already has. So the application does not use the volume `nuget`: it has its own packages folder under `artifacts/min-sdk/`, and the command deletes that directory first. Every run then takes the package that the last `test` packed.

The entry point runs, in `/repo/eng/min-sdk`:

1. Stop with `min-sdk: failed: no package in artifacts/packages; run the service test first` when `../../artifacts/packages` has no `Great.Parts.Cel.*.nupkg`.
2. Delete `../../artifacts/min-sdk/`.
3. `dotnet --version`; any version other than `10.0.100` fails the run.
4. `dotnet build -c Release --artifacts-path ../../artifacts/min-sdk/build -p:ReportAnalyzer=true -flp:"LogFile=../../artifacts/min-sdk/build.log;Verbosity=diagnostic"`. The console keeps the default verbosity, so the compiler lines appear in the output, and a copy of it goes to `../../artifacts/min-sdk/console.log`; the build passes when its exit code is 0 and the copy has the lines `0 Warning(s)` and `0 Error(s)`. The log file gets the analyzer report.
5. Find the evidence that the generator loaded and print the lines found: the `project.assets.json` of the restore names `analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll` as an analyzer of the package, and the generator part of the analyzer report in `build.log` names `Great.Parts.Cel.Generator.CelTypeContextGenerator`.

The run passes when every step succeeds: SDK 10.0.100, a build with no warning and no error, both pieces of evidence found. `CS9057` and `CS8032` (an analyzer that cannot be created) are warnings, so `TreatWarningsAsErrors` makes either of them a failed build. The last line is `min-sdk: passed` or `min-sdk: failed: <step>`, and the exit code is 0 or 1. The generator has no pipeline yet, so the run shows that it loads, not that it produces code.

Order of a run, from the repository root; a linked worktree needs the second Compose file only for `test`:

```text
docker compose -f eng/compose.yaml run --rm test
docker compose -f eng/compose.yaml run --rm min-sdk
```

The service is not part of `test` and CI does not run it. It is run when the generator first ships, when the generator gets its pipeline, and when the Roslyn version of the generator or the oldest supported SDK changes.

## Output and exit code

- The exit code of `docker compose run` is the exit code of the command: 0 or 1, and also 2 for `difftest`.
- Standard output and standard error of the container carry everything the steps print: compiler lines, failed tests with a file and a line, finding lines of the checks.
- Files go to `artifacts/` in the working copy, which git ignores:

| Directory | Written by |
| :- | :- |
| `artifacts/test-results/` | test runs with a report |
| `artifacts/aot/` | Native AOT publish |
| `artifacts/packages/` | `eng/checks packages` |
| `artifacts/docs/` | `eng/docs` |
| `artifacts/mutate/` | `eng/mutate` |
| `artifacts/oracle/` | the launcher of the oracle container ([eng-oracle.md](eng-oracle.md#launcher)) |
| `artifacts/min-sdk/` | the service `min-sdk`: its packages folder, build output and build log ([min-sdk](#min-sdk)) |
| `artifacts/worktree/` | the host, once per linked worktree; no command deletes it |

The checks of `eng/checks` skip `artifacts/`, like `bin/` and `obj/`.

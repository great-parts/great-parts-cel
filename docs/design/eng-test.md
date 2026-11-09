# eng/test and the containers of eng/

`eng/test` is the one full check of a change: it builds the solution, runs every test under JIT, runs the source and package checks, and runs the tests of the two projects that are published under Native AOT on linux-x64, comparing their test count with the JIT run. There is no separate command for Native AOT. It runs in a Linux container with the .NET 11 SDK, the .NET 10 runtime and the native toolchain, without Go; it is run once, on the final `head` of a task. The same container setup serves `eng/docs` ([eng-docs.md](eng-docs.md)) and `eng/mutate` ([eng-mutate.md](eng-mutate.md)); other components add their services to the same Compose file and their commands to the same project.

## Commands

Each command is a service of the Compose file `eng/compose.yaml` and runs with one command from the repository root:

```text
docker compose -f eng/compose.yaml run --rm test
docker compose -f eng/compose.yaml run --rm docs
docker compose -f eng/compose.yaml run --rm mutate [<configuration>] [--since <commit>]
docker compose -f eng/compose.yaml run --rm min-sdk
```

| Service | Name in the documentation | Image stage | What it does |
| :- | :- | :- | :- |
| `test` | `eng/test` | `sdk-aot` | build, all tests under JIT, source and package checks, tests under Native AOT on linux-x64, test count |
| `docs` | `eng/docs` | `docs` | examples, error codes page, offline links |
| `mutate` | `eng/mutate` | `sdk` | mutation testing of the changed code |
| `min-sdk` | `min-sdk` | `sdk-min` | builds a sample application on the .NET SDK 10.0.100 from the packed `Great.Parts.Cel`, see [min-sdk](#min-sdk) |

Every service:

- mounts the working copy at `/repo` and works there, so build outputs and reports land in the working copy;
- keeps NuGet packages in the named volume `nuget` at `/root/.nuget/packages`, so restore is fast after the first run;
- takes no environment variables from the host, so a run does not depend on the starting shell; Compose reads the variable of the second Compose file ([Linked worktrees](#linked-worktrees)) on the host, not in the container;
- has the entry point `dotnet run --project eng/run -- <service>`; arguments after the service name of `docker compose run` are passed to that command.

The service `oracle` of the differential run is the exception: it runs the image `golang` without .NET, with its own entry point and volumes, and the service `difftest` depends on it ([eng-oracle.md](eng-oracle.md#container)).

The service `min-sdk` is the second exception: building `eng/run` needs the SDK of the root `global.json`, which its image does not have. Its entry point runs the `eng/run` that the service `test` compiled, `dotnet eng/run/bin/Release/net10.0/Great.Parts.Cel.Eng.Run.dll min-sdk`, and it does not use the volume `nuget` ([min-sdk](#min-sdk)).

## Image

`eng/docker/Dockerfile` has four stages that services run, and one helper stage with the .NET 10 SDK image from which `sdk` copies the runtime. The build context is `eng/docker/`, which holds only this file: nothing of the working copy is copied into the image.

| Stage | Base image | Added |
| :- | :- | :- |
| `sdk` | `mcr.microsoft.com/dotnet/sdk` with the SDK version of `global.json` | the .NET 10 runtime, copied from the `mcr.microsoft.com/dotnet/sdk` 10.0 image; `/repo` and `/git` registered as safe directories of `git` |
| `sdk-aot` | the `-aot` variant of the same SDK version: it adds the native toolchain that Native AOT needs; the service `test` runs on it | the same as `sdk` |
| `docs` | the `sdk` stage | the `lychee` program, copied from `lycheeverse/lychee` 0.24.2 |
| `sdk-min` | `mcr.microsoft.com/dotnet/sdk:10.0.100`, the first .NET 10 SDK | `/repo` and `/git` registered as safe directories of `git` |

- Tags are exact versions; floating tags such as `latest` are not used. The SDK tag of `sdk` changes together with `global.json`, the tag of `sdk-min` together with `eng/min-sdk/global.json`.
- The .NET 10 runtime runs the tests and examples for `net10.0` and the mutation tool.
- `git` comes with the SDK image; the safe directory entries avoid its ownership error for files of another host user.

## Linked worktrees

In every service that runs `eng/run`, `/repo` must be a git repository with the local `main` and the history down to the merge base, and the host repository must stay unchanged. Three readers need it: `git` for the new places of the ratchet ([eng-checks-source.md](eng-checks-source.md)), the SourceLink lookup of every build, which warns without it, and `eng/run` for the commit and the state of the tree in its last line. `eng/mutate` runs in a linked worktree only without `since`, such as the control set ([eng-mutate.md](eng-mutate.md#linked-worktrees)).

In a linked worktree (`git worktree add`) `.git` is a file whose line `gitdir: <path>` holds an absolute host path, which does not exist in the container. A run from a linked worktree adds the second Compose file `eng/compose.worktree.yaml` with its variable set for one command:

```text
ENG_GIT_COMMON_DIR="$(git rev-parse --path-format=absolute --git-common-dir)" \
  docker compose -f eng/compose.yaml -f eng/compose.worktree.yaml run --rm test
```

It adds two read-only bind mounts to every service that runs `eng/run` — `test`, `mutate`, `docs`, `difftest` and `min-sdk`, not `oracle`, which does not read git ([eng-oracle.md](eng-oracle.md#container)): the common git directory `${ENG_GIT_COMMON_DIR}` at `/git`, and the worktree's file `artifacts/worktree/gitdir` over `/repo/.git`. A mount over a file leaves the host file unchanged. Both have `create_host_path: false`: a missing source stops the run instead of creating a host directory.

The file holds the line `gitdir: /git/worktrees/<name>`, with `<name>` from the host's `.git` file. Whoever creates the worktree writes it once, right after `git worktree add`; `artifacts/` is ignored by git, and the file goes away with the worktree. In Git Bash, in the worktree root:

```text
mkdir -p artifacts/worktree
echo "gitdir: /git/worktrees/$(basename "$(git rev-parse --git-dir)")" > artifacts/worktree/gitdir
```

A config with inline `content` is not used: Compose would copy it into the writable `/repo` and replace the host file.

The file `commondir` of the worktree directory holds `../..`, so the readers find `/git`. The name `GIT_COMMON_DIR` is avoided: git itself reads it. Relative worktree paths (`--relative-paths`) are not used.

## eng/run

`eng/run/` is a console project (`net10.0`, no packages, `IsPackable=false`, part of the solution) with the commands `test`, `docs`, `mutate`, `difftest` ([eng-difftest.md](eng-difftest.md)) and `min-sdk`. It contains no check logic: it runs steps as child processes in the repository root and passes their output through unchanged. The one exception is the steps of `min-sdk`, which are C# in this process because they read the output of the build ([min-sdk](#min-sdk)).

A command is a list of stages. A stage is lanes that run at the same time, and a lane is steps that run one after another; a stage ends when all its lanes have ended. Only `test` has a stage of several lanes.

- Every step has a time limit. A step that is still running when its limit is over is killed with its whole process tree and fails; see [Time limits](#time-limits).
- Before a step it writes a header line `== <step>`. The output of a stage of one lane passes through as the steps write it. The output of every lane of a stage of several lanes is written as a whole when the lane ends, so lines of different processes are not mixed.
- A step stops the rest of its lane when it fails and is marked so, because the next steps need its result; a stage stops the command when it has a failed step and is marked so. A step that is not marked does not stop the others, so one run shows all failures.
- Each command, and the launcher of the oracle, first deletes the directories under `artifacts/` that it writes, so every report there comes from the current run. The lanes of the Native AOT publish also delete what an unfinished publish left under `obj/` ([Unfinished Native AOT outputs](#unfinished-native-aot-outputs)).
- Before the first stage and before each later one the command waits until the host is quiet and writes how long it waited ([Waiting for the host](#waiting-for-the-host)). The wait is not part of the time limit of any step.
- The last line of every command is its summary, in this form: `<command>: passed` or `<command>: failed: <steps or a reason>`, where a step that ran out of time is named `timeout after <N> s in step <step>`; `head <40 characters of the SHA of HEAD>`; `tree clean`, `tree dirty` or `tree unknown`; the duration in seconds, without the wait; `waited <N> s`, the time of the wait; `exit <code>`; for `test` also the counters. The lines do not match the finding pattern. `tree` is `clean` when `git status --porcelain` listed nothing before the first step and after the last one, `unknown` when git cannot answer. A reviewer does not repeat a run whose summary names the same SHA of `HEAD` and a clean tree.
- The exit code is 0 when every step returned 0, otherwise 1, a step that ran out of time included. One step is marked so that its exit code is not used (`Step.IgnoresExitCode`): the lychee step of `docs`, whose report is the result ([eng-docs.md](eng-docs.md#steps)). A program that does not start or does not end within its limit still fails such a step. After a successful build, `difftest` returns the exit code of `eng/difftest` unchanged: 0, 1 or 2 ([eng-difftest.md](eng-difftest.md#findings-and-output)).
- `eng/checks` is built once, with the solution or by the first step of `mutate`; every later call of it is `dotnet run --project eng/checks -c Release --no-build`.

## Time limits

`eng/run` limits the time of every step, so a hung process does not hold the run for ever. A step that is still running when its limit is over is killed together with the processes it started, and fails with the reason `timeout after <N> s in step <step>`; `<N>` is its limit in seconds. The command ends with exit code 1, also `difftest`, which otherwise returns the exit code of `eng/difftest`. The last line is, for example, `test: failed: timeout after 67 s in step dotnet build Great.Parts.Cel.slnx -c Release; head …; tree clean; 70.3 s; exit 1`. Whether a failed step stops the rest is decided as for any failed step ([eng/run](#engrun)).

A limit is the time of a cold run in a new working copy, plus 20 %. The runs of `dotnet build`, of the JIT module and of the publish were measured again on 2026-11-02 and took longer than on 2026-10-30; their limits come from the second measurement, the lowest cold run of it (67.1 s, 77.3 s and 166.7 s) plus 20 %, rounded up. The limits measured on 2026-11-03 come from the longest run of the step on that day plus 20 %, rounded up to a second, because the runs of one step on that day differed widely, and a limit from the lowest run would leave little above the others: `examples` and lychee in `docs` from 24.1 s and 1.4 s (a cold run of `docs` took 24.1 s and 0.9 s); the preparation of `mutate` from 200.1 s, a run of the control set with the Debug build done (the cold run took 179.2 s, and a limit of 180 s was not enough once). The limits of the build and of the JIT module were measured again on 2026-11-04, after the code of the third increment came: the build, cold, with no `bin` and `obj`, took 78.3 s to 85 s in four runs, and the module `Great.Parts.Cel.Eng.Tests` next to the four publishes took 89.3 s in one run and more than 93 s in two others, so the limits are 102 s (85 s) and 112 s (taken as 93.3 s) with 20 % added, rounded up to a second. The limit of lychee was 2 s and failed a run of `docs` on that day; three runs took 1.6 s, 2.1 s and 2.1 s, so the limit is 3 s (2.1 s with 20 % added, rounded up). The limits of the build, of the JIT module, of the publish and of the examples of `docs` were measured again on 2026-11-09, after the code of the fourth increment came, in one cold run of `test` and one of `docs` (no `bin`, `obj` and `artifacts`, one run on the host; the waits before the three stages of `test` saw no load of other processes, then up to 1.5 and up to 0.9 processors, and those of `docs` up to 1.0 and 2.1 processors, then none): the build of the solution took 106.6 s, the module `Great.Parts.Cel.Eng.Tests` 92.1 s, the longest Native AOT publish, that of `Great.Parts.Cel.Tests` for `net10.0`, 233.0 s (that for `net11.0` 214.6 s, those of `Great.Parts.Cel.Conformance` 136.7 s and 150.7 s), and `examples` in `docs` 34.7 s. A second cold run of `test` on the same day gave 86.7 s, 90.5 s and 221.4 s for the first three, below the first. One cold run and 30 % added by decision of the stakeholder of 2026-11-09 (instead of the longest of three runs and 20 %), so the limits are 139 s, 120 s, 303 s and 46 s, rounded up to a second; none is lower than before. The other limits come from the measurement of 2026-10-30, 2026-11-03 and 2026-11-04. The limits are constants of `eng/run/Timeouts.cs`; when runs fail by timeout regularly, a limit is raised there.

| Step | Limit | Cold run |
| :- | :- | :- |
| `dotnet build`: the solution in `test`, `eng/checks` in `mutate` and in `docs`, `eng/difftest` in `difftest`, `src/Great.Parts.Cel` in `docs` | 139 s | 56 s for the solution on 2026-10-30; at least 67.1 s on 2026-11-02; 78 s to 85 s on 2026-11-04; 106.6 s on 2026-11-09 |
| `test`: one test module under JIT; the checks of `eng/checks` of the same lane | 120 s each | about 60 s for the lane on 2026-10-30; at least 77.3 s on 2026-11-02; the module `Great.Parts.Cel.Eng.Tests` 89 s to more than 93 s on 2026-11-04, 92.1 s on 2026-11-09 |
| `test`: the Native AOT publish of one project and target | 303 s | about 100 s on 2026-10-30; at least 166.7 s on 2026-11-02; 233.0 s for `Great.Parts.Cel.Tests`, `net10.0`, on 2026-11-09 |
| `test`: the run of one published executable | 120 s | about 100 s |
| `test-count` of `test`; the reading of the report by `mutate` | 12 s | about 10 s |
| `min-sdk`: each child process — `dotnet --version`, the build of the sample application | 20 s | 16 s for the command |
| `difftest`: the run of `eng/difftest` | its budget × 1.2 + 36 s, see [eng-difftest.md](eng-difftest.md#invocation) | |
| `docs`: `eng/checks examples` | 46 s | 24.1 s on 2026-11-03; 34.7 s on 2026-11-09 |
| `docs`: lychee, offline | 3 s | 0.9 s on 2026-11-03; up to 1.4 s in later runs; 1.6 s to 2.1 s on 2026-11-04 |
| `docs`: `eng/checks error-codes` and `eng/checks links` | 12 s | |
| `mutate`: the restore of the tool; the run of Stryker.NET until it prints the number of mutants | 241 s | 118 s on 2026-10-30; for the control set on 2026-11-03 179.2 s, up to 200.1 s in later runs |
| `mutate`: the run of Stryker.NET after that line | 0.3 s × the number of mutants + 30 s, see [eng-mutate.md](eng-mutate.md#time-limits) | 0.25 s per mutant |

The build of `eng/checks` and of `eng/difftest`, the reading of the mutation report and the restore of the tool were not measured on their own: each has the limit of the step of the same kind in the table.

## Waiting for the host

The limits above are those of a run on a quiet machine. When the build of another working copy runs in a container of the same host, the steps share the processors with it and fail by timeout without a fault of the change (`timeout after 102 s`, `112 s`, `201 s` in the runs of two tasks that shared a host with another build). So `eng/run` waits for the host before it runs the steps, and the wait is not part of any limit.

- When: before the first stage of every command (`test`, `docs`, `mutate`, `difftest`, `min-sdk`) and before each later stage, at the points where no step of this command is running. The host must be quiet for 60 s without a break before the first stage, and for 10 s before each later one, because the build of the command itself has just ended. A busy reading restarts the time.
- What is seen: the container does not get the Docker socket, so it cannot list the other containers. It reads `/proc/stat`, whose counters are those of the host kernel: every container of the host, and a build on the host itself, show in it. Once a second the wait takes the processor time that was not idle, as a number of processors (the share of busy time times the number of processors). A reading of more than 0.5 processors is busy; an idle host with the usual services of the harness showed 0.1 to 0.25. The other containers cannot be named: the line says how many processors they used, not who used them.
- What it writes: the header `== wait: the host quiet for <N> s`; while the host is busy, every 30 s `wait: other processes use <X> processors; waited <N> s`; at the end `wait: the host is quiet; waited <N> s; other processes used up to <X> processors` (or `no load of other processes seen`). The summary line has `waited <N> s`, and the duration in it does not include the wait.
- The end: when the host is quiet, as above. After 60 minutes the wait gives up, writes `wait: gave up; the run goes on and may be slowed by the load` and the command runs: a run that was slowed by the load shows it by this line and by its timeouts. When `/proc/stat` cannot be read (a platform without it), the wait writes `wait: the load of the host cannot be read; not waiting` and does not wait.
- Where: `eng/run/QuietWait.cs` (the wait, with its constants) and `eng/run/HostLoad.cs` (the reading); the tests are in `eng/tests/EngRun/QuietWaitTests.cs` and `HostLoadTests.cs`, with a fake host and a fake clock.

The wait covers the other builds of the host, not the runs of the same Compose project: those are started one after another by whoever runs them. A build that starts after the wait has ended still slows the run; the wait lowers the chance, it does not lock the host.

## Unfinished Native AOT outputs

A Native AOT publish that was killed in the middle, by its limit, by a user or by the end of the container, can leave a half-written object file under `<project>/obj/Release/<target>/linux-x64/native/` and a half-linked executable in `artifacts/aot/`. The next publish takes the object file as up to date, links it and produces a program that crashes (exit code 139) or cannot be started (`Permission denied`). `eng/run` does not take the result of an unfinished publish for a current one:

- The lane of every published project and target starts with a step in this process, `aot: delete artifacts/aot/<project>/<target> <project directory>/obj/Release/<target>/linux-x64/native`. It deletes both directories, so the publish always runs the native compiler in full and nothing of an earlier publish reaches the run of the executable. `artifacts/aot/` is also deleted at the start of the command ([eng/run](#engrun)); the step covers the lane when it is run alone and the directory under `obj/`, which the start does not delete.
- The compiled assemblies of `obj/`, the restore and the outputs of the build are kept: only the output of the native compiler is not trusted. The limit of the publish is the limit of a cold run ([Time limits](#time-limits)), so the full run of the native compiler stays inside it.
- A directory that cannot be deleted fails the lane with the reason of the deletion, and the publish and the run of the executable are skipped, as after a failed publish.
- Code: `eng/run/StaleOutputs.cs`; the lane is built in `Commands.AotLanes`. The tests are in `eng/tests/EngRun/StaleOutputsTests.cs`.

## test

1. Stage 1, the build: `dotnet build Great.Parts.Cel.slnx -c Release`, once, for every project of the solution and both targets, `eng/checks` included. A failed build stops the command; the compiler lines name the places.
2. Stage 2, at the same time:
   - The lane of the JIT run and the checks. Every test project of the solution, for each target of the project, runs under JIT once: `dotnet <project directory>/bin/Release/<target>/<project>.dll -noColor -result-xml artifacts/test-results/jit/<project>/<target>/report.xml`. These are the unit tests, the property tests, the suite runner with the cel-spec suite and the regression corpus, and the tests of `eng/`. The report is also the reference of the test count below; there is no second JIT run. The test projects are found in the solution: a project with a package reference to `xunit.v3.*`; the projects that set `PublishAot` are the ones that stage 2 also publishes. Then `dotnet run --project eng/checks -c Release --no-build -- suppressions duplicates ratchet packages --no-build`.
   - For every target of every published project, one lane: first the deletion of the unfinished outputs of an earlier publish, a step in this process ([Unfinished Native AOT outputs](#unfinished-native-aot-outputs)); then the publish for linux-x64 under Native AOT, `dotnet publish <project> -f <target> -r linux-x64 -c Release --no-restore -p:InvariantGlobalization=true -p:BuildProjectReferences=false -o artifacts/aot/<project>/<target>/`, then the run of the executable `artifacts/aot/<project>/<target>/<project> -noColor -result-xml artifacts/test-results/aot/<project>/<target>/report.xml`. The publishes run at the same time with each other, with the JIT run and with the checks. A failed publish skips the run of its executable.
3. Stage 3, the comparison: for every published project and target, `dotnet run --project eng/checks -c Release --no-build -- test-count --jit <JIT report> --aot <AOT report> --where "linux-x64, <target>"`.

No step stops the others, so one run shows all failures; only a failed build stops the command.

The publish has no restore of its own: it reuses the restore of the build with `--no-restore`, and it does not build the referenced projects again: the build of stage 1 made them for both targets (`BuildProjectReferences=false`). Trimming and AOT warnings of the publish fail it, as every warning does, and name the file and the line without a column, with the absolute path as the build saw it: `<path>(<line>): Trim analysis error IL2xxx: …` or `… AOT analysis error IL3xxx: …`. The file and the line come from the PDB of the library next to its assembly; without it the line starts with `ILC :`. A restore with `-r` would add targets for the runtime identifier to the lock files of the referenced projects, and a locked restore in CI would then fail; see [tests.md](tests.md). Why `InvariantGlobalization` is set only on the publish command line is explained there too. CI runs the same publish and comparison in its Native AOT cells on linux-x64.

The counters of the last line are the sums of the reports under `artifacts/test-results/`: passed, failed and skipped of the JIT runs and of the Native AOT runs, with the number of reports. Measured on 2026-10-30 in one container, with an incremental build: from 115 to 153 seconds in all, depending on how much the build recompiles; the slowest part is the four publishes, which run at the same time.

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

The command `min-sdk` of `eng/run` has four steps. They are C# in `eng/run/MinSdk.cs`, not a shell script, because the logic reads the output of the build. The image has only the SDK 10.0.100, which cannot build `eng/run` for the root `global.json`, so the service runs the `eng/run` that the service `test` compiled (`eng/run/bin/Release/net10.0/Great.Parts.Cel.Eng.Run.dll`, a `net10.0` program that the .NET 10 runtime of the image runs). The working directory of the container is `/repo`; the child processes of the steps run in `eng/min-sdk`, where its `global.json` selects the SDK. Before the first step `eng/run` deletes `artifacts/min-sdk/`. The steps:

1. Stop with `min-sdk: failed: min-sdk: packages (no package in artifacts/packages; run the service test first)` when `artifacts/packages` has no `Great.Parts.Cel.<version>.nupkg`.
2. `dotnet --version` in `eng/min-sdk`; any version other than `10.0.100` fails the run.
3. `dotnet build -c Release --artifacts-path <artifacts/min-sdk>/build -p:ReportAnalyzer=true -flp:"LogFile=<artifacts/min-sdk>/build.log;Verbosity=diagnostic"` in `eng/min-sdk`. The console keeps the default verbosity, so the compiler lines appear in the output, and a copy of it goes to `artifacts/min-sdk/console.log`; the build passes when its exit code is 0 and the copy has the lines `0 Warning(s)` and `0 Error(s)`. The log file gets the analyzer report.
4. Find the evidence that the generator loaded and print the lines found with their numbers: the `project.assets.json` of the restore names `analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll` as an analyzer of the package, and the generator part of the analyzer report in `build.log` names `Great.Parts.Cel.Generator.CelTypeContextGenerator`.

Each child process of steps 2 and 3 has the limit of 20 s ([Time limits](#time-limits)); a process that runs out of time is killed and fails its step. A failed step 1, 2 or 3 stops the command, as the next steps need its result. The run passes when every step succeeds: SDK 10.0.100, a build with no warning and no error, both pieces of evidence found. `CS9057` and `CS8032` (an analyzer that cannot be created) are warnings, so `TreatWarningsAsErrors` makes either of them a failed build. The last line is the summary of `eng/run` ([eng/run](#engrun)): `min-sdk: passed` or `min-sdk: failed: <steps>`, the duration and the exit code 0 or 1. The sample application builds without SourceLink and reads no git; only `eng/run` reads it, for `head` and `tree`, so in a linked worktree the service needs the second Compose file as `test` does. The generator has no pipeline yet, so the run shows that it loads, not that it produces code.

Order of a run, from the repository root; from a linked worktree both commands add the second Compose file ([Linked worktrees](#linked-worktrees)):

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
| `artifacts/test-results/` | the reports of the JIT runs and of the Native AOT runs of `test` |
| `artifacts/aot/` | the Native AOT publish of `test` |
| `artifacts/packages/` | `eng/checks packages` |
| `artifacts/docs/` | `eng/docs` |
| `artifacts/mutate/` | `eng/mutate` |
| `artifacts/oracle/` | the launcher of the oracle container ([eng-oracle.md](eng-oracle.md#launcher)) |
| `artifacts/min-sdk/` | the service `min-sdk`: its packages folder, build output and build log ([min-sdk](#min-sdk)) |
| `artifacts/worktree/` | the host, once per linked worktree; no command deletes it |

The checks of `eng/checks` skip `artifacts/`, like `bin/` and `obj/`.

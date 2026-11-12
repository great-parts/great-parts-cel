# eng/test and the containers of eng/

`eng/test` is the one full check of a change: it builds the solution, runs every test under JIT, runs the source and package checks, and runs the tests of the two projects that are published under Native AOT on linux-x64, comparing their test count with the JIT run. There is no separate command for Native AOT. It runs in a Linux container with the .NET 11 SDK, the .NET 10 runtime and the native toolchain, without Go. The full check of a change is made by CI on every pull request and on every push to `main` ([CI](#ci)); the command stays for a manual run on a machine with Docker. The same container setup serves `eng/docs` ([eng-docs.md](eng-docs.md)) and `eng/mutate` ([eng-mutate.md](eng-mutate.md)); other components add their services to the same Compose file and their commands to the same project.

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

In a linked worktree (`git worktree add`) `.git` is a file whose line `gitdir: <path>` holds an absolute host path, which does not exist in the container. A run from a linked worktree adds the second Compose file `eng/compose.worktree.yaml` with its two variables set for one command:

```text
ENG_GIT_COMMON_DIR="$(git rev-parse --path-format=absolute --git-common-dir)" \
ENG_WORKTREE_NAME="$(basename "$(git rev-parse --show-toplevel)")" \
  docker compose -f eng/compose.yaml -f eng/compose.worktree.yaml run --rm test
```

**Project name.** All working copies mount their own tree at `/repo`, but a Compose project is shared by every run that has its name, and the name `great-parts-cel-eng` of `eng/compose.yaml` is the same in every working copy. The service `oracle` is the case that matters: `docker compose run difftest` starts it as a dependency and leaves it in the project, so a run from another working copy recreates it with its own `/repo`, which ends the session of the first run. The second file therefore sets `name: great-parts-cel-eng-${ENG_WORKTREE_NAME}`, with the name of the directory of the working copy; Compose lowercases it. A missing variable stops the run, as a missing `ENG_GIT_COMMON_DIR` does. The main working copy keeps `great-parts-cel-eng`, and a run from it needs no second file. What stays shared on purpose has a fixed name that the project does not change: the volumes `great-parts-cel-eng_nuget`, `_gomod` and `_gobuild`, and the images, which `eng/compose.yaml` names (`image: great-parts-cel-eng-<service>`) and which hold only the toolchain. Compose warns that a volume was created for another project; the warning is harmless. The last line of `eng/run` names the commit and the state of the tree of the working copy that it ran in, and it shows the `head` of the working copy that started the run; another `head` means that the run did not build this tree.

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

- A step has no time limit: `eng/run` does not stop a step that runs long ([Time limits](#time-limits)).
- Before a step it writes a header line `== <step>`. The output of a stage of one lane passes through as the steps write it. The output of every lane of a stage of several lanes is written as a whole when the lane ends, so lines of different processes are not mixed.
- A step stops the rest of its lane when it fails and is marked so, because the next steps need its result; a stage stops the command when it has a failed step and is marked so. A step that is not marked does not stop the others, so one run shows all failures.
- Each command, and the launcher of the oracle, first deletes the directories under `artifacts/` that it writes, so every report there comes from the current run. The lanes of the Native AOT publish also delete what an unfinished publish left under `obj/` ([Unfinished Native AOT outputs](#unfinished-native-aot-outputs)).
- The last line of every command is its summary, in this form: `<command>: passed` or `<command>: failed: <steps or a reason>`; `head <40 characters of the SHA of HEAD>`; `tree clean`, `tree dirty` or `tree unknown`; the duration in seconds; `exit <code>`; for `test` also the counters. The lines do not match the finding pattern. `tree` is `clean` when `git status --porcelain` listed nothing before the first step and after the last one, `unknown` when git cannot answer. A reviewer does not repeat a run whose summary names the same SHA of `HEAD` and a clean tree.
- The exit code is 0 when every step returned 0, otherwise 1. One step is marked so that its exit code is not used (`Step.IgnoresExitCode`): the lychee step of `docs`, whose report is the result ([eng-docs.md](eng-docs.md#steps)). A program that does not start still fails such a step. After a successful build, `difftest` returns the exit code of `eng/difftest` unchanged: 0, 1 or 2 ([eng-difftest.md](eng-difftest.md#findings-and-output)).
- `eng/checks` is built once, with the solution or by the first step of `mutate`; every later call of it is `dotnet run --project eng/checks -c Release --no-build`.

## Time limits

`eng/run` sets no time limit on a step and does not wait for a quiet host before a stage. The resources of the machine are not spared: a run uses the processors it needs, and a step that takes long is not a failure by itself. A hung process is stopped in CI by the limit of the job that GitHub Actions gives by default, 6 hours (the workflows set no `timeout-minutes`); on a developer machine, by whoever started the command. A step that does not start fails, as before.

These are not limits of a step and stay as they are:

- the limits of the library itself: the limit of the cost of an evaluation and the timeouts of regular expressions, with their tests;
- the option `--budget` of `eng/difftest`, the time of the differential run, and the budget of the workflows ([eng-difftest.md](eng-difftest.md#invocation)): the program ends its run by it, `eng/run` does not read it;
- the limit of one built program in `eng/checks examples` ([eng-docs.md](eng-docs.md#steps)), a rule of that check.

## Unfinished Native AOT outputs

A Native AOT publish that was killed in the middle, by a user or by the end of the container, can leave a half-written object file under `<project>/obj/Release/<target>/linux-x64/native/` and a half-linked executable in `artifacts/aot/`. The next publish takes the object file as up to date, links it and produces a program that crashes (exit code 139) or cannot be started (`Permission denied`). `eng/run` does not take the result of an unfinished publish for a current one:

- The lane of every published project and target starts with a step in this process, `aot: delete artifacts/aot/<project>/<target> <project directory>/obj/Release/<target>/linux-x64/native`. It deletes both directories, so the publish always runs the native compiler in full and nothing of an earlier publish reaches the run of the executable. `artifacts/aot/` is also deleted at the start of the command ([eng/run](#engrun)); the step covers the lane when it is run alone and the directory under `obj/`, which the start does not delete.
- The compiled assemblies of `obj/`, the restore and the outputs of the build are kept: only the output of the native compiler is not trusted.
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
| `Program.cs` | a type context of two types, `SampleContext`, and one statement that reads `SampleContext.Default`; see "The context of the application" below |

Every local build packs version `0.0.0-dev`, and NuGet does not look at the source again for a version that its packages folder already has. So the application does not use the volume `nuget`: it has its own packages folder under `artifacts/min-sdk/`, and the command deletes that directory first. Every run then takes the package that the last `test` packed.

**The context of the application.** `Program.cs` declares `[CelObject(typeof(Customer))] [CelObject(typeof(Order))] internal sealed partial class SampleContext : CelTypeContext;` with two small types in the same file — `Customer` with a `string` and a `long`, `Order` with a `long`, a `Customer` and a `List<string>` — and prints `SampleContext.Default.SourceName`. The project has no nullable context, which is what a project of this age may have, so the generator reads `string` as nullable there. `Default` exists only in the file that the generator creates: a compiler that skipped the generator (`CS9057`) or a generator that created nothing fails the build with `CS0117`, so a passing build already shows that the generator ran on this SDK and wrote a file that compiles in a project with warnings as errors and none of the settings of the repository. The text of the file is checked by the generator tests, not here.

The command `min-sdk` of `eng/run` has four steps. They are C# in `eng/run/MinSdk.cs`, not a shell script, because the logic reads the output of the build. The image has only the SDK 10.0.100, which cannot build `eng/run` for the root `global.json`, so the service runs the `eng/run` that the service `test` compiled (`eng/run/bin/Release/net10.0/Great.Parts.Cel.Eng.Run.dll`, a `net10.0` program that the .NET 10 runtime of the image runs). The working directory of the container is `/repo`; the child processes of the steps run in `eng/min-sdk`, where its `global.json` selects the SDK. Before the first step `eng/run` deletes `artifacts/min-sdk/`. The steps:

1. Stop with `min-sdk: failed: min-sdk: packages (no package in artifacts/packages; run the service test first)` when `artifacts/packages` has no `Great.Parts.Cel.<version>.nupkg`.
2. `dotnet --version` in `eng/min-sdk`; any version other than `10.0.100` fails the run.
3. `dotnet build -c Release --artifacts-path <artifacts/min-sdk>/build -p:ReportAnalyzer=true -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<artifacts/min-sdk>/generated -flp:"LogFile=<artifacts/min-sdk>/build.log;Verbosity=diagnostic"` in `eng/min-sdk`. The console keeps the default verbosity, so the compiler lines appear in the output, and a copy of it goes to `artifacts/min-sdk/console.log`; the build passes when its exit code is 0 and the copy has the lines `0 Warning(s)` and `0 Error(s)`. The log file gets the analyzer report.
4. Find the evidence that the generator loaded and created code, and print the lines found with their numbers: the `project.assets.json` of the restore names `analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll` as an analyzer of the package; the generator part of the analyzer report in `build.log` names `Great.Parts.Cel.Generator.CelTypeContextGenerator`; and a file `SampleContext.g.cs` under `<artifacts/min-sdk>/generated/` (the compiler writes the created files there, in a directory named for the generator) contains the line `partial class SampleContext`, which is the created file that went into the assembly.

A failed step 1, 2 or 3 stops the command, as the next steps need its result. The run passes when every step succeeds: SDK 10.0.100, a build with no warning and no error, the three pieces of evidence found. `CS9057` and `CS8032` (an analyzer that cannot be created) are warnings, so `TreatWarningsAsErrors` makes either of them a failed build. The last line is the summary of `eng/run` ([eng/run](#engrun)): `min-sdk: passed` or `min-sdk: failed: <steps>`, the duration and the exit code 0 or 1. The sample application builds without SourceLink and reads no git; only `eng/run` reads it, for `head` and `tree`, so in a linked worktree the service needs the second Compose file as `test` does. The run shows that the generator loads and that it produces a file that compiles; what the file says is the business of the generator tests.

Order of a run, from the repository root; from a linked worktree both commands add the second Compose file ([Linked worktrees](#linked-worktrees)):

```text
docker compose -f eng/compose.yaml run --rm test
docker compose -f eng/compose.yaml run --rm min-sdk
```

The service is not part of `test`; CI runs it in its own job ([CI](#ci)). A manual run is useful when the generator or the Roslyn version of the generator changes, or when the oldest supported SDK changes.

## CI

`.github/workflows/ci.yml` is the full check of a change. It runs on every pull request and on every push to `main`; a change is not checked by the containers of `eng/` before it is pushed, `eng/difftest` for a change of the core of the library included: the differential run is its own workflow, `.github/workflows/difftest-main.yml`, on every push to `main` ([eng-difftest.md](eng-difftest.md#run-on-a-push-to-main)). Locally only single tests and the reproduction of one case of `eng/difftest` are run. The commands of this page stay for a manual run.

| Job | Runs | Same as |
| :- | :- | :- |
| `JIT <os> <target>`, Ubuntu and macOS, `net10.0` and `net11.0` | restore in locked mode, build, every test project under JIT; in the cell Ubuntu `net11.0` also the tests of `eng/` and `suppressions duplicates ratchet packages` | the build, the JIT run and the checks of `test` |
| `AOT linux-x64 <target>`, `net10.0` and `net11.0` | build, JIT run of the published projects, the Native AOT publish and run, `test-count` | stage 2 (publish lanes) and stage 3 of `test` |
| `Docs` | the build of the library, `examples`, `error-codes`, lychee offline and `links`, then lychee for external links and `links` | `docs`, with the external links ([eng-docs.md](eng-docs.md)) |
| `min-sdk` | restore, build, `dotnet pack`, then the service `min-sdk` ([min-sdk](#min-sdk)) | the order `test`, `min-sdk` of a manual run |
| `Signed-off-by` | `eng/checks signoff` on the new commits | |

The differential run `difftest-main` is not a job of `ci`: it runs on a push to `main` only, never on a pull request, and has its own section ([eng-difftest.md](eng-difftest.md#run-on-a-push-to-main)).

The cells run the commands of the steps of `eng/run` directly, without containers. The workflows set no `timeout-minutes`: the protection against a hung job is the default limit of GitHub Actions, 6 hours. The runners have no SDK 11, so `actions/setup-dotnet` installs the SDK of `global.json` and the SDK 10.0.x. The job `min-sdk` runs the Compose service with `docker compose -f eng/compose.yaml run --rm -T min-sdk`, as the runs of `eng/difftest` do; the log of the run shows the summary line of `eng/run`.

`min-sdk` runs on every push and pull request, not by the paths of a change. Its result depends on the library and on the generator, which are most of the repository, so a filter by paths would be a second place to keep right. The job runs at the same time as the others, so it does not make the run longer by much. The control set of Stryker.NET is not a job of `ci.yml`: it runs before release 1.0, together with `eng/mutate` ([eng-mutate.md](eng-mutate.md#control-set)). Time of the jobs on a hosted runner (jobs of the run of 2026-11-09, `ci` on `main`; `min-sdk` is an estimate from the steps of the same kind, to be replaced by the first runs):

| Job | Time |
| :- | :- |
| `JIT Ubuntu`, one cell | 143 s to 229 s |
| `JIT macOS`, one cell | 243 s to 249 s |
| `AOT linux-x64`, one cell | 331 s to 337 s |
| `Docs` | 71 s |
| `min-sdk` | about 4 minutes: build of the solution 110 s to 150 s, the image and the steps of `min-sdk` (16 s for the command) |

No job sets `timeout-minutes`: a job that hangs is stopped by the default limit of GitHub Actions, 6 hours. `eng/run` has no limits of steps either ([Time limits](#time-limits)).

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

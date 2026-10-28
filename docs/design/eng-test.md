# eng/test and the containers of eng/

`eng/test` is the blocking check of a change: it builds the solution, runs every test under JIT and runs the source and package checks. It runs in a Linux container with the .NET 11 SDK and the .NET 10 runtime, without Go. The same container setup serves `eng/docs` ([eng-docs.md](eng-docs.md)) and `eng/mutate` ([eng-mutate.md](eng-mutate.md)); other components add their services to the same Compose file and their commands to the same project.

## Commands

Each command is a service of the Compose file `eng/compose.yaml` and runs with one command from the repository root:

```text
docker compose -f eng/compose.yaml run --rm test
docker compose -f eng/compose.yaml run --rm test-aot
docker compose -f eng/compose.yaml run --rm docs
docker compose -f eng/compose.yaml run --rm mutate
```

| Service | Name in the documentation | Image stage | What it does |
| :- | :- | :- | :- |
| `test` | `eng/test` | `sdk` | build, all tests under JIT, source and package checks |
| `test-aot` | `eng/test` for Native AOT | `sdk-aot` | tests under JIT and Native AOT on linux-x64, test count |
| `docs` | `eng/docs` | `docs` | examples, error codes page, offline links |
| `mutate` | `eng/mutate` | `sdk` | mutation testing of the changed code |

Every service:

- mounts the working copy at `/repo` and works there, so build outputs and reports land in the working copy;
- keeps NuGet packages in the named volume `nuget` at `/root/.nuget/packages`, so restore is fast after the first run;
- takes no environment variables from the host, so a run does not depend on the starting shell; Compose reads the variable of the second Compose file ([Linked worktrees](#linked-worktrees)) on the host, not in the container;
- has the entry point `dotnet run --project eng/run -- <service>`; arguments after the service name of `docker compose run` are passed to that command.

The service `oracle` of the differential run is the exception: it runs the image `golang` without .NET, with its own entry point and volumes, and the service `difftest` depends on it ([eng-oracle.md](eng-oracle.md#container)).

## Image

`eng/docker/Dockerfile` has three stages. The build context is `eng/docker/`, which holds only this file: nothing of the working copy is copied into the image.

| Stage | Base image | Added |
| :- | :- | :- |
| `sdk` | `mcr.microsoft.com/dotnet/sdk` with the SDK version of `global.json` | the .NET 10 runtime, copied from the `mcr.microsoft.com/dotnet/sdk` 10.0 image; `/repo` and `/git` registered as safe directories of `git` |
| `sdk-aot` | the `-aot` variant of the same SDK version: it adds the native toolchain that Native AOT needs | the same as `sdk` |
| `docs` | the `sdk` stage | the `lychee` program, copied from `lycheeverse/lychee` 0.24.2 |

- Tags are exact versions; floating tags such as `latest` are not used. The SDK tag changes together with `global.json`.
- The .NET 10 runtime runs the tests and examples for `net10.0` and the mutation tool.
- `git` comes with the SDK image; the safe directory entries avoid its ownership error for files of another host user.

## Linked worktrees

In every service `/repo` must be a git repository with the local `main` and the history down to the merge base, and the host repository must stay unchanged. Two readers need it: `git` for the new places of the ratchet ([eng-checks-source.md](eng-checks-source.md)) and the SourceLink lookup of every build, which warns without it. `eng/mutate` does not run in a linked worktree ([eng-mutate.md](eng-mutate.md#linked-worktrees)).

In a linked worktree (`git worktree add`) `.git` is a file whose line `gitdir: <path>` holds an absolute host path, which does not exist in the container. A run from a linked worktree adds the second Compose file `eng/compose.worktree.yaml` with its variable set for one command:

```text
ENG_GIT_COMMON_DIR="$(git rev-parse --path-format=absolute --git-common-dir)" \
  docker compose -f eng/compose.yaml -f eng/compose.worktree.yaml run --rm test
```

It adds two read-only bind mounts to every .NET service — not to `oracle`, which does not read git ([eng-oracle.md](eng-oracle.md#container)): the common git directory `${ENG_GIT_COMMON_DIR}` at `/git`, and the worktree's file `artifacts/worktree/gitdir` over `/repo/.git`. A mount over a file leaves the host file unchanged. Both have `create_host_path: false`: a missing source stops the run instead of creating a host directory.

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

1. Run the tests under JIT with an xUnit XML report into `artifacts/test-results/jit/`.
2. Publish for linux-x64 under Native AOT: `dotnet publish -f <target> -r linux-x64 -c Release -p:InvariantGlobalization=true -o artifacts/aot/<project>/<target>/`. Trimming and AOT warnings of the publish are compiler lines with a file and a line.
3. Run the published executable with the same report option into `artifacts/test-results/aot/`.
4. `eng/checks test-count` on the two reports, with `linux-x64` and the target as the label.

All four steps run for every pair, so one run shows every failing pair. The command takes minutes and is not part of `test`; CI runs the same publish and comparison in its Native AOT cells on linux-x64 and win-x64. Why `InvariantGlobalization` is set only on the publish command line is explained in [tests.md](tests.md).

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
| `artifacts/worktree/` | the host, once per linked worktree; no command deletes it |

The checks of `eng/checks` skip `artifacts/`, like `bin/` and `obj/`.
